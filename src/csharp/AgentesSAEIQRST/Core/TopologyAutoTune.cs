using System;

namespace AgentesSAEIQRST.Core
{
    public static class TopologyAutoTune
    {
        // Cada nodo T atendera aproximadamente 
        // esta cantidad de dispositivos
        public static int DefaultTargetPerT = 30;

        public static void Apply(InitOptions opts, int? targetPerTOverride = null)
        {
            if (opts == null) return;

            int devices =
                opts.CountS + opts.CountA + opts.CountE +
                opts.CountI + opts.CountQ + opts.CountR;

            int targetPerT = targetPerTOverride ?? DefaultTargetPerT;

            // Ajuste automatico de T
            int nT = Math.Max(3, (int)Math.Round(devices / (double)targetPerT));
            opts.CountT = nT;

            // parametros
            double minSide = Math.Min(opts.Width, opts.Height);

            opts.RingRadius = 0.35 * minSide;
            opts.TreeBranching = ComputeBranching(nT);
            // BusY se mantiene igual
        }

        private static int ComputeBranching(int nT)
        {
            if (nT < 6)   return 2;
            if (nT < 20)  return 3;
            if (nT < 60)  return 4;
            return 5;
        }
    }
}
