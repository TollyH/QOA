using System.Buffers.Binary;

namespace QOA
{
    public static class QOAEncoder
    {
        /// <summary>
        /// Encode a QOA file byte stream.
        /// </summary>
        /// <returns>An encoded byte array containing the entire file.</returns>
        public static byte[] Encode(QOAFile file)
        {
            ulong totalSamples = file.SamplesPerChannel * file.ChannelCount;
            uint totalFrames = (uint)Math.Ceiling(file.SamplesPerChannel / (double)QOAConstants.SamplesPerFrameChannel);
            uint totalSlices = (uint)Math.Ceiling(totalSamples / (double)QOAConstants.SamplesPerSlice);

            byte[] data = new byte[
                QOAConstants.HeaderSize
                + (QOAConstants.FrameHeaderSize * totalFrames)
                + (QOAConstants.LMSStateBytes * file.ChannelCount * totalFrames)
                + (totalSlices * 8)
                + file.TrailingData.Length];

            Span<byte> dataSpan = data.AsSpan();

            QOAFile.MagicBytes.CopyTo(data, 0);

            BinaryPrimitives.WriteUInt32BigEndian(dataSpan[4..8], file.SamplesPerChannel);

            short[][] lmsHistory = new short[file.ChannelCount][];
            short[][] lmsWeights = new short[file.ChannelCount][];
            for (int c = 0; c < file.ChannelCount; c++)
            {
                lmsHistory[c] = new short[QOAConstants.LMSStateArraySize];
                lmsWeights[c] = new short[QOAConstants.LMSStateArraySize];
                QOAConstants.LMSInitialWeights.CopyTo(lmsWeights[c], 0);
            }

            int dataOffset = 8;

            ulong remainingSamples = totalSamples;
            ulong encodedSamples = 0;
            for (uint frameIndex = 0; frameIndex < totalFrames; frameIndex++)
            {
                ushort samplesToEncode = (ushort)Math.Min(remainingSamples / file.ChannelCount, QOAConstants.SamplesPerFrameChannel);

                QOAFrame frame = new(file.ChannelCount, file.SampleRate, samplesToEncode);

                for (int channel = 0; channel < file.ChannelCount; channel++)
                {
                    for (uint sampleIndex = 0; sampleIndex < samplesToEncode; sampleIndex++)
                    {
                        frame.ChannelSamples[channel][sampleIndex] = file.ChannelSamples[channel][encodedSamples + sampleIndex];
                    }
                }

                EncodeFrame(frame, lmsHistory, lmsWeights).CopyTo(dataSpan[dataOffset..]);

                remainingSamples -= samplesToEncode;
                encodedSamples += samplesToEncode;

                dataOffset += frame.Size;
            }

            file.TrailingData.CopyTo(dataSpan[dataOffset..]);

            return data;
        }

        /// <summary>
        /// Encode a single QOA frame. The LMS history and weight arrays will be updated.
        /// </summary>
        /// <returns>An encoded byte array containing the entire frame.</returns>
        public static byte[] EncodeFrame(QOAFrame frame, short[][] lmsHistory, short[][] lmsWeights)
        {
            byte[] frameData = new byte[frame.CalculatedSize];
            Span<byte> frameDataSpan = frameData.AsSpan();

            frameDataSpan[0] = frame.ChannelCount;

            // BinaryPrimitives does not have 24-bit methods
            frameDataSpan[1] = (byte)(frame.SampleRate >> 16);
            frameDataSpan[2] = (byte)(frame.SampleRate >> 8);
            frameDataSpan[3] = (byte)frame.SampleRate;

            BinaryPrimitives.WriteUInt16BigEndian(frameDataSpan[4..6], frame.SamplesPerChannel);
            BinaryPrimitives.WriteUInt16BigEndian(frameDataSpan[6..8], frame.Size);

            int dataOffset = 8 + QOAConstants.LMSStateBytes;
            int slicesToEncode = (int)Math.Ceiling(frame.SamplesPerChannel * frame.ChannelCount / (double)QOAConstants.SamplesPerSlice);
            for (int sliceIndex = 0; sliceIndex < slicesToEncode; sliceIndex++)
            {
                int channel = sliceIndex % frame.ChannelCount;
                int channelSliceIndex = sliceIndex / frame.ChannelCount;

                int startIndex = channelSliceIndex * QOAConstants.SamplesPerSlice;
                int endIndex = Math.Min(startIndex + QOAConstants.SamplesPerSlice, frame.SamplesPerChannel - startIndex);

                BinaryPrimitives.WriteUInt64BigEndian(frameDataSpan[dataOffset..],
                    EncodeSlice(frame.ChannelSamples[channel].AsSpan()[startIndex..endIndex], lmsHistory[channel], lmsWeights[channel]));

                dataOffset += 8;
            }

            dataOffset = 8;
            for (int c = 0; c < frame.ChannelCount; c++)
            {
                for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
                {
                    BinaryPrimitives.WriteInt16BigEndian(frameDataSpan[dataOffset..], lmsHistory[c][i]);
                    dataOffset += 2;
                }
                for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
                {
                    BinaryPrimitives.WriteInt16BigEndian(frameDataSpan[dataOffset..], lmsWeights[c][i]);
                    dataOffset += 2;
                }
            }

            return frameData;
        }

        /// <summary>
        /// Encode a single QOA slice. The LMS history and weight arrays will be updated.
        /// </summary>
        /// <param name="samples">An array of no more than <see cref="QOAConstants.SamplesPerSlice"/> samples.</param>
        /// <returns>A single QOA slice packed into a <see cref="UInt64"/>.</returns>
        public static ulong EncodeSlice(Span<short> samples, short[] lmsHistory, short[] lmsWeights)
        {
            if (samples.Length > QOAConstants.SamplesPerSlice)
            {
                throw new ArgumentException($"The number of samples must not exceed {QOAConstants.SamplesPerSlice}");
            }

            // Used to rollback LMS state for each potential scale factor
            short[] initialLmsHistory = lmsHistory.ToArray();
            short[] initialLmsWeights = lmsWeights.ToArray();

            ulong bestSlice = 0;
            uint bestError = uint.MaxValue;
            short[] bestLmsHistory = new short[QOAConstants.LMSStateArraySize];
            short[] bestLmsWeights = new short[QOAConstants.LMSStateArraySize];

            // Try all possible scale factors to find the optimal one for this slice
            for (uint potentialSfQuantized = 0; potentialSfQuantized < 16; potentialSfQuantized++)
            {
                ulong slice = 0;
                uint error = 0;

                initialLmsHistory.CopyTo(lmsHistory, 0);
                initialLmsWeights.CopyTo(lmsWeights, 0);

                slice |= (ulong)potentialSfQuantized << 60;

                double scaleFactor = Math.Round(Math.Pow(potentialSfQuantized + 1, QOAConstants.ScaleFactorExponent));

                for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
                {

                }

                if (error < bestError)
                {
                    bestSlice = slice;
                    bestError = error;

                    lmsHistory.CopyTo(bestLmsHistory, 0);
                    lmsWeights.CopyTo(bestLmsWeights, 0);
                }
            }

            bestLmsHistory.CopyTo(lmsHistory, 0);
            bestLmsWeights.CopyTo(lmsWeights, 0);

            return bestSlice;
        }
    }
}
