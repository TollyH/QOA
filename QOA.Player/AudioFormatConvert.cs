using System.Buffers.Binary;

namespace QOA.Player
{
    public static class AudioFormatConvert
    {
        /// <summary>
        /// Converts from a per-channel 16-bit signed sample array as found in <see cref="QOAFile"/> to a raw interleaved little-endian PCM byte array.
        /// All channels must have the same number of samples, and there must be at least one channel.
        /// </summary>
        /// <param name="channelSamples">
        /// An array of channels, which themselves are an array of samples.
        /// Indexes as channelSamples[channel][sample]
        /// </param>
        public static byte[] Int16ChannelsToInterleavedPCMBytesLE(short[][] channelSamples)
        {
            int samplesPerChannel = channelSamples[0].Length;
            if (channelSamples.Any(arr => arr.Length != samplesPerChannel))
            {
                throw new NotSupportedException("All channels must have the same number of samples");
            }

            int totalSamples = channelSamples.Length * samplesPerChannel;

            byte[] pcmData = new byte[totalSamples * 2];  // 2 bytes per short
            Span<byte> pcmDataSpan = pcmData.AsSpan();
            for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
            {
                int channel = sampleIndex % channelSamples.Length;
                int sample = sampleIndex / channelSamples.Length;

                BinaryPrimitives.WriteInt16LittleEndian(pcmDataSpan[(sampleIndex * 2)..], channelSamples[channel][sample]);
            }

            return pcmData;
        }
    }
}
