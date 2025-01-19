namespace QOA
{
    public static class Constants
    {
        public const int SlicesPerFrame = 256;
        public const int SamplesPerSlice = 20;
        public const int SamplesPerFrame = SlicesPerFrame * SamplesPerSlice;

        public const int LMSStateArraySize = 4;

        public const double ScaleFactorExponent = 2.75;

        public static readonly double[] DequantizationTab = new double[8] { 0.75, -0.75, 2.5, -2.5, 4.5, -4.5, 7, -7 };
    }
}
