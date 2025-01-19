namespace QOA
{
    public static class QOAConstants
    {
        public const int HeaderSize = 8;
        public const int FrameHeaderSize = 8;

        public const int BitDepth = 16;

        public const int SlicesPerFrameChannel = 256;
        public const int SamplesPerSlice = 20;
        public const int SamplesPerFrameChannel = SlicesPerFrameChannel * SamplesPerSlice;

        public const int LMSStateArraySize = 4;
        public const int LMSStateBytes = LMSStateArraySize * 4;  // *2 for 2 arrays, *2 for 2-bytes per short

        public const double ScaleFactorExponent = 2.75;

        public static readonly double[] DequantizationTab = new double[8] { 0.75, -0.75, 2.5, -2.5, 4.5, -4.5, 7, -7 };
    }
}
