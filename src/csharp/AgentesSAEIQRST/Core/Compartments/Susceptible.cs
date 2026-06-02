using System;
using System.Collections.Generic;
using System.Linq;
using static AgentesSAEIQRST.Core.ParametrosSimulacion;

namespace AgentesSAEIQRST.Core.Compartments
{
    public class Susceptible : Agent
    {
        public Susceptible(double x, double y) : base(Compartment.S)
        {
            this.X = x;
            this.Y = y;
            this.VX = rand.NextDouble() * 2 - 1;
            this.VY = rand.NextDouble() * 2 - 1;
        }

        public static bool Event(double rate, double dt, Random r)
            => r.NextDouble() < 1.0 - Math.Exp(-rate * dt);

        public override void Update(double dt, IEnumerable<Agent> vecinos)
        {
            // alpha
            if (Event(alpha, dt, rand))
            {
                Simulation.RequestStateChange(this, new Antidote(this.X, this.Y));
                return;
            }

            // omega 
            if (Event(omega, dt, rand))
            {
                Simulation.RequestStateChange(this, new Transmission(this.X, this.Y));
                return;
            }

            // muS
            if (Event(muS, dt, rand))
            {
                Simulation.RequestStateChange(this, null);
                return;
            }
        }

        public void BecomeExposed()
        {
            Simulation.RequestStateChange(this, new Exposed(this.X, this.Y));
        }
    }
}
