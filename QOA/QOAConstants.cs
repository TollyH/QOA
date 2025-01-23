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

        /// <summary>
        /// Maps all possible combinations of quantized scale factor (first index) and quantized residual (second index)
        /// to the corresponding dequantized residual.
        /// </summary>
        public static readonly short[,] DequantizationTab = new short[16, 8]
        {
            {    1,    -1,    3,    -3,    5,    -5,     7,     -7 },
            {    5,    -5,   18,   -18,   32,   -32,    49,    -49 },
            {   16,   -16,   53,   -53,   95,   -95,   147,   -147 },
            {   34,   -34,  113,  -113,  203,  -203,   315,   -315 },
            {   63,   -63,  210,  -210,  378,  -378,   588,   -588 },
            {  104,  -104,  345,  -345,  621,  -621,   966,   -966 },
            {  158,  -158,  528,  -528,  950,  -950,  1477,  -1477 },
            {  228,  -228,  760,  -760, 1368, -1368,  2128,  -2128 },
            {  316,  -316, 1053, -1053, 1895, -1895,  2947,  -2947 },
            {  422,  -422, 1405, -1405, 2529, -2529,  3934,  -3934 },
            {  548,  -548, 1828, -1828, 3290, -3290,  5117,  -5117 },
            {  696,  -696, 2320, -2320, 4176, -4176,  6496,  -6496 },
            {  868,  -868, 2893, -2893, 5207, -5207,  8099,  -8099 },
            { 1064, -1064, 3548, -3548, 6386, -6386,  9933,  -9933 },
            { 1286, -1286, 4288, -4288, 7718, -7718, 12005, -12005 },
            { 1536, -1536, 5120, -5120, 9216, -9216, 14336, -14336 }
        };

        public static readonly short[] LMSInitialWeights = new short[LMSStateArraySize] { 0, 0, -1, 2 };
    }
}
