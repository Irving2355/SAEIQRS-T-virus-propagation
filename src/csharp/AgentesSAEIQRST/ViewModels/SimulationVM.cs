using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AgentesSAEIQRST.Core;

namespace AgentesSAEIQRST.ViewModels
{
    public class SimulationVM : INotifyPropertyChanged
    {
        // flags ui/headless
        private bool _useGraphics = false; // sin vista = headless
        public bool UseGraphics
        {
            get => _useGraphics;
            set
            {
                if (_useGraphics == value) return;
                _useGraphics = value;
                OnPropertyChanged();
                RecreateSimulationKeepingDt();  // recrea sim
            }
        }

        // modelo
        public Simulation Sim { get; private set; }

        // inputs (bindables)
        private int _width = 900, _height = 800;
        private double _margin = 20;
        private int _countT = 333, _countS = 9100, _countA = 100, _countE = 250, _countI = 350, _countQ = 150, _countR = 50;
        private double _ringRadius = 260;
        private int _treeBranching = 3;
        private double _busY = 0.5;
        private double _dt = 1.0;
        private TopologyKind _topology = TopologyKind.Ring;

        // CSV (solo para CustomCsv)
        private string _nodesCsv = "", _edgesCsv = "";

        // Loop de ejecución con UI
        private CancellationTokenSource? _cts;

