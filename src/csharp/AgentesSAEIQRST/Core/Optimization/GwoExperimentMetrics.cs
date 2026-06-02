using System;
using System.Collections.Generic;

namespace AgentesSAEIQRST.Core.Optimization
{
    public class GwoExperimentMetrics
    {
        public TopologyKind Topology { get; set; }

        public int BaselinePeakI { get; set; }
        public int GwoPeakI { get; set; }

        // REDUCCION porcentual del pico
        public double ReductionPercent =>
            BaselinePeakI == 0 ? 0 :
            100.0 * (BaselinePeakI - GwoPeakI) / BaselinePeakI;

        // IDs de los T seleccionados por GWO (SOLO GUID)
        public Guid[] SelectedTNodes { get; set; } = Array.Empty<Guid>();

        public override string ToString()
        {
            return
                $"Topo={Topology}, BaselinePeakI={BaselinePeakI}, " +
                $"GWOpeakI={GwoPeakI}, Reduction={ReductionPercent:F2}%, " +
                $"SelectedT=[{string.Join(",", SelectedTNodes)}]";
        }
    }
}
