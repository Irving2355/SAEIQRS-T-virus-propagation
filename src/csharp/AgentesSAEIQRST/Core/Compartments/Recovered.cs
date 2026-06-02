using System;
using System.Collections.Generic;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Recovered : Agent
    {
        public Recovered(double x, double y) : base(Compartment.R)
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
            // eta
            if (Event(eta, dt, rand))
            {
                Simulation.RequestStateChange(this, new Susceptible(X, Y));
                return;
            }

            // muR
            if (Event(muR, dt, rand))
            {
                Simulation.RequestStateChange(this, null);
                return;
            }
        }
    }
}
