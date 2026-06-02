using System;
using System.Collections.Generic;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Infected : Agent
    {
        public Infected(double x, double y) : base(Compartment.I)
        {
            this.X = x;
            this.Y = y;
            this.VX = rand.NextDouble() * 2 - 1;
            this.VY = rand.NextDouble() * 2 - 1;
        }

        private static bool AnyEvent(double lambda, double dt, Random r)
            => lambda > 0 && r.NextDouble() < 1.0 - Math.Exp(-lambda * dt);

        public override void Update(double dt, IEnumerable<Agent> vecinos)
        {
            // R, Q, muerte
            double lambda = sigma1 + sigma2 + muI;
            if (!AnyEvent(lambda, dt, rand)) return;

            // evento
            double u = rand.NextDouble() * lambda;
            if (u < sigma1)
            {
                // I -> R (recuperación)
                Simulation.RequestStateChange(this, new Recovered(X, Y));
                return;
            }
            u -= sigma1;

            if (u < sigma2)
            {
                // I -> Q (cuarentena)
                Simulation.RequestStateChange(this, new Quarantine(X, Y));
                return;
            }

            // I -> (muerte)
            Simulation.RequestStateChange(this, null);
        }
    }
}