        public SimulationVM()
        {
            Sim = new Simulation(enableGraphics: _useGraphics);
            Sim.Dt = _dt;

            Sim.InvalidateRequested += () =>
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Sim)));
        }

        // inotifypropertychanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // propiedades bindables
        public int Width { get => _width; set { _width = value; OnPropertyChanged(); } }
        public int Height { get => _height; set { _height = value; OnPropertyChanged(); } }
        public double Margin { get => _margin; set { _margin = value; OnPropertyChanged(); } }

        public int CountT { get => _countT; set { _countT = value; OnPropertyChanged(); } }
        public int CountS { get => _countS; set { _countS = value; OnPropertyChanged(); } }
        public int CountA { get => _countA; set { _countA = value; OnPropertyChanged(); } }
        public int CountE { get => _countE; set { _countE = value; OnPropertyChanged(); } }
        public int CountI { get => _countI; set { _countI = value; OnPropertyChanged(); } }
        public int CountQ { get => _countQ; set { _countQ = value; OnPropertyChanged(); } }
        public int CountR { get => _countR; set { _countR = value; OnPropertyChanged(); } }

        public double RingRadius { get => _ringRadius; set { _ringRadius = value; OnPropertyChanged(); } }
        public int TreeBranching { get => _treeBranching; set { _treeBranching = value; OnPropertyChanged(); } }
        public double BusY { get => _busY; set { _busY = value; OnPropertyChanged(); } }

        public double Dt
        {
            get => _dt;
            set { _dt = value; Sim.Dt = value; OnPropertyChanged(); }
        }

        public TopologyKind Topology { get => _topology; set { _topology = value; OnPropertyChanged(); } }

        public string NodesCsv { get => _nodesCsv; set { _nodesCsv = value; OnPropertyChanged(); } }
        public string EdgesCsv { get => _edgesCsv; set { _edgesCsv = value; OnPropertyChanged(); } }

        public bool IsRunning => Sim.IsRunning;

        // helpers
        private (InitOptions opts, CustomCsvPaths? custom) BuildInit()
        {
            var opts = new InitOptions
            {
                Width = Width, Height = Height, Margin = Margin,
                CountT = CountT, CountS = CountS, CountA = CountA, CountE = CountE,
                CountI = CountI, CountQ = CountQ, CountR = CountR,
                RingRadius = RingRadius, TreeBranching = TreeBranching, BusY = BusY
            };

            CustomCsvPaths? custom = null;
            if (Topology == TopologyKind.CustomCsv)
            {
                string nodes = NodesCsv;
                string edges = EdgesCsv;
                
                // rutas por defecto si están vacías
                if (string.IsNullOrWhiteSpace(nodes))
                    nodes = "Assets/Bus/nodos.csv";
                if (string.IsNullOrWhiteSpace(edges))
                    edges = "Assets/Bus/conexiones.csv";
                    
                custom = new CustomCsvPaths { NodesCsvPath = nodes, EdgesCsvPath = edges };
            }

            return (opts, custom);
        }

        /// <summary>
        /// Recrea la Simulation (por cambio de UseGraphics), preservando Dt.
        /// </summary>
        public void RecreateSimulationKeepingDt()
        {
            var dtTmp = Sim?.Dt ?? _dt;
            Sim = new Simulation(enableGraphics: _useGraphics);
            Sim.Dt = dtTmp;
            Sim.InvalidateRequested += () =>
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Sim)));
            OnPropertyChanged(nameof(Sim));
        }

        // comandos
        public void Initialize()
        {
            var init = BuildInit();
            Sim.Initialize(Topology, init.opts, init.custom);
            OnPropertyChanged(nameof(Sim));
        }

        public void Step() => Sim.Step();

        /// <summary>
        /// Ejecuta N pasos en modo headless y guarda CSV (streaming o al final).
        /// Si la Sim no está inicializada, la inicializa aquí.
        /// </summary>
        public async Task RunHeadlessAndSaveCsvAsync(
            int steps,
            string csvPath,
            bool streamCsv = true,
            int flushEveryNSteps = 1500,
            CancellationToken ct = default)
        {
            // graficos
            if (_useGraphics)
            {
                var dtTmp = Sim.Dt;
                Sim = new Simulation(enableGraphics: false);
                Sim.Dt = dtTmp;
                Sim.InvalidateRequested += () =>
                    Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Sim)));
                OnPropertyChanged(nameof(Sim));
            }

            // re-init
            if (!(Sim.GetAgents()?.Any() ?? false))
            {
                var init = BuildInit();
                Sim.Initialize(Topology, init.opts, init.custom);
            }

            // salida
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() =>
                Sim.RunHeadless(steps, csvPath, streamCsv, flushEveryNSteps, ct), ct);
        }

        /// <summary>
        /// Exporta el historial acumulado al CSV indicado (cuando no usas streaming).
        /// </summary>
        public void ExportCsv(string csvPath)
        {
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Sim.WriteHistoryCsv(csvPath);
        }

        /// <summary>
        /// Alterna ejecución. En headless corre y guarda CSV; en UI corre en bucle con repaints.
        /// </summary>
        public async void RunOrStop()
        {
            if (!_useGraphics)
            {
                // headless + replay
                // arranca replay automatico desde el csv
                await RunHeadlessThenReplayAsync(
                    steps: 1500,
                    csvPath: "salidas/resultado_agentes.csv",
                    replayMs: 200,
                    seedDevices: 123
                );
                return;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
                Sim.Stop();
                OnPropertyChanged(nameof(IsRunning));
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Sim.Start();
            OnPropertyChanged(nameof(IsRunning));

            _ = Task.Run(async () =>
            {
                var sw = new System.Diagnostics.Stopwatch();
                const double targetHz = 60.0;
                double dtTarget = 1.0 / targetHz;

                while (!token.IsCancellationRequested && Sim.IsRunning)
                {
                    sw.Restart();

                    for (int i = 0; i < 3; i++) Sim.Step();

                    Dispatcher.UIThread.Post(() => Sim.RequestInvalidate());

                    var elapsed = sw.Elapsed.TotalSeconds;
                    if (elapsed < dtTarget)
                        await Task.Delay(TimeSpan.FromSeconds(dtTarget - elapsed), token);
                }
            }, token);
        }

        public async Task RunHeadlessThenReplayAsync(
            int steps,
            string csvPath,
            int replayMs = 200,
            int seedDevices = 123,
            CancellationToken ct = default)
        {
            // arranca el headless con replay desde csv

            try
            {
                var init = BuildInit();

                // 1) Simulation para todo el proceso (ya en modo gráfico)
                var sim = new Simulation(enableGraphics: true);
                sim.Dt = Sim.Dt;

                sim.InvalidateRequested += () =>
                    Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Sim)));

                // Topología para el replay (no usar CustomCsv directo)
                var topologyForReplay = (Topology == TopologyKind.CustomCsv)
                    ? TopologyKind.Ring
                    : Topology;

                // topologia elegida para el replay

                // Inicializa SOLO para tener la misma configuración geométrica de T
                sim.Initialize(topologyForReplay, init.opts,
                    (Topology == TopologyKind.CustomCsv) ? null : init.custom);

                // sim lista pa headless

                // Esta instancia será la que vea la UI
                Sim = sim;
                OnPropertyChanged(nameof(Sim));

                // Opciones de replay
                var replay = new ReplayOptions
                {
                    AutoPlay = true,
                    StepDurationMs = replayMs,
                    SeedDevices = seedDevices,
                    WarnOnTCountMismatch = true
                };

                // 2) Correr SOLO el headless en segundo plano y generar el CSV
                await Task.Run(() =>
                {
                    // arranca el headless
                    var res = sim.RunHeadless(
                        maxSteps: steps,
                        csvPath: csvPath,
                        streamCsv: true,
                        flushEveryNSteps: steps,
                        ct: ct
                    );
                    // headless termino
                }, ct);

                // 2.5) Verificar CSV
                bool exists = File.Exists(csvPath);
                if (!exists)
                {
                    Console.WriteLine("error: no existe el csv");
                    return;
                }

                int lineCount = File.ReadLines(csvPath).Count();

                // arranca el replay desde el csv
                sim.StartReplayFromCsv(
                    csvPath,
                    topologyForReplay,
                    init.opts,
                    replay
                );
                // replay iniciado ok

                // 4) Dejar el flag en modo gráfico (sin recrear Sim)
                _useGraphics = true;
                OnPropertyChanged(nameof(UseGraphics));

                // termino todo bien
            }
            catch (Exception ex)
            {
                Console.WriteLine("error en el proceso:");
                Console.WriteLine(ex.ToString());
            }
        }


    }
}