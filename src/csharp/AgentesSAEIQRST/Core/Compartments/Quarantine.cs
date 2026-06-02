using System;
using System.Collections.Generic;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Quarantine : Agent
    {
        public Quarantine(double x, double y) : base(Compartment.Q)
        {
            this.X = x;
            this.Y = y;
            this.VX = rand.NextDouble() * 2 - 1;
            this.VY = rand.NextDouble() * 2 - 1;
        }

        private static bool Event(double rate, double dt, Random r)
            => r.NextDouble() < 1.0 - Math.Exp(-rate * dt);

        public override void Update(double dt, IEnumerable<Agent> vecinos)
        {
            // delta
            if (Event(delta, dt, rand))
            {
                Simulation.RequestStateChange(this, new Recovered(X, Y));
                return;
            }

            // muQ
            if (Event(muQ, dt, rand))
            {
                Simulation.RequestStateChange(this, null);
                return;
            }
        }
    }
}
