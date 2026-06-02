namespace AgentesSAEIQRST.Core.Optimization
{
    // Resultado del algoritmo GWO
    public class GwoResult
    {
        public bool[] BestMask { get; }
        public double BestPeakI { get; }
        public double BestFitness { get; }

        public GwoResult(bool[] bestMask, double bestPeakI, double bestFitness)
        {
            BestMask   = bestMask;
            BestPeakI  = bestPeakI;
            BestFitness = bestFitness;
        }
    }
}
