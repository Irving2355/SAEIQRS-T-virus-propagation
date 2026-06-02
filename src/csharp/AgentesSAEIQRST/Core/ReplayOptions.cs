namespace AgentesSAEIQRST.Core
{
    public sealed class ReplayOptions
    {
        public int StepDurationMs { get; set; } = 160;
        public int SeedDevices { get; set; } = 424242;
        public bool WarnOnTCountMismatch { get; set; } = true;
        public bool AutoPlay { get; set; } = true;
    }

    internal readonly struct FrameCounts
    {
        public readonly int Step, S, A, E, I, Q, R, T;
        public FrameCounts(int step, int s, int a, int e, int i, int q, int r, int t)
        {
            Step = step; S = s; A = a; E = e; I = i; Q = q; R = r; T = t;
        }
        public int NDevices => S + A + E + I + Q + R;
    }
}
