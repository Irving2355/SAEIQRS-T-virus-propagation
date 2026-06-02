using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Shapes;
using Avalonia.Platform;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AgentesSAEIQRST.Core;
using AgentesSAEIQRST.Core.Compartments;
using AgentesSAEIQRST.ViewModels;
using System.Linq;
using AgentesSAEIQRST.Core.Optimization;

namespace AgentesSAEIQRST.Views
{
    public partial class MainWindow : Window
    {
        // estado ui
        //private Simulation simulation = new Simulation();
        private SimulationVM vm;
        private Simulation simulation => vm.Sim;
        private readonly Dictionary<Guid, Control> agenteVisuales = new();
        private DispatcherTimer? timer;
        private int pasosMaximos = 1500;
        private int WidthImg = 28;
        private int HeightImg = 25;

        // no reentrar
        private bool _suspendAuto = false;
        private const int TARGET_LOAD = 30;
        private const double K_RADIUS = 0.8;
        private readonly HashSet<Guid> _criticalTIds = new();
        private readonly Dictionary<Guid, Ellipse> _criticalHalos = new();

        public MainWindow()
        {
            InitializeComponent();

            vm = new SimulationVM();
            vm.Sim.StepReported += Sim_StepReported;
            DataContext = vm;

            HookInputAutoAdjust();
            vm.UseGraphics = true; // grafico
        }

