using System;
using System.Collections.Generic;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Transmission : Agent
    {
        public Transmission(double x, double y, double radius = 80.0) : base(Compartment.T)
        {
            this.X = x;
            this.Y = y;
            this.VX = 0;
            this.VY = 0;
            this.ConnectionRadius = radius;
        }

        private static bool Event(double rate, double dt, Random r)
            => r.NextDouble() < 1.0 - Math.Exp(-rate * dt);

        public override void Update(double dt, IEnumerable<Agent> vecinos)
        {
            // tau
            if (Event(tau, dt, rand))
            {
                Simulation.RequestStateChange(this, new Susceptible(X, Y));
                return;
            }

            // muT
            if (Event(muT, dt, rand))
            {
                Simulation.RequestStateChange(this, null);
                return;
            }
        }
    }
}
