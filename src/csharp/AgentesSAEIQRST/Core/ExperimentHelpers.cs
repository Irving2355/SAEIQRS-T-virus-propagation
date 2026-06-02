using System;
using System.Linq;
using AgentesSAEIQRST.Core;

public static class ExperimentHelpers
{
    /// Ejecuta una simulación sin intervención 
    /// (sin GWO) y regresa el pico de infectados.
    public static int ComputeBaselinePeakI(
        TopologyKind topology,
        InitOptions opts,
        CustomCsvPaths? custom,
        int maxSteps)
    {
        var sim = new Simulation(enableGraphics: false)
        {
            Dt = 1.0,
            IsWellMixed = false
        };

        sim.Initialize(topology, opts, custom);

        for (int s = 0; s < maxSteps; s++)
            sim.Step();

        int peakI = sim.Historial.Max(h => h.I);
        return peakI;
    }
}
