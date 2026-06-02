using System;
using System.Linq;
using AgentesSAEIQRST.Core;

namespace AgentesSAEIQRST.Core.Optimization
{
    public sealed class GwoOptimizer
    {
        public record Result(bool[] BestMask, double BestFitness, int BestPeakI);

        private readonly TopologyKind _topology;
        private readonly InitOptions _opts;
        private readonly int _maxSteps;
        private readonly CustomCsvPaths? _custom;
        public List<double> FitnessHistory { get; } = new();

        public GwoOptimizer(TopologyKind topology,
                        InitOptions opts,
                        CustomCsvPaths? custom,
                        int maxSteps = 500)
        {
            _topology = topology;
            _opts = opts;
            _custom = custom;
            _maxSteps = maxSteps;
        }

        // evalua candidato
        private double EvaluateCandidate(bool[] maskOverT, out int peakI)
        {
            var sim = new Simulation(enableGraphics: false);
            sim.Dt = 1.0;
            sim.IsWellMixed = false;
            sim.Initialize(_topology, _opts, _custom);

            // aplica mascara
            sim.SetInterventionMaskOverT(maskOverT);

            // corre
            for (int s = 0; s < _maxSteps; s++)
                sim.Step();

            // pico
            peakI = sim.Historial.Max(h => h.I);
            return peakI; // minimizamos
        }

        public Result Run(int populationSize = 20, int maxIters = 50, int seed = 1234)
        {
            var rnd = new Random(seed);

            // cuantos t
            var tmpSim = new Simulation(enableGraphics: false);
            tmpSim.Initialize(_topology, _opts, _custom);
            var tNodes = tmpSim.GetTNodes();
            int nT = tNodes.Count;
            if (nT == 0)
                throw new InvalidOperationException("No hay nodos T para optimizar.");

            // poblacion
            double[][] wolves = new double[populationSize][];
            bool[][] masks = new bool[populationSize][];
            double[] fitness = new double[populationSize];

            bool[] BestMaskCopy(bool[] src)
            {
                var dst = new bool[src.Length];
                Array.Copy(src, dst, src.Length);
                return dst;
            }

            // random
            for (int i = 0; i < populationSize; i++)
            {
                wolves[i] = new double[nT];
                masks[i]  = new bool[nT];

                for (int j = 0; j < nT; j++)
                {
                    wolves[i][j] = rnd.NextDouble();
                    masks[i][j]  = wolves[i][j] > 0.5;
                }

                fitness[i] = EvaluateCandidate(masks[i], out _);
            }

            // alpha, beta, delta
            int alpha = 0, beta = 1, delta = 2;
            void SortLeaders()
            {
                var idx = Enumerable.Range(0, populationSize)
                                    .OrderBy(k => fitness[k])
                                    .ToArray();
                alpha = idx[0];
                beta  = idx[1 % populationSize];
                delta = idx[2 % populationSize];
            }

            SortLeaders();

            double bestFitness = fitness[alpha];
            int bestPeakI;
            EvaluateCandidate(masks[alpha], out bestPeakI);
            var bestMask = BestMaskCopy(masks[alpha]);

            // gwo main
            for (int iter = 0; iter < maxIters; iter++)
            {
                double a = 2.0 - 2.0 * iter / (double)maxIters;

                for (int i = 0; i < populationSize; i++)
                {
                    for (int j = 0; j < nT; j++)
                    {
                        double r1 = rnd.NextDouble();
                        double r2 = rnd.NextDouble();
                        double A1 = 2 * a * r1 - a;
                        double C1 = 2 * r2;
                        double D_alpha = Math.Abs(C1 * wolves[alpha][j] - wolves[i][j]);
                        double X1 = wolves[alpha][j] - A1 * D_alpha;

                        r1 = rnd.NextDouble();
                        r2 = rnd.NextDouble();
                        double A2 = 2 * a * r1 - a;
                        double C2 = 2 * r2;
                        double D_beta = Math.Abs(C2 * wolves[beta][j] - wolves[i][j]);
                        double X2 = wolves[beta][j] - A2 * D_beta;

                        r1 = rnd.NextDouble();
                        r2 = rnd.NextDouble();
                        double A3 = 2 * a * r1 - a;
                        double C3 = 2 * r2;
                        double D_delta = Math.Abs(C3 * wolves[delta][j] - wolves[i][j]);
                        double X3 = wolves[delta][j] - A3 * D_delta;

                        double newPos = (X1 + X2 + X3) / 3.0;
                        if (newPos < 0) newPos = 0;
                        if (newPos > 1) newPos = 1;

                        wolves[i][j] = newPos;
                        masks[i][j]  = newPos > 0.5;
                    }

                    fitness[i] = EvaluateCandidate(masks[i], out _);
                }

                SortLeaders();

                if (fitness[alpha] < bestFitness)
                {
                    bestFitness = fitness[alpha];
                    EvaluateCandidate(masks[alpha], out bestPeakI);
                    bestMask = BestMaskCopy(masks[alpha]);
                }

                FitnessHistory.Add(bestFitness);

                Console.WriteLine($"Iteracion {iter + 1} de {maxIters}  - bestFitness = {bestFitness}");
            }

            // consistencia
            bestFitness = EvaluateCandidate(bestMask, out bestPeakI);

            return new Result(bestMask, bestFitness, bestPeakI);
        }
    }
}
