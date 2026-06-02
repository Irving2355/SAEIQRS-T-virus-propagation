using System;
using System.Collections.Generic;

namespace AgentesSAEIQRST.Core.Compartments
{
    public enum Compartment
    {
        S, A, E, I, Q, R, T
    }

    public abstract class Agent
    {
        public int CompId = -1;
        public Guid Id { get; private set; }
        public Compartment State { get; protected set; }
        public double X { get; protected set; }
        public double Y { get; protected set; }
        public double VX { get; protected set; }
        public double VY { get; protected set; }

        public double ConnectionRadius { get; protected set; } = 120.0;

        public List<Agent> Neighbors { get; private set; }

        protected static Random rand = new();

        public Agent(Compartment initialState)
        {
            Id = Guid.NewGuid();
            State = initialState;
            Neighbors = new List<Agent>();

            X = rand.NextDouble() * 800;
            Y = rand.NextDouble() * 600;
            VX = rand.NextDouble() * 2 - 1;
            VY = rand.NextDouble() * 2 - 1;
        }

        public void SetPosition(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        private static bool Event(double rate, double dt, Random r)
            => r.NextDouble() < 1.0 - Math.Exp(-rate * dt);

        public void AddNeighbor(Agent neighbor)
        {
            if (!Neighbors.Contains(neighbor))
                Neighbors.Add(neighbor);
        }

        public static bool FrozenPositions { get; set; } = false;
        public void Move(double width, double height)
        {
            if (FrozenPositions || State == Compartment.T) return;
            
            X += VX;
            Y += VY;

            if (X < 0 || X > width) VX = -VX;
            if (Y < 0 || Y > height) VY = -VY;

            X = Math.Clamp(X, 0, width);
            Y = Math.Clamp(Y, 0, height);
        }
        public abstract void Update(double dt, IEnumerable<Agent> vecinos);
        public virtual void Update(double dt)
        {
            Update(dt, Neighbors);
        }
    }
}
