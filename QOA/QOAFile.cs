namespace QOA
{
    public class QOAFile
    {
        public const int SlicesPerFrame = 256;
        public const int SamplesPerSlice = 20;
        public const int SamplesPerFrame = SlicesPerFrame * SamplesPerSlice;

        public static readonly byte[] MagicBytes = new byte[4] { 113, 111, 97, 102 };  // 'qoaf'

        public byte ChannelCount { get; }
        public uint SampleRate { get; }
        public uint SamplesPerChannel { get; }

        public short[][] ChannelSamples { get; }

        public byte[] TrailingData { get; }

        public QOAFile(byte channelCount, uint sampleRate, ushort samplesPerChannel, byte[] trailingData)
        {
            ChannelCount = channelCount;
            SampleRate = sampleRate;
            SamplesPerChannel = samplesPerChannel;

            TrailingData = trailingData;

            ChannelSamples = new short[SamplesPerFrame][];

            for (int i = 0; i < SamplesPerFrame; i++)
            {
                ChannelSamples[i] = new short[ChannelCount];
            }
        }
    }
}
