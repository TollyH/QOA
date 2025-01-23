using System.Buffers.Binary;

namespace QOA
{
    public static class QOADecoder
    {
        /// <summary>
        /// Decode a QOA file byte stream.
        /// </summary>
        /// <param name="data">A byte stream containing the entirety of a QOA file.</param>
        /// <returns>A fully decoded <see cref="QOAFile"/> instance.</returns>
        public static QOAFile Decode(Span<byte> data)
        {
            if (!data[..4].SequenceEqual(QOAFile.MagicBytes))
            {
                throw new ArgumentException("The given bytes do not start with the correct header");
            }

            uint samplesPerChannel = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);

            if (samplesPerChannel == 0)
            {
                throw new NotSupportedException("QOA files in streaming mode are not supported");
            }

            // This data is part of the first frame - however in a static file, it will be the same for every frame
            byte channelCount = data[8];
            // BinaryPrimitives doesn't have 24-bit methods
            uint sampleRate = (uint)(data[9] << 16) | (uint)(data[10] << 8) | data[11];

            QOAFile file = new(channelCount, sampleRate, samplesPerChannel);

            int frameDataIndex = 8;
            int decodedFrames = 0;
            long decodedSamples = 0;
            uint frameCount = (uint)Math.Ceiling(samplesPerChannel / (double)QOAConstants.SamplesPerFrameChannel);
            while (frameDataIndex < data.Length && decodedFrames < frameCount)
            {
                QOAFrame frame = DecodeFrame(data[frameDataIndex..]);

                if (frame.SampleRate != sampleRate || frame.ChannelCount != channelCount)
                {
                    throw new FormatException($"Frame {decodedFrames} has different sample/channel parameters to the containing file");
                }

                if (frame.Size != frame.CalculatedSize)
                {
                    throw new FormatException($"Frame {decodedFrames} has an invalid size");
                }

                for (int c = 0; c < frame.ChannelCount; c++)
                {
                    for (int s = 0; s < frame.SamplesPerChannel; s++)
                    {
                        file.ChannelSamples[c][decodedSamples + s] = frame.ChannelSamples[c][s];
                    }
                }

                decodedFrames++;
                decodedSamples += frame.SamplesPerChannel;
                frameDataIndex += frame.Size;
            }

            file.TrailingData = data[frameDataIndex..].ToArray();

            return file;
        }

        /// <summary>
        /// Decode a single QOA frame.
        /// </summary>
        /// <param name="frameData">A byte stream containing a single QOA frame.</param>
        /// <returns>A fully decoded <see cref="QOAFrame"/> instance.</returns>
        public static QOAFrame DecodeFrame(Span<byte> frameData)
        {
            byte channelCount = frameData[0];
            // BinaryPrimitives doesn't have 24-bit methods
            uint sampleRate = (uint)(frameData[1] << 16) | (uint)(frameData[2] << 8) | frameData[3];
            ushort samplesPerChannel = BinaryPrimitives.ReadUInt16BigEndian(frameData[4..6]);
            ushort size = BinaryPrimitives.ReadUInt16BigEndian(frameData[6..8]);

            int dataOffset = 8;

            short[][] lmsHistory = new short[channelCount][];
            short[][] lmsWeights = new short[channelCount][];
            for (int c = 0; c < channelCount; c++)
            {
                lmsHistory[c] = new short[QOAConstants.LMSStateArraySize];
                lmsWeights[c] = new short[QOAConstants.LMSStateArraySize];

                for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
                {
                    lmsHistory[c][i] = BinaryPrimitives.ReadInt16BigEndian(frameData[dataOffset..]);
                    dataOffset += 2;
                }
                for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
                {
                    lmsWeights[c][i] = BinaryPrimitives.ReadInt16BigEndian(frameData[dataOffset..]);
                    dataOffset += 2;
                }
            }

            QOAFrame frame = new(channelCount, sampleRate, samplesPerChannel, size);

            int totalSlices = QOAConstants.SlicesPerFrameChannel * channelCount;
            int decodedSamples = 0;
            for (int sliceIndex = 0; sliceIndex < totalSlices && dataOffset < frameData.Length; sliceIndex++, dataOffset += 8)
            {
                int channel = sliceIndex % channelCount;

                short[] samples = DecodeSlice(
                    BinaryPrimitives.ReadUInt64BigEndian(frameData[dataOffset..]),
                    lmsHistory[channel], lmsWeights[channel]);

                for (int s = 0; s < samples.Length && decodedSamples + s < samplesPerChannel; s++)
                {
                    frame.ChannelSamples[channel][decodedSamples + s] = samples[s];
                }

                if (channel == channelCount - 1)
                {
                    // Only increment decoded samples once every channel has been filled
                    decodedSamples += QOAConstants.SamplesPerSlice;
                }
            }

            return frame;
        }

        /// <summary>
        /// Decode a single QOA slice. The LMS history and weight arrays will be updated.
        /// </summary>
        /// <param name="slice">A single QOA slice packed into a <see cref="UInt64"/>.</param>
        /// <returns>An array of decoded samples.</returns>
        public static short[] DecodeSlice(ulong slice, short[] lmsHistory, short[] lmsWeights)
        {
            short[] samples = new short[QOAConstants.SamplesPerSlice];

            ulong sfQuantized = slice >> 60;

            for (int sampleIndex = 0; sampleIndex < QOAConstants.SamplesPerSlice; sampleIndex++)
            {
                short residual = QOAConstants.DequantizationTab[sfQuantized, (slice >> 57) & 0b111];
                // Last slices are in the lower bits, but we consider the upper bits in the above calculation,
                // so bring each residual up one by one
                slice <<= 3;

                int predictedSample = QOACommon.PredictSample(lmsHistory, lmsWeights);

                samples[sampleIndex] = (short)Math.Clamp(predictedSample + residual, short.MinValue, short.MaxValue);

                QOACommon.UpdateLMSState(residual, samples[sampleIndex], lmsHistory, lmsWeights);
            }

            return samples;
        }
    }
}
