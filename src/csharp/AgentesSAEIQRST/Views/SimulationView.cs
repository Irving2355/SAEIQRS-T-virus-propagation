using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AgentesSAEIQRST.Core;
using AgentesSAEIQRST.Core.Compartments;

namespace AgentesSAEIQRST.Views
{
    public class SimulationView : Control
    {
        public static readonly StyledProperty<Simulation?> SimulationProperty =
            AvaloniaProperty.Register<SimulationView, Simulation?>(nameof(Simulation));

        public Simulation? Simulation
        {
            get => GetValue(SimulationProperty);
            set => SetValue(SimulationProperty, value);
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (Simulation is null)
            {
                // fondo
                ctx.FillRectangle(Brushes.White, new Rect(Bounds.Size));
                return;
            }

            // Fondo
            ctx.FillRectangle(Brushes.White, new Rect(Bounds.Size));

            // Escala a tamaño del control
            double sx = Bounds.Width / (double)Simulation.CanvasWidth;
            double sy = Bounds.Height / (double)Simulation.CanvasHeight;

            // Aristas
            var penEdge = new Pen(Brushes.LightGray, 1.2);
            foreach (var (o, d) in Simulation.Edges)
            {
                ctx.DrawLine(penEdge,
                    new Point(o.X * sx, o.Y * sy),
                    new Point(d.X * sx, d.Y * sy));
            }

            // Nodos
            foreach (var a in Simulation.Agents)
            {
                var (brush, r) = StyleFor(a);
                double cx = a.X * sx, cy = a.Y * sy;
                ctx.DrawGeometry(brush, null, new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2)));
            }
        }

        private static (IBrush brush, double r) StyleFor(Agent a) =>
            a.State switch
            {
                Compartment.T => (Brushes.Goldenrod, 8.5),
                Compartment.S => (Brushes.SteelBlue, 4.5),
                Compartment.A => (Brushes.SeaGreen, 4.5),
                Compartment.E => (Brushes.Orange, 4.5),
                Compartment.I => (Brushes.Crimson, 5.0),
                Compartment.Q => (Brushes.MediumVioletRed, 4.5),
                Compartment.R => (Brushes.Gray, 4.5),
                _ => (Brushes.Black, 4.5)
            };
    }
}
