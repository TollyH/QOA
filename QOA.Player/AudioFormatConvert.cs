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

        /// <summary>
        /// Converts from a raw interleaved little-endian PCM byte array to a per-channel 16-bit signed sample array as found in <see cref="QOAFile"/>.
        /// </summary>
        /// <returns>
        /// An array of channels, which themselves are an array of samples.
        /// Indexes as channelSamples[channel][sample]
        /// </returns>
        public static short[][] InterleavedPCMBytesLEToInt16Channels(byte[] pcmData, uint samplesPerChannel, byte channels)
        {
            Span<byte> pcmDataSpan = pcmData.AsSpan();

            uint totalSamples = channels * samplesPerChannel;

            if (pcmData.Length < totalSamples * 2)  // 2 bytes per short
            {
                throw new ArgumentException("The given PCM data array is to short to contain all of the samples");
            }

            short[][] channelSamples = new short[channels][];
            for (int channel = 0; channel < channels; channel++)
            {
                channelSamples[channel] = new short[samplesPerChannel];
            }

            for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
            {
                int channel = sampleIndex % channelSamples.Length;
                int sample = sampleIndex / channelSamples.Length;

                channelSamples[channel][sample] = BinaryPrimitives.ReadInt16LittleEndian(pcmDataSpan[(sampleIndex * 2)..]);
            }

            return channelSamples;
        }
    }
}
