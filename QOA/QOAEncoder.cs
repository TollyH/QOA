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

                EncodeFrame(frame).CopyTo(dataSpan[dataOffset..]);

                remainingSamples -= samplesToEncode;
                encodedSamples += samplesToEncode;

                dataOffset += frame.Size;
            }

            file.TrailingData.CopyTo(dataSpan[dataOffset..]);

            return data;
        }

        /// <summary>
        /// Encode a single QOA frame.
        /// </summary>
        /// <returns>An encoded byte array containing the entire frame.</returns>
        public static byte[] EncodeFrame(QOAFrame frame)
        {
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Encode a single QOA slice. The LMS history and weight arrays will be updated.
        /// </summary>
        /// <param name="samples">An array of no more than <see cref="QOAConstants.SamplesPerSlice"/> samples.</param>
        /// <returns>A single QOA slice packed into a <see cref="UInt64"/>.</returns>
        public static ulong EncodeSlice(short[] samples, short[] lmsHistory, short[] lmsWeights)
        {
            return 0;
        }
    }
}
