namespace SpleeterToMultichannel {
    public enum UpmixOption {
        Center,
        Front,
        Screen,
        QuadroSide,
        QuadroRear,
        MidSideScreen,
        MidSideFull,
        Full,
        Skip
    }

    /// <summary>
    /// Contains how to mix a stereo input to the 8 target reference channels.
    /// </summary>
    public class OutputMatrix {
        /// <summary>
        /// Gain of the left input channel to mix to L, R, C, LFE, RL, RR, SL, SR, in this order.
        /// </summary>
        public float[] LeftInput { get; }

        /// <summary>
        /// Gain of the right input channel to mix to L, R, C, LFE, RL, RR, SL, SR, in this order.
        /// </summary>
        public float[] RightInput { get; }

        /// <summary>
        /// Voltage multipliers for constant power gain with multiple speakers used: sqrt(1 / channel count).
        /// </summary>
        internal const float out2 = .70710678118654752440084436210485f, out3 = .57735026918962576450914878050196f, out4 = .5f;

        public OutputMatrix(UpmixOption option) {
            switch (option) {
                case UpmixOption.Center:
                    LeftInput = new float[8] { 0, 0, out2, 0, 0, 0, 0, 0 };
                    RightInput = new float[8] { 0, 0, out2, 0, 0, 0, 0, 0 };
                    break;
                case UpmixOption.Front:
                    LeftInput = new float[8] { 1, 0, 0, 0, 0, 0, 0, 0 };
                    RightInput = new float[8] { 0, 1, 0, 0, 0, 0, 0, 0 };
                    break;
                case UpmixOption.Screen:
                    LeftInput = new float[8] { out2, 0, out3, 0, 0, 0, 0, 0 };
                    RightInput = new float[8] { 0, out2, out3, 0, 0, 0, 0, 0 };
                    break;
                case UpmixOption.QuadroSide:
                    LeftInput = new float[8] { out2, 0, 0, 0, 0, 0, out2, 0 };
                    RightInput = new float[8] { 0, out2, 0, 0, 0, 0, 0, out2 };
                    break;
                case UpmixOption.QuadroRear:
                    LeftInput = new float[8] { out2, 0, 0, 0, out2, 0, 0, 0 };
                    RightInput = new float[8] { 0, out2, 0, 0, 0, out2, 0, 0 };
                    break;
                case UpmixOption.MidSideScreen:
                    LeftInput = new float[8] { out3, -out3, out2, 0, 0, 0, 0, 0 };
                    RightInput = new float[8] { -out3, out3, out2, 0, 0, 0, 0, 0 };
                    break;
                case UpmixOption.MidSideFull:
                    LeftInput = new float[8] { out4, -out4, out2, 0, out4, -out4, out4, -out4 };
                    RightInput = new float[8] { -out4, out4, out2, 0, -out4, out4, -out4, out4 };
                    break;
                case UpmixOption.Full:
                    LeftInput = new float[8] { out3, 0, 0, 0, out3, 0, out3, 0 };
                    RightInput = new float[8] { 0, out3, 0, 0, 0, out3, 0, out3 };
                    break;
                default:
                    LeftInput = RightInput = new float[8];
                    break;
            }
        }
    }
}