namespace QOA
{
    public class QOAFrame
    {
        public byte ChannelCount { get; }
        public uint SampleRate { get; }
        public ushort SamplesPerChannel { get; }
        public ushort Size { get; }

        public ushort CalculatedSize => CalculateSize(ChannelCount, SamplesPerChannel);

        /// <summary>
        /// An array of channels, which themselves are an array of samples.
        /// Indexes as ChannelSamples[channel][sample]
        /// </summary>
        public short[][] ChannelSamples { get; }

        public QOAFrame(byte channelCount, uint sampleRate, ushort samplesPerChannel, ushort size)
        {
            ChannelCount = channelCount;
            SampleRate = sampleRate;
            SamplesPerChannel = samplesPerChannel;
            Size = size;

            ChannelSamples = new short[ChannelCount][];

            for (int i = 0; i < ChannelCount; i++)
            {
                ChannelSamples[i] = new short[SamplesPerChannel];
            }
        }

        public QOAFrame(byte channelCount, uint sampleRate, ushort samplesPerChannel)
            : this(channelCount, sampleRate, samplesPerChannel, CalculateSize(channelCount, samplesPerChannel)) { }

        public static ushort CalculateSize(byte channelCount, ushort samplesPerChannel)
        {
            return (ushort)(QOAConstants.FrameHeaderSize
                + (QOAConstants.LMSStateBytes * channelCount)
                + (int)Math.Ceiling(samplesPerChannel / (double)QOAConstants.SamplesPerSlice));
        }
    }

    public class QOAFile
    {
        public static readonly byte[] MagicBytes = new byte[4] { 113, 111, 97, 102 };  // 'qoaf'

        public byte ChannelCount { get; }
        public uint SampleRate { get; }
        public uint SamplesPerChannel { get; }

        /// <summary>
        /// An array of channels, which themselves are an array of samples.
        /// Indexes as ChannelSamples[channel][sample]
        /// </summary>
        public short[][] ChannelSamples { get; }

        public byte[] TrailingData { get; set; } = Array.Empty<byte>();

        public QOAFile(byte channelCount, uint sampleRate, uint samplesPerChannel)
        {
            ChannelCount = channelCount;
            SampleRate = sampleRate;
            SamplesPerChannel = samplesPerChannel;

            ChannelSamples = new short[ChannelCount][];

            for (int i = 0; i < ChannelCount; i++)
            {
                ChannelSamples[i] = new short[SamplesPerChannel];
            }
        }
    }
}
