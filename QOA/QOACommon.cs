namespace QOA
{
    public static class QOACommon
    {
        /// <summary>
        /// Update LMS state arrays with a given sample and residual.
        /// </summary>
        public static void UpdateLMSState(int residual, short sample, short[] lmsHistory, short[] lmsWeights)
        {
            short delta = (short)Math.Clamp(residual >> 4, short.MinValue, short.MaxValue);
            for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
            {
                lmsWeights[i] += (short)(lmsHistory[i] < 0 ? -delta : delta);
            }

            for (int i = 0; i < QOAConstants.LMSStateArraySize - 1; i++)
            {
                lmsHistory[i] = lmsHistory[i + 1];
            }
            lmsHistory[QOAConstants.LMSStateArraySize - 1] = sample;
        }

        /// <summary>
        /// Get the predicted sample calculated from the current LMS state.
        /// </summary>
        public static int PredictSample(short[] lmsHistory, short[] lmsWeights)
        {
            int predictedSample = 0;
            for (int i = 0; i < QOAConstants.LMSStateArraySize; i++)
            {
                predictedSample += lmsHistory[i] * lmsWeights[i];
            }

            return predictedSample >> 13;
        }

        /// <summary>
        /// Convert a quantized residual to the corresponding residual, scaled by the given scale factor.
        /// </summary>
        public static int DequantizeResidual(double scaleFactor, uint quantizedResidual)
        {
            return (int)Math.Round(
                scaleFactor * QOAConstants.DequantizationTab[quantizedResidual],
                MidpointRounding.AwayFromZero);
        }
    }
}
