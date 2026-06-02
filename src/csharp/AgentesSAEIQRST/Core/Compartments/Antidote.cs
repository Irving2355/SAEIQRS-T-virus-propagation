using System;
using System.Collections.Generic;
using System.Linq;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Antidote : Agent
    {
        public Antidote(double x, double y) : base(Compartment.A)
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
            // muA
            if (Event(muA, dt, rand))
            {
                Simulation.RequestStateChange(this, null);
                return;
            }
        }
    }
}