        private void Sim_StepReported(int step)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (this.FindControl<TextBlock>("TxtEstado") is { } txt)
                    txt.Text = $"Estado — Step {step}";
            });
        }

        private bool UseGwo =>
            (this.FindControl<ToggleSwitch>("ToggleUseGwo")?.IsChecked ?? false);

        private void HighlightCriticalTNodes()
        {
            if (_criticalTIds.Count == 0) return;

            foreach (var ag in simulation.GetAgents())
            {
                if (ag.State == Compartment.T &&
                    _criticalTIds.Contains(ag.Id) &&
                    agenteVisuales.TryGetValue(ag.Id, out var ctrl) &&
                    ctrl is Image img)
                {
                    // grande
                    img.Width  = WidthImg * 1.8;
                    img.Height = HeightImg * 1.8;

                    // top
                    img.ZIndex = 1000;
                }
            }
        }

        // handlers botones

        private async void BtnIniciar_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                //Console.WriteLine("Inicia simulacion (headless + replay)");
                TxtEstado.Text = "Inicia simulacion";

                var (topology, opts, custom) = LeerInputsDeUI();

                vm!.Topology = topology;
                vm.Width         = opts.Width;
                vm.Height        = opts.Height;
                vm.Margin        = opts.Margin;
                vm.CountT        = opts.CountT;
                vm.CountS        = opts.CountS;
                vm.CountA        = opts.CountA;
                vm.CountE        = opts.CountE;
                vm.CountI        = opts.CountI;
                vm.CountQ        = opts.CountQ;
                vm.CountR        = opts.CountR;
                vm.RingRadius    = opts.RingRadius;
                vm.TreeBranching = opts.TreeBranching;
                vm.BusY          = opts.BusY;
                vm.Dt            = simulation.Dt;

                if (topology == TopologyKind.CustomCsv && custom != null)
                {
                    vm.NodesCsv = custom.NodesCsvPath;
                    vm.EdgesCsv = custom.EdgesCsvPath;
                }

                string csvPath = "salidas/resultado_agentes.csv";

                // HEADLESS
                vm.UseGraphics = false;
                await vm.RunHeadlessAndSaveCsvAsync(
                    steps: pasosMaximos,
                    csvPath: csvPath,
                    streamCsv: true,
                    flushEveryNSteps: pasosMaximos);

                TxtEstado.Text = "headless termino, csv generado.";

                // Simulation gráfica para REPLAY
                vm.UseGraphics = true; //true para lo grafico
                var sim = vm.Sim;
                sim.Dt = simulation.Dt;
                sim.Initialize(topology, opts, custom);

                // Si GWO está activo, correr optimizador pero SOLO guardar el resultado.
                _criticalTIds.Clear();
                bool useGwo = UseGwo;
                AgentesSAEIQRST.Core.Optimization.GwoOptimizer.Result? gwoResult = null;

                // metricas
                GwoExperimentMetrics? gwoMetrics = null;
                int baselinePeakI = 0;   // pico baseline

                AgentesSAEIQRST.Core.Optimization.GwoOptimizer? optimizer = null;

                if (useGwo)
                {
                    TxtGwoSummary.Text = "Ejecutando GWO para encontrar nodos T críticos...";
                    int maxStepsForGwo = 500;

                    // Correr simulación baseline SIN GWO para obtener el pico de I
                    baselinePeakI = RunBaselinePeakI(topology, opts, custom, maxStepsForGwo);

                    // Ejecutar el optimizador GWO (igual que antes)
                    optimizer = new AgentesSAEIQRST.Core.Optimization.GwoOptimizer(
                        topology,
                        opts,
                        custom,
                        maxStepsForGwo);

                    gwoResult = await Task.Run(() => optimizer.Run(
                            populationSize: 10,
                            maxIters: 20,
                            seed: 1234));
                }
                else
                {
                    TxtGwoSummary.Text = "GWO desactivado.";
                }

                //  GENERAR CSV OPTIMIZADO CON GWO
                if (useGwo && gwoResult != null)
                {
                    string csvGwo = "salidas/resultado_agentes_gwo.csv";

                    // simulacion headless
                    var simGwo = new Simulation(enableGraphics: false);
                    simGwo.Dt = simulation.Dt;
                    simGwo.Initialize(topology, opts, custom);

                    // aplica mascara
                    simGwo.SetInterventionMaskOverT(gwoResult.BestMask);

                    // ejecuta headless
                    for (int s = 0; s < pasosMaximos; s++)
                        simGwo.Step();

                    // guardar csv
                    var lines2 = new List<string> { "Paso,S,A,E,I,Q,R,T" };
                    for (int i = 0; i < simGwo.Historial.Count; i++)
                    {
                        var h = simGwo.Historial[i];
                        lines2.Add($"{i},{h.S},{h.A},{h.E},{h.I},{h.Q},{h.R},{h.T}");
                    }
                    File.WriteAllLines(csvGwo, lines2);

                    TxtGwoSummary.Text +=
                        $"\nCSV optimizado guardado en:\n{csvGwo}";

                    // guardar convergencia
                    if (optimizer != null)
                    {
                        var histCsv = "salidas/gwo_convergence.csv";
                        var lines = new List<string>();
                        lines.Add("Iter,BestFitness");

                        for (int i = 0; i < optimizer.FitnessHistory.Count; i++)
                            lines.Add($"{i},{optimizer.FitnessHistory[i]}");

                        File.WriteAllLines(histCsv, lines);

                        TxtGwoSummary.Text +=
                            $"\nConvergencia guardada en:\n{histCsv}";
                    }
                }

                // REPLAY desde CSV
                var replayOpts = new ReplayOptions
                {
                    AutoPlay = false,
                    StepDurationMs = 0,
                    SeedDevices = 123,
                    WarnOnTCountMismatch = true
                };

                // arranca el replay desde csv
                Console.WriteLine("iniciando replay...");
                sim.StartReplayFromCsv(
                    csvPath,
                    topology,
                    opts,
                    replayOpts);

                // preparar canvas con los agentes del pool de replay
                PrepararCanvasConAgentes();

                // canvas preparado
                Console.WriteLine($"canvas: children={SimulationCanvas.Children.Count}, agentes={agenteVisuales.Count}");

                // Después del replay, mapear máscara -> IDs de los T ACTUALES
                if (useGwo && gwoResult != null)
                {
                    _criticalTIds.Clear();

                    var tNodes = sim.GetTNodes();   // t en escena
                    var mask   = gwoResult.BestMask;

                    var selectedGuids = new List<Guid>();

                    for (int i = 0; i < tNodes.Count && i < mask.Length; i++)
                    {
                        if (mask[i])
                        {
                            var id = tNodes[i].Id;
                            _criticalTIds.Add(id);
                            selectedGuids.Add(id);
                        }
                    }

                    // metricas tabla
                    gwoMetrics = new GwoExperimentMetrics
                    {
                        Topology        = topology,
                        BaselinePeakI   = baselinePeakI,
                        GwoPeakI        = gwoResult.BestPeakI,
                        SelectedTNodes  = selectedGuids.ToArray()
                    };

                    // guardar csv
                    SaveGwoMetricsCsv(gwoMetrics);

                    TxtGwoSummary.Text =
                        $"GWO activado.\n" +
                        $"T críticos seleccionados: {_criticalTIds.Count}\n" +
                        $"Pico sin GWO: {gwoMetrics.BaselinePeakI}\n" +
                        $"Pico con GWO: {gwoMetrics.GwoPeakI}\n" +
                        $"Reducción: {gwoMetrics.ReductionPercent:F2}%\n" +
                        $"T nodos: {string.Join(", ", gwoMetrics.SelectedTNodes)}";
                }

                // sync inicial
                SyncVisualsWithAgents();

                // Timer que avanza sólo el replay
                timer?.Stop();
                int agentCount = sim.GetAgents().Count();
                int intervalMs = agentCount > 4000 ? 200 : 30;  

                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(intervalMs)
                };
                timer.Tick += (_, __) =>
                {
                    bool avanzó = sim.StepReplayOnce();
                    SyncVisualsWithAgents();

                    if (!avanzó)
                    {
                        TxtEstado.Text = "replay termino.";
                        timer!.Stop();
                    }
                };
                timer.Start();

                // replay grafico iniciado
            }
            catch (Exception ex)
            {
                await MostrarError($"Error al iniciar simulación:\n{ex.Message}");
            }
        }

    
        private void BtnStep_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            TxtEstado.Text = "Step sencillo";
            var sim = vm!.Sim;

            // avanza frame
            bool avanzó = sim.StepReplayOnce();
            SyncVisualsWithAgents();

            if (!avanzó)
                HighlightCriticalTNodes();
        }

        private void BtnRunStop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (timer == null) return;
            if (timer.IsEnabled)
            {
                TxtEstado.Text = "Pause";
                timer.Stop();
            }
            else
            {
                TxtEstado.Text = "Play";
                timer.Start();
            }
        }

        private void RedibujarConexionesFijas()
        {
            if (vm == null || !vm.UseGraphics) return;
            var sim = vm.Sim;

            // borra lineas
            for (int i = SimulationCanvas.Children.Count - 1; i >= 0; i--)
                if (SimulationCanvas.Children[i] is Line)
                    SimulationCanvas.Children.RemoveAt(i);

            foreach (var (origen, destino) in sim.GetConexionesFijas())
            {
                // Usar las imágenes
                if (!agenteVisuales.TryGetValue(origen.Id, out var ctrlO) ||
                    !agenteVisuales.TryGetValue(destino.Id, out var ctrlD) ||
                    ctrlO is not Image imgO ||
                    ctrlD is not Image imgD ||
                    !imgO.IsVisible || !imgD.IsVisible)
                    continue;

                double x1 = Canvas.GetLeft(imgO) + imgO.Width  / 2.0;
                double y1 = Canvas.GetTop(imgO) + imgO.Height / 2.0;
                double x2 = Canvas.GetLeft(imgD) + imgD.Width  / 2.0;
                double y2 = Canvas.GetTop(imgD) + imgD.Height / 2.0;

                var linea = new Line
                {
                    StartPoint      = new Avalonia.Point(x1, y1),
                    EndPoint        = new Avalonia.Point(x2, y2),
                    Stroke          = Brushes.Gray,
                    StrokeThickness = 1,
                    ZIndex          = -1
                };

                SimulationCanvas.Children.Add(linea);
            }
        }


        private void PrepararCanvasConAgentes()
        {
            if (!vm!.UseGraphics) return;

            SimulationCanvas.Children.Clear();
            agenteVisuales.Clear();

            foreach (var (origen, destino) in simulation.GetConexionesFijas())
            {
                var linea = new Line
                {
                    StartPoint = new Avalonia.Point(origen.X + 5, origen.Y + 5),
                    EndPoint = new Avalonia.Point(destino.X + 5, destino.Y + 5),
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    ZIndex = -1
                };
                SimulationCanvas.Children.Add(linea);
            }

            foreach (var agente in simulation.GetAgents())
            {
                string iconPath = $"avares://AgentesSAEIQRST/Assets/Icons/{GetImageFileName(agente.State, agente.Id)}";
                var imagen = new Image
                {
                    Width = WidthImg,
                    Height = HeightImg,
                    Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri(iconPath)))
                };

                Canvas.SetLeft(imagen, agente.X);
                Canvas.SetTop(imagen, agente.Y);
                SimulationCanvas.Children.Add(imagen);
                agenteVisuales[agente.Id] = imagen;
            }

            RedibujarConexionesFijas();
        }

        // utilidades

        private async Task MostrarError(string mensaje)
        {
            var cerrarButton = new Button
            {
                Content = "Cerrar",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var dialog = new Window
            {
                Width = 420,
                Height = 240,
                Title = "Error",
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap },
                        cerrarButton
                    }
                }
            };

            cerrarButton.Click += (s, e) => dialog.Close();
            await dialog.ShowDialog(this);
        }

        private void GuardarCSV()
        {
            try
            {
                var lines = new List<string> { "Paso,S,A,E,I,Q,R,T" };
                for (int i = 0; i < simulation.Historial.Count; i++)
                {
                    var h = simulation.Historial[i];
                    lines.Add($"{i},{h.S},{h.A},{h.E},{h.I},{h.Q},{h.R},{h.T}");
                }

                var ruta = System.IO.Path.Combine(AppContext.BaseDirectory, "resultado_agentes.csv");
                File.WriteAllLines(ruta, lines);
            }
            catch { /* no-op */ }
        }

        private string GetImageFileName(Compartment estado, Guid idAgente) =>
        (estado == Compartment.T && _criticalTIds.Contains(idAgente))
            ? "pc_t_critical.png" // icono especial para T críticos
            : estado switch
            {
                Compartment.S => "pc_s.png",
                Compartment.A => "pc_a.png",
                Compartment.E => "pc_e.png",
                Compartment.I => "pc_i.png",
                Compartment.Q => "pc_q.png",
                Compartment.R => "pc_r.png",
                Compartment.T => "pc_t.png",
                _ => "pc_s.png"
            };

        // lectura de inputs

        private (TopologyKind topology, InitOptions opts, CustomCsvPaths? custom) LeerInputsDeUI()
        {
            var topText = (TopologiaSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ring";
            var topology = topText switch
            {
                "Bus" => TopologyKind.Bus,
                "Star" => TopologyKind.Star,
                "Ring" => TopologyKind.Ring,
                "Mesh" => TopologyKind.Mesh,
                "Tree" => TopologyKind.Tree,
                "CustomCsv" or "Custom" or "CSV" => TopologyKind.CustomCsv,
                _ => TopologyKind.Ring
            };

            int I(TextBox? t, int def) => (t != null && int.TryParse(t.Text, out var v)) ? v : def;
            double D(TextBox? t, double def) => (t != null && double.TryParse(t.Text, out var v)) ? v : def;

            var txtW = this.FindControl<TextBox>("TxtWidth");
            var txtH = this.FindControl<TextBox>("TxtHeight");
            var txtM = this.FindControl<TextBox>("TxtMargin");
            var txtT = this.FindControl<TextBox>("TxtCountT");
            var txtS = this.FindControl<TextBox>("TxtCountS");
            var txtA = this.FindControl<TextBox>("TxtCountA");
            var txtE = this.FindControl<TextBox>("TxtCountE");
            var txtI = this.FindControl<TextBox>("TxtCountI");
            var txtQ = this.FindControl<TextBox>("TxtCountQ");
            var txtR = this.FindControl<TextBox>("TxtCountR");
            var txtRr = this.FindControl<TextBox>("TxtRingRadius");
            var txtTb = this.FindControl<TextBox>("TxtTreeBranching");
            var txtBy = this.FindControl<TextBox>("TxtBusY");
            var txtDt = this.FindControl<TextBox>("TxtDt");

            var opts = new InitOptions
            {
                Width = I(txtW, 1000),
                Height = I(txtH, 700),
                Margin = D(txtM, 30),
                CountT = I(txtT, 6),
                CountS = I(txtS, 200),
                CountA = I(txtA, 20),
                CountE = I(txtE, 5),
                CountI = I(txtI, 6),
                CountQ = I(txtQ, 3),
                CountR = I(txtR, 10),
                RingRadius = D(txtRr, 0.35 * Math.Min(I(txtW, 1000), I(txtH, 700))),
                TreeBranching = I(txtTb, 3),
                BusY = D(txtBy, 0.5)
            };

            var dtVal = D(txtDt, 0.5);
            simulation.Dt = dtVal;

            CustomCsvPaths? custom = null;
            if (topology == TopologyKind.CustomCsv)
            {
                var txtCsvN = this.FindControl<TextBox>("TxtCsvNodos");
                var txtCsvE = this.FindControl<TextBox>("TxtCsvConexiones");
                var nodesPath = txtCsvN?.Text;
                var edgesPath = txtCsvE?.Text;

                if (string.IsNullOrWhiteSpace(nodesPath) || string.IsNullOrWhiteSpace(edgesPath))
                {
                    var basePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "MatLab");
                    nodesPath ??= System.IO.Path.Combine(basePath, "nodos.csv");
                    edgesPath ??= System.IO.Path.Combine(basePath, "conexiones.csv");
                }

                if (!File.Exists(nodesPath) || !File.Exists(edgesPath))
                    throw new FileNotFoundException("No se encontraron los CSV para CustomCsv.", $"{nodesPath} | {edgesPath}");

                custom = new CustomCsvPaths { NodesCsvPath = nodesPath!, EdgesCsvPath = edgesPath! };
            }

            return (topology, opts, custom);
        }

        // auto-ajuste parametros

        private void HookInputAutoAdjust()
        {
            void onChanged(object? s, EventArgs e)
            {
                if (_suspendAuto) return;
                TryRecomputeSuggestions();
            }

            // Campos relevantes
            var ids = new[]
            {
                "TxtWidth","TxtHeight","TxtMargin",
                "TxtCountS","TxtCountA","TxtCountE","TxtCountI","TxtCountQ","TxtCountR",
                "TxtCountT","TxtRingRadius","TxtTreeBranching","TxtBusY"
            };

            foreach (var id in ids)
            {
                var tb = this.FindControl<TextBox>(id);
                if (tb != null)
                {
                    // Evento genérico de cambio de propiedad
                    tb.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == TextBox.TextProperty)
                            onChanged(tb, EventArgs.Empty);
                    };
                }
            }

            if (TopologiaSelector != null)
                TopologiaSelector.SelectionChanged += (_, __) => onChanged(this, EventArgs.Empty);

            // Primera sugerencia al abrir
            TryRecomputeSuggestions();

        }

        private void TryRecomputeSuggestions()
        {
            // Lectura segura de UI
            var txtW = this.FindControl<TextBox>("TxtWidth");
            var txtH = this.FindControl<TextBox>("TxtHeight");
            var txtM = this.FindControl<TextBox>("TxtMargin");

            var txtS = this.FindControl<TextBox>("TxtCountS");
            var txtA = this.FindControl<TextBox>("TxtCountA");
            var txtE = this.FindControl<TextBox>("TxtCountE");
            var txtI = this.FindControl<TextBox>("TxtCountI");
            var txtQ = this.FindControl<TextBox>("TxtCountQ");
            var txtR = this.FindControl<TextBox>("TxtCountR");

            var txtT = this.FindControl<TextBox>("TxtCountT");
            var txtRr = this.FindControl<TextBox>("TxtRingRadius");
            var txtTb = this.FindControl<TextBox>("TxtTreeBranching");
            var txtBy = this.FindControl<TextBox>("TxtBusY");

            int W = ParseInt(txtW, 1000);
            int H = ParseInt(txtH, 700);
            double M = ParseDouble(txtM, 30);

            int devices =
                ParseInt(txtS, 200) + ParseInt(txtA, 20) + ParseInt(txtE, 5) +
                ParseInt(txtI, 6) + ParseInt(txtQ, 3) + ParseInt(txtR, 10);

            devices = Math.Max(1, devices);

            // #T sugeridos ~ devices / carga objetivo
            int suggestedT = Math.Clamp((int)Math.Round(devices / (double)TARGET_LOAD), 2, 500);

            // Radio sugerido (misma fórmula que Simulation.ComputeAssociationRadius)
            double area = Math.Max(1, (W - 2 * M) * (H - 2 * M));
            double baseR = Math.Sqrt(area / (Math.PI * Math.Max(1, suggestedT)));
            int suggestedRingRadius = (int)Math.Max(20, K_RADIUS * baseR);

            // ramificacion
            int suggestedBranching = Math.Clamp((int)Math.Round(2.0 + Math.Log10(Math.Max(3, suggestedT))), 2, 6);

            // BusY lo mantenemos si el usuario lo cambió (no lo forzamos)
            double keepBusY = ParseDouble(txtBy, 0.5);

            // Topología actual
            var topo = (TopologiaSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ring";

            // ui sin bucle
            _suspendAuto = true;

            SetTextIfChanged(txtT, suggestedT.ToString());
            if (topo is "Ring" or "Mesh" or "Tree")
                SetTextIfChanged(txtRr, suggestedRingRadius.ToString());
            if (topo == "Tree")
                SetTextIfChanged(txtTb, suggestedBranching.ToString());
            if (topo == "Bus")
                SetTextIfChanged(txtBy, keepBusY.ToString("0.##"));

            _suspendAuto = false;
        }

        private static int ParseInt(TextBox? tb, int def)
            => tb != null && int.TryParse(tb.Text, out var v) ? v : def;

        private static double ParseDouble(TextBox? tb, double def)
            => tb != null && double.TryParse(tb.Text, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out var v)
               ? v : def;

        private static void SetTextIfChanged(TextBox? tb, string value)
        {
            if (tb == null) return;
            if (!string.Equals(tb.Text, value, StringComparison.Ordinal))
                tb.Text = value;
        }

        // cache iconos

        private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _iconCache = new();
        private Avalonia.Media.Imaging.Bitmap GetIcon(string iconPath)
        {
            if (_iconCache.TryGetValue(iconPath, out var bmp))
                return bmp;
            var nb = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri(iconPath)));
            _iconCache[iconPath] = nb;
            return nb;
        }

        private void SyncVisualsWithAgents()
        {
            if (vm == null) return;
            var sim = vm.Sim;

            // Tomar agentes una sola vez
            var agents = sim.GetAgents().ToList();
            int agentCount = agents.Count;

            // Si hay muchos nodos, bajamos el detalle gráfico
            bool heavyMode = agentCount > 4000;

            // Agregar faltantes
            foreach (var ag in agents)
            {
                if (!agenteVisuales.ContainsKey(ag.Id))
                {
                    string iconPath =
                        $"avares://AgentesSAEIQRST/Assets/Icons/{GetImageFileName(ag.State, ag.Id)}";
                    var img = new Image
                    {
                        Width  = WidthImg,
                        Height = HeightImg,
                        Source = GetIcon(iconPath)
                    };

                    Canvas.SetLeft(img, ag.X);
                    Canvas.SetTop(img, ag.Y);
                    SimulationCanvas.Children.Add(img);
                    agenteVisuales[ag.Id] = img;
                }
            }

            // Remover huérfanos
            var aliveIds = agents.Select(a => a.Id).ToHashSet();
            var muertos = agenteVisuales.Keys.Where(id => !aliveIds.Contains(id)).ToList();
            foreach (var id in muertos)
            {
                if (agenteVisuales[id] is Image imgDead)
                    SimulationCanvas.Children.Remove(imgDead);
                agenteVisuales.Remove(id);
            }

            // Actualizar posición + icono + halo
            foreach (var ag in agents)
            {
                if (!agenteVisuales.TryGetValue(ag.Id, out var ctrl))
                    continue;
                if (ctrl is not Image img)
                    continue;

                double canvasW = SimulationCanvas.Bounds.Width;
                double canvasH = SimulationCanvas.Bounds.Height;
                if (canvasW <= 0) canvasW = sim.CanvasWidth;
                if (canvasH <= 0) canvasH = sim.CanvasHeight;

                double minX = 0, minY = 0;
                double maxX = canvasW - img.Width;
                double maxY = canvasH - img.Height;

                bool fueraHard =
                    ag.X < -1000 || ag.X > canvasW + 1000 ||
                    ag.Y < -1000 || ag.Y > canvasH + 1000;

                if (fueraHard)
                {
                    img.IsVisible = false;
                    if (_criticalHalos.TryGetValue(ag.Id, out var haloOut))
                    {
                        SimulationCanvas.Children.Remove(haloOut);
                        _criticalHalos.Remove(ag.Id);
                    }
                    continue;
                }

                img.IsVisible = true;

                double x = Math.Clamp(ag.X, minX, maxX);
                double y = Math.Clamp(ag.Y, minY, maxY);

                Canvas.SetLeft(img, x);
                Canvas.SetTop(img, y);

                string iconPath =
                    $"avares://AgentesSAEIQRST/Assets/Icons/{GetImageFileName(ag.State, ag.Id)}";
                var nueva = GetIcon(iconPath);
                if (!ReferenceEquals(img.Source, nueva))
                    img.Source = nueva;

                if (ag.State == Compartment.T)
                {
                    bool esCritico = _criticalTIds.Contains(ag.Id);

                    img.ZIndex = esCritico ? 1000 : 10;
                    img.Width  = esCritico ? WidthImg * 1.8 : WidthImg;
                    img.Height = esCritico ? HeightImg * 1.8 : HeightImg;

                    // En modo pesado, desactivamos halos para ahorrar
                    if (!heavyMode && esCritico)
                    {
                        if (!_criticalHalos.TryGetValue(ag.Id, out var halo))
                        {
                            halo = new Ellipse
                            {
                                Stroke = Brushes.Red,
                                StrokeThickness = 3,
                                ZIndex = 900
                            };
                            SimulationCanvas.Children.Add(halo);
                            _criticalHalos[ag.Id] = halo;
                        }

                        halo.Width  = img.Width + 10;
                        halo.Height = img.Height + 10;
                        Canvas.SetLeft(halo, x - 5);
                        Canvas.SetTop(halo, y - 5);
                    }
                    else
                    {
                        if (_criticalHalos.TryGetValue(ag.Id, out var halo))
                        {
                            SimulationCanvas.Children.Remove(halo);
                            _criticalHalos.Remove(ag.Id);
                        }
                    }
                }
                else
                {
                    img.ZIndex = 0;
                    img.Width  = WidthImg;
                    img.Height = HeightImg;

                    if (_criticalHalos.TryGetValue(ag.Id, out var halo))
                    {
                        SimulationCanvas.Children.Remove(halo);
                        _criticalHalos.Remove(ag.Id);
                    }
                }
            }

            if (!heavyMode)
                RedibujarConexionesFijas();
        }

        // metricas gwo baseline
        private int RunBaselinePeakI(
            TopologyKind topology,
            InitOptions opts,
            CustomCsvPaths? custom,
            int maxSteps)
        {
            var simBase = new Simulation(enableGraphics: false)
            {
                Dt = simulation.Dt,       // mismo dt que la UI
                IsWellMixed = false       // red real
            };

            simBase.Initialize(topology, opts, custom);

            for (int s = 0; s < maxSteps; s++)
                simBase.Step();

            // pico de infectados
            return simBase.Historial.Max(h => h.I);
        }

        // guardar metricas gwo
        private void SaveGwoMetricsCsv(GwoExperimentMetrics m)
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "salidas");
            Directory.CreateDirectory(dir);

            var path = System.IO.Path.Combine(dir, "gwo_metrics.csv");
            bool exists = File.Exists(path);

            using var sw = new StreamWriter(path, append: true);

            if (!exists)
            {
                sw.WriteLine("DateTime,Topology,BaselinePeakI,GwoPeakI,ReductionPercent,SelectedTNodes");
            }

            string selected = string.Join(" ", m.SelectedTNodes);
            sw.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                $"{m.Topology}," +
                $"{m.BaselinePeakI}," +
                $"{m.GwoPeakI}," +
                $"{m.ReductionPercent:F2}," +
                $"\"{selected}\"");
        }
    }
}