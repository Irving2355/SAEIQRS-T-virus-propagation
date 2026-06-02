using AgentesSAEIQRST.Core.Compartments;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AgentesSAEIQRST.Core
{
    public enum TopologyKind { Bus, Star, Ring, Mesh, Tree, CustomCsv }

    public class InitOptions
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public double Margin { get; set; } = 20.0;
        public int CountS { get; set; } = 100;
        public int CountA { get; set; } = 10;
        public int CountE { get; set; } = 5;
        public int CountI { get; set; } = 4;
        public int CountQ { get; set; } = 3;
        public int CountR { get; set; } = 8;
        public int CountT { get; set; } = 5;

        // geometria
        public double RingRadius { get; set; } = 220;
        public int TreeBranching { get; set; } = 3;
        public double BusY { get; set; } = 0.5;
    }

    public class CustomCsvPaths
    {
        public string NodesCsvPath { get; set; } = "";
        public string EdgesCsvPath { get; set; } = "";
    }

    public class Simulation
    {
        public event Action<int>? StepReported;
        // replay
        private bool _isReplay = false;
        private List<FrameCounts> _replayFrames = new();
        private int _replayIndex = 0;
        private System.Threading.CancellationTokenSource? _replayCts = null;

        // pool de dispositivos
        private readonly List<Agent> _replayPool = new();
        private readonly List<(double x, double y)> _replayPoolPos = new();

        // esconder nodos 
        private const double HIDDEN_X = -10000.0;
        private const double HIDDEN_Y = -10000.0;

        // metricas
        public int ReplayTotalFrames => _replayFrames.Count;
        public int ReplayCurrentFrame => _replayIndex + 1;

        // fin replay

        private readonly bool _enableGraphics;

        private readonly HashSet<int> _blockedComponents = new();

        public Simulation(bool enableGraphics = true)
        {
            _enableGraphics = enableGraphics;

            // setup threadpool
            ThreadPool.GetMinThreads(out var minWorkers, out var minIO);
            ThreadPool.SetMinThreads(Math.Max(minWorkers, Environment.ProcessorCount), minIO);
        }

        public bool IsWellMixed
        {
            get => WellMixed;
            set => WellMixed = value;
        }
        private readonly Stopwatch _uiSw = Stopwatch.StartNew();
        private const int UiMs = 100;

        private Agent[] _agentsCache = Array.Empty<Agent>();
        private bool _agentsDirty = true;
    
        public static Simulation? Current { get; private set; }
        private readonly List<Agent> agents = new();
        private readonly List<(Agent origen, Agent destino)> conexionesFijas = new();

        // topologia tt 
        private readonly List<(Transmission o, Transmission d)> topologyEdgesTT = new();

        private readonly Random rand = new(); 
        private double dt = 1.0;

        public int CanvasWidth { get; private set; } = 800;
        public int CanvasHeight { get; private set; } = 600;

        public event Action? InvalidateRequested;
        public void RequestInvalidate()
        {
            InvalidateRequested?.Invoke();
        }
        public List<(int S, int A, int E, int I, int Q, int R, int T)> Historial { get; } = new();
        private readonly List<(Agent oldAgent, Agent? newAgent)> pendingChanges = new();
        private readonly HashSet<Guid> changesSet = new();

        // cache de T y componentes
        private List<Transmission> tNodesCache = new();
        private Dictionary<Transmission, int> tCompId = new();

        public IEnumerable<Agent> GetAgents() => agents;
        public IEnumerable<(Agent origen, Agent destino)> GetConexionesFijas() => conexionesFijas;

        // propiedades
        public IEnumerable<Agent> Agents => GetAgents();
        public IEnumerable<(Agent origen, Agent destino)> Edges => GetConexionesFijas();
        public double Dt { get => dt; set => dt = Math.Max(0.001, value); }

        public void SetInterventionMaskOverT(bool[]? maskOverT)
        {
            _blockedComponents.Clear();

            if (maskOverT == null || tNodesCache.Count == 0)
                return;

            int n = Math.Min(maskOverT.Length, tNodesCache.Count);
            for (int i = 0; i < n; i++)
            {
                if (!maskOverT[i]) continue;

                var t = tNodesCache[i];
                if (tCompId.TryGetValue(t, out int cid))
                    _blockedComponents.Add(cid); // bloquea comp
            }
        }


        // api principal
        public void Initialize(TopologyKind kind, InitOptions opts, CustomCsvPaths? custom = null)
        {
            agents.Clear();
            conexionesFijas.Clear();
            topologyEdgesTT.Clear();
            tNodesCache.Clear();
            tCompId.Clear();
            Historial.Clear();

            Current = this;

            Agent.FrozenPositions = true;

            CanvasWidth = opts.Width;
            CanvasHeight = opts.Height;

            // topologia t
            switch (kind)
            {
                case TopologyKind.Bus: GenerateBusT(opts); break;
                case TopologyKind.Star: GenerateStarT(opts); break;
                case TopologyKind.Ring: GenerateRingT(opts); break;
                case TopologyKind.Mesh: GenerateMeshT(opts); break;
                case TopologyKind.Tree: GenerateTreeT(opts); break;
                case TopologyKind.CustomCsv:
                    if (custom == null) throw new Exception("CustomCsvPaths requerido.");
                    LoadTopologyTFromCsv(custom.NodesCsvPath, custom.EdgesCsvPath);
                    break;
            }

            // crear pcs
            GenerateNonTUniform(opts);

            // construir grafo
            RebuildEdgesTopologyAndNearest();

            // precalc comps
            PrecomputeTComponents();

            AssignCompIdToDevices();

            if (_enableGraphics)
                InvalidateRequested?.Invoke();
            _agentsCache = agents.ToArray();
            _agentsDirty = false;
        }

        private void AssignCompIdToDevices()
        {
            // asigna compid
            foreach (var dev in agents)
            {
                if (dev is Transmission)
                {
                    dev.CompId = -1; // ignorar
                    continue;
                }

                var t = GetTNeighbor(dev);
                if (t != null && tCompId.TryGetValue(t, out int cid))
                    dev.CompId = cid;
                else
                    dev.CompId = -1;
            }
        }

        // csv (solo T)
        private void LoadTopologyTFromCsv(string rutaNodos, string rutaConexiones)
        {
            // validar rutas
            if (string.IsNullOrWhiteSpace(rutaNodos) || string.IsNullOrWhiteSpace(rutaConexiones))
                throw new Exception("rutas csv no especificadas");
            if (!File.Exists(rutaNodos))
                throw new Exception($"nodos csv no existe: {rutaNodos}");
            if (!File.Exists(rutaConexiones))
                throw new Exception($"conexiones csv no existe: {rutaConexiones}");

            var idToT = new Dictionary<string, Transmission>();

            foreach (var linea in File.ReadAllLines(rutaNodos).Skip(1))
            {
                var p = linea.Split(',');
                string id = p[0];
                string tipo = p[1].Trim();
                double x = double.Parse(p[2]);
                double y = double.Parse(p[3]);
                if (tipo != "T") continue;

                var t = new Transmission(x, y, radius: 90);
                agents.Add(t);
                idToT[id] = t;
            }

            var invalidConnections = new List<string>();

            foreach (var linea in File.ReadAllLines(rutaConexiones).Skip(1))
            {
                var p = linea.Split(',');
                string id1 = p[0].Trim();
                string id2 = p[1].Trim();
                
                if (idToT.TryGetValue(id1, out var o) && idToT.TryGetValue(id2, out var d))
                {
                    AddTopologyEdge(o, d);
                }
                else
                {
                    // conexion q no existe, la guardo
                    invalidConnections.Add(linea);
                }
            }

            if (invalidConnections.Count > 0)
            {
                // aviso si hay conexiones rotas
                foreach (var conn in invalidConnections)
                    Console.WriteLine($"Conexion invalida: {conn}");
            }
        }

        // helpers topologia
        private void AddTopologyEdge(Transmission a, Transmission b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return;
            foreach (var e in topologyEdgesTT)
                if ((ReferenceEquals(e.o, a) && ReferenceEquals(e.d, b)) ||
                    (ReferenceEquals(e.o, b) && ReferenceEquals(e.d, a)))
                    return;
            topologyEdgesTT.Add((a, b));
        }

        // solo t y pc->t, nunca pc-pc
        private void AddEdge(Agent a, Agent b)
        {
            if (a.State != Compartment.T && b.State != Compartment.T) return;

            // un pc, una t
            if (a.State != Compartment.T && b.State == Compartment.T)
                if (a.Neighbors.Any(n => n.State == Compartment.T)) return;

            if (b.State != Compartment.T && a.State == Compartment.T)
                if (b.Neighbors.Any(n => n.State == Compartment.T)) return;

            // sin duplicar
            for (int i = 0; i < conexionesFijas.Count; i++)
            {
                var e = conexionesFijas[i];
                if ((ReferenceEquals(e.origen, a) && ReferenceEquals(e.destino, b)) ||
                    (ReferenceEquals(e.origen, b) && ReferenceEquals(e.destino, a)))
                    return;
            }

            a.AddNeighbor(b);
            b.AddNeighbor(a);
            conexionesFijas.Add((a, b));
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // generadores topologias
        private void GenerateBusT(InitOptions opts)
        {
            int n = Math.Max(opts.CountT, 2);
            double y = opts.Height * opts.BusY;
            Transmission? prev = null;
            for (int i = 0; i < n; i++)
            {
                double x = Lerp(opts.Margin, opts.Width - opts.Margin, i / (double)(n - 1));
                var t = new Transmission(x, y, radius: 90);
                agents.Add(t);
                if (prev != null) AddTopologyEdge(prev, t);
                prev = t;
            }
        }

        private void GenerateStarT(InitOptions opts)
        {
            int n = Math.Max(opts.CountT, 2);
            double cx = opts.Width / 2.0, cy = opts.Height / 2.0;
            var center = new Transmission(cx, cy, radius: 110);
            agents.Add(center);

            for (int i = 1; i < n; i++)
            {
                double ang = 2 * Math.PI * (i - 1) / (n - 1);
                double r = Math.Min(opts.Width, opts.Height) * 0.35;
                var t = new Transmission(cx + r * Math.Cos(ang), cy + r * Math.Sin(ang), radius: 90);
                agents.Add(t);
                AddTopologyEdge(center, t);
            }
        }

        private void GenerateRingT(InitOptions opts)
        {
            int n = Math.Max(opts.CountT, 3);
            double cx = opts.Width / 2.0, cy = opts.Height / 2.0, R = opts.RingRadius;
            Transmission? first = null, prev = null;
            for (int i = 0; i < n; i++)
            {
                double ang = 2 * Math.PI * i / n;
                var t = new Transmission(cx + R * Math.Cos(ang), cy + R * Math.Sin(ang), radius: 90);
                agents.Add(t);
                if (i == 0) first = t;
                if (prev != null) AddTopologyEdge(prev, t);
                prev = t;
            }
            if (prev != null && first != null) AddTopologyEdge(prev, first);
        }

        private void GenerateMeshT(InitOptions opts)
        {
            int n = Math.Max(opts.CountT, 2);
            var ts = new List<Transmission>();
            for (int i = 0; i < n; i++)
            {
                var t = new Transmission(
                    rand.NextDouble() * (opts.Width - 2 * opts.Margin) + opts.Margin,
                    rand.NextDouble() * (opts.Height - 2 * opts.Margin) + opts.Margin,
                    radius: 90
                );
                agents.Add(t);
                ts.Add(t);
            }
            for (int i = 0; i < ts.Count; i++)
                for (int j = i + 1; j < ts.Count; j++)
                    AddTopologyEdge(ts[i], ts[j]);
        }

        private void GenerateTreeT(InitOptions opts)
        {
            int n = Math.Max(opts.CountT, 2);
            int b = Math.Max(opts.TreeBranching, 2);

            var ts = new List<Transmission>();
            for (int i = 0; i < n; i++)
            {
                double level = Math.Floor(Math.Log(i + 1, b));
                double y = Lerp(opts.Margin, opts.Height - opts.Margin,
                                level / Math.Max(1, Math.Ceiling(Math.Log(n, b))));
                int start = (int)Math.Pow(b, level) - 1;
                int idxInLevel = i - start;
                int nodesThisLevel = (int)Math.Pow(b, level);
                double x = Lerp(opts.Margin, opts.Width - opts.Margin,
                                nodesThisLevel <= 1 ? 0.5 : idxInLevel / (double)(nodesThisLevel - 1));

                var t = new Transmission(x, y, radius: 90);
                agents.Add(t);
                ts.Add(t);

                if (i > 0)
                {
                    int parentIndex = (i - 1) / b;
                    AddTopologyEdge(ts[parentIndex], t);
                }
            }
        }

        // pcs uniformes
        private void GenerateNonTUniform(InitOptions opts)
        {
            (double x, double y) RandomPoint()
            {
                double x = rand.NextDouble() * (opts.Width - 2 * opts.Margin) + opts.Margin;
                double y = rand.NextDouble() * (opts.Height - 2 * opts.Margin) + opts.Margin;
                return (x, y);
            }

            void Create(Func<double, double, Agent> factory)
            {
                var (x, y) = RandomPoint();
                var a = factory(x, y);
                agents.Add(a);
            }

            for (int i = 0; i < opts.CountS; i++) Create((x, y) => new Susceptible(x, y));
            for (int i = 0; i < opts.CountA; i++) Create((x, y) => new Antidote(x, y));
            for (int i = 0; i < opts.CountE; i++) Create((x, y) => new Exposed(x, y));
            for (int i = 0; i < opts.CountI; i++) Create((x, y) => new Infected(x, y));
            for (int i = 0; i < opts.CountQ; i++) Create((x, y) => new Quarantine(x, y));
            for (int i = 0; i < opts.CountR; i++) Create((x, y) => new Recovered(x, y));
        }

        private const int UpdateChunk = 8192;
        private const int InfectChunk = 8192;

        private static int SamplePoisson(double lambda, Random r)
        {
            if (lambda <= 0) return 0;
            int k = 0;
            double L = Math.Exp(-lambda);
            double p = 1.0;
            do { k++; p *= r.NextDouble(); } while (p > L);
            return k - 1;
        }

        // ciclo principal
        public void Step()
        {
            int stepNum = Historial.Count + 1;
            if (stepNum % 100 == 0)
            {
                // cada x pasos aviso
            } 

            if (running)
                StepReported?.Invoke(stepNum);

            // nacimientos
            int N = agents.Count(a => a is not Transmission);
            double cap = 1.0;
            if (ParametrosSimulacion.Nmax > 0)
                cap = Math.Max(0.0, 1.0 - (double)N / ParametrosSimulacion.Nmax);

            double Beff = ParametrosSimulacion.B * cap;       // B absoluto (como en la ODE)
            int births = SamplePoisson(Beff * dt, rand);
            if (births > 0)
            {
                var nuevos = new List<Susceptible>(births);
                for (int b = 0; b < births; b++)
                {
                    double x = rand.NextDouble() * (CanvasWidth - 40) + 20;
                    double y = rand.NextDouble() * (CanvasHeight - 40) + 20;
                    nuevos.Add(new Susceptible(x, y));
                }

                lock (_changeLock)
                {
                    agents.AddRange(nuevos);

                    if (tNodesCache.Count > 0)
                    {
                        var tArr = tNodesCache; // IList<Transmission>
                        foreach (var s in nuevos)
                        {
                            var t = FindNearestT(s, tArr);
                            AddEdge(s, t); // crea arista PC–T (no duplica)

                            // *** clave: asignar el componente una vez conectado ***
                            if (tCompId.TryGetValue(t, out int cid)) s.CompId = cid;
                            else s.CompId = -1;
                        }
                    }
                    else
                    {
                        // sin componente
                        foreach (var s in nuevos) s.CompId = -1;
                    }

                    _agentsDirty = true;
                }
            }

            // refrescar cache si cambio
            if (_agentsDirty)
            {
                _agentsCache = agents.ToArray();
                _agentsDirty = false;
            }
            var agentsArr = _agentsCache;

            // update paralelo
            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - (_enableGraphics ? 1 : 0))
            };
            var partUpdate = Partitioner.Create(0, agentsArr.Length, UpdateChunk);

            Parallel.ForEach(partUpdate, po, range =>
            {
                var (start, end) = range;
                for (int i = start; i < end; i++)
                    agentsArr[i].Update(dt, Array.Empty<Agent>());
            });

            // contagio por componentes
            if (WellMixed) ApplyInfections_WellMixed(dt, agentsArr);
            else
                ApplyInfectionsThroughT_Fast_Parallel(dt, agentsArr);

            // aplicar cambios pendientes
            ApplyPendingChanges();

            // si cambio, refrescar
            if (_agentsDirty)
            {
                _agentsCache = agents.ToArray();
                _agentsDirty = false;
            }

            // conteo y redibuja
            CountAndStore();

            if (_enableGraphics && _uiSw.ElapsedMilliseconds >= UiMs)
            {
                InvalidateRequested?.Invoke();
                _uiSw.Restart();
            }
        }

        public void WriteHistoryCsv(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var sw = new StreamWriter(path, false);
            sw.WriteLine("step,S,A,E,I,Q,R,T");
            for (int k = 0; k < Historial.Count; k++)
            {
                var h = Historial[k];
                sw.WriteLine($"{k+1},{h.S},{h.A},{h.E},{h.I},{h.Q},{h.R},{h.T}");
            }
        }

        private void ApplyPendingChanges()
        {
            bool anyChange = false;
            foreach (var (oldAgent, newAgent) in pendingChanges)
            {
                anyChange = true;

                if (oldAgent != null)
                {
                    if (newAgent != null)
                    {
                        RewireEdgesOnReplace(oldAgent, newAgent);
                        agents.Remove(oldAgent);
                        agents.Add(newAgent);
                    }
                    else
                    {
                        RemoveEdgesFor(oldAgent);
                        agents.Remove(oldAgent);
                    }
                }
            }

            pendingChanges.Clear();
            changesSet.Clear();

            if(anyChange) _agentsDirty = true;
        }

        private void RewireEdgesOnReplace(Agent oldA, Agent newA)
        {
            for (int i = 0; i < conexionesFijas.Count; i++)
            {
                var (o, d) = conexionesFijas[i];
                if (ReferenceEquals(o, oldA)) o = newA;
                if (ReferenceEquals(d, oldA)) d = newA;

                if (o.State != Compartment.T && d.State != Compartment.T)
                {
                    conexionesFijas.RemoveAt(i);
                    i--;
                    continue;
                }

                conexionesFijas[i] = (o, d);
            }

            var oldNeighbors = oldA.Neighbors.ToArray();
            foreach (var nb in oldNeighbors)
            {
                nb.Neighbors.Remove(oldA);
                if (newA.State == Compartment.T || nb.State == Compartment.T)
                {
                    if (!nb.Neighbors.Contains(newA)) nb.AddNeighbor(newA);
                    if (!newA.Neighbors.Contains(nb)) newA.AddNeighbor(nb);
                }
            }
        }

        private void RemoveEdgesFor(Agent a)
        {
            conexionesFijas.RemoveAll(e => ReferenceEquals(e.origen, a) || ReferenceEquals(e.destino, a));
            var nbs = a.Neighbors.ToArray();
            foreach (var nb in nbs)
                nb.Neighbors.Remove(a);
        }

        // reconstruir
        private void RebuildEdgesTopologyAndNearest()
        {
            var tNodes = agents.OfType<Transmission>().ToList();
            var devices = agents.Where(a => a is not Transmission).ToList();

            // limpiar grafo
            conexionesFijas.Clear();
            foreach (var a in agents) a.Neighbors.Clear();

            // repone tt
            foreach (var (o, d) in topologyEdgesTT)
                AddEdge(o, d);

            if (tNodes.Count == 0) { DebugPrintEdgeStats(); return; }

            // cada dispositivo al T más cercano
            foreach (var dev in devices)
                ConnectDeviceToNearestT(dev, tNodes);

            // refrescar caches
            tNodesCache = tNodes;
            // se recalcula en init

            DebugPrintEdgeStats();
        }

        private void EnsureNearestTForAllDevices_Parallel()
        {
            if (tNodesCache.Count == 0) return;

            var tArr = tNodesCache.ToArray();
            var devices = agents.Where(a => a is not Transmission).ToArray();

            var toConnect = new ConcurrentBag<(Agent dev, Transmission t)>();

            Parallel.For(0, devices.Length, i =>
            {
                var dev = devices[i];
                // ya tiene su T actual
                var nearest = FindNearestT(dev, tArr);
                bool hasThisT = dev.Neighbors.Any(n => ReferenceEquals(n, nearest));
                bool hasAnyT = dev.Neighbors.Any(n => n is Transmission);
                if (!hasThisT) toConnect.Add((dev, nearest));
            });

            // aplica conexiones
            foreach (var (dev, near) in toConnect)
            {
                // elimina T previos
                RemoveAllTNeighborsOf(dev);
                AddEdge(dev, near);
            }

        }
        
        private Transmission FindNearestT(Agent dev, IList<Transmission> tNodes)
        {
            Transmission nearest = tNodes[0];
            double best = Dist2(dev, nearest);
            for (int i = 1; i < tNodes.Count; i++)
            {
                double d2 = Dist2(dev, tNodes[i]);
                if (d2 < best) { best = d2; nearest = tNodes[i]; }
            }
            return nearest;
        }

        private static double Dist2(Agent a, Agent b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }


        private void ConnectDeviceToNearestT(Agent dev, IList<Transmission> tNodes)
        {
            var nearest = FindNearestT(dev, tNodes);
            RemoveAllTNeighborsOf(dev);
            AddEdge(dev, nearest);
        }

        private void RemoveAllTNeighborsOf(Agent dev)
        {
            for (int j = dev.Neighbors.Count - 1; j >= 0; j--)
            {
                var nb = dev.Neighbors[j];
                if (nb is Transmission t)
                {
                    dev.Neighbors.RemoveAt(j);
                    t.Neighbors.Remove(dev);
                    for (int k = conexionesFijas.Count - 1; k >= 0; k--)
                    {
                        var (o, d) = conexionesFijas[k];
                        if ((ReferenceEquals(o, dev) && ReferenceEquals(d, t)) ||
                            (ReferenceEquals(o, t) && ReferenceEquals(d, dev)))
                        {
                            conexionesFijas.RemoveAt(k);
                        }
                    }
                }
            }
        }

        // conteo / contagio
        private void CountAndStore()
        {
            int S = 0, A = 0, E = 0, I = 0, Q = 0, R = 0, T = 0;

            // recorre una sola vez el cache
            var arr = _agentsCache;
            for (int i = 0; i < arr.Length; i++)
            {
                var a = arr[i];
                if (a is Transmission) { T++; continue; }
                switch (a.State)
                {
                    case Compartment.S: S++; break;
                    case Compartment.A: A++; break;
                    case Compartment.E: E++; break;
                    case Compartment.I: I++; break;
                    case Compartment.Q: Q++; break;
                    case Compartment.R: R++; break;
                }
            }
            Historial.Add((S, A, E, I, Q, R, T));
        }

        // precalculo dsu de componentes
        private void PrecomputeTComponents()
        {
            tNodesCache = agents.OfType<Transmission>().ToList();
            tCompId.Clear();
            if (tNodesCache.Count == 0) return;

            int n = tNodesCache.Count;
            var parent = Enumerable.Range(0, n).ToArray();

            int Find(int x)
            {
                while (x != parent[x]) x = parent[x] = parent[parent[x]];
                return x;
            }
            void Union(int a, int b)
            {
                a = Find(a); b = Find(b);
                if (a != b) parent[b] = a;
            }

            var idx = new Dictionary<Transmission, int>(n);
            for (int i = 0; i < n; i++) idx[tNodesCache[i]] = i;

            foreach (var (o, d) in topologyEdgesTT) Union(idx[o], idx[d]);

            for (int i = 0; i < n; i++) tCompId[tNodesCache[i]] = Find(i);
        }

        // version rapida
        private int[]? _IcountBuffer;
        private readonly ConcurrentBag<int[]> _localsBag = new();

        private void ApplyInfectionsThroughT_Fast_Parallel(double dt, Agent[] agentsArr)
        {
            if (tNodesCache.Count == 0) return;

            int maxCid = -1;
            for (int i = 0; i < agentsArr.Length; i++)
            {
                int cid = agentsArr[i].CompId;
                if (cid > maxCid) maxCid = cid;
            }
            if (maxCid < 0) return;

            if (_IcountBuffer == null || _IcountBuffer.Length < (maxCid + 1))
                _IcountBuffer = new int[maxCid + 1];
            else
                Array.Clear(_IcountBuffer, 0, maxCid + 1);

            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - (_enableGraphics ? 1 : 0))
            };

            // acumuladores locales
            var locals = new List<int[]>(capacity: Environment.ProcessorCount * 2);

            Parallel.ForEach(
                Partitioner.Create(0, agentsArr.Length, InfectChunk),
                po,
                () => new int[maxCid + 1],
                (range, state, local) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var ag = agentsArr[i];
                        int cid = ag.CompId;
                        if (cid >= 0 && ag is Infected) local[cid]++;
                    }
                    return local;
                },
                local =>
                {
                    lock (locals) locals.Add(local); // una entrada por partición
                }
            );

            // suma los acumuladores
            foreach (var local in locals)
                for (int c = 0; c <= maxCid; c++)
                    _IcountBuffer[c] += local[c];

            // aplica infecciones
            Parallel.ForEach(
                Partitioner.Create(0, agentsArr.Length, InfectChunk),
                po,
                range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var ag = agentsArr[i];
                        int cid = ag.CompId;
                        if (cid < 0) continue;

                        int Icomp = _IcountBuffer![cid];
                        if (Icomp <= 0) continue;

                        if (ag is Susceptible s)
                        {
                            // si bloqueado por intervención, no hay contagio
                            if (_blockedComponents.Contains(cid)) continue;

                            double p = 1.0 - Math.Exp(-ParametrosSimulacion.beta * Icomp * dt);
                            if (Random.Shared.NextDouble() < p)
                                Simulation.RequestStateChange(s, new Exposed(s.X, s.Y));
                        }
                        else if (ag is Antidote a)
                        {
                            if (_blockedComponents.Contains(cid)) continue;

                            double p = 1.0 - Math.Exp(-ParametrosSimulacion.phi1 * Icomp * dt);
                            if (Random.Shared.NextDouble() < p)
                                Simulation.RequestStateChange(a, new Exposed(a.X, a.Y));
                        }
                    }
                });
        }

        public IReadOnlyList<Transmission> GetTNodes()
            => tNodesCache;

        bool WellMixed = true; // para comparar con ODE

        private void ApplyInfections_WellMixed(double dt, Agent[] arr)
        {
            int I = 0;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] is Infected) I++;

            if (I == 0) return;

            // formula
            double pS = 1.0 - Math.Exp(-ParametrosSimulacion.beta * I * dt);
            double pA = 1.0 - Math.Exp(-ParametrosSimulacion.phi1 * I * dt);

            for (int i = 0; i < arr.Length; i++)
            {
                var ag = arr[i];
                if (ag is Susceptible s)
                {
                    if (Random.Shared.NextDouble() < pS)
                        Simulation.RequestStateChange(s, new Exposed(s.X, s.Y));
                }
                else if (ag is Antidote a)
                {
                    if (Random.Shared.NextDouble() < pA)
                        Simulation.RequestStateChange(a, new Exposed(a.X, a.Y));
                }
            }
        }

        // vecino t
        private Transmission? GetTNeighbor(Agent dev)
        {
            var nbs = dev.Neighbors;
            for (int i = 0; i < nbs.Count; i++)
                if (nbs[i] is Transmission t) return t;
            return null;
        }

        // debug
        [System.Diagnostics.Conditional("DEBUG")]
        private void DebugPrintEdgeStats()
        {
            int tt = 0, pt = 0, pp = 0;
            foreach (var (o, d) in conexionesFijas)
            {
                if (o.State == Compartment.T && d.State == Compartment.T) tt++;
                else if (o.State != Compartment.T && d.State == Compartment.T) pt++;
                else if (o.State == Compartment.T && d.State != Compartment.T) pt++;
                else pp++;
            }

            int devices = agents.Count(a => a is not Transmission);
            int tcount = agents.OfType<Transmission>().Count();

            //Console.WriteLine($"[Edges] T–T={tt}, PC–T={pt}, PC–PC={pp}, total={conexionesFijas.Count}  | devices={devices}, T={tcount}");
        }

        private readonly object _changeLock = new();

        //  versión de instancia 
        private void EnqueueChange(Agent? oldAgent, Agent? newAgent)
        {
            // casos de altas
            if (oldAgent == null && newAgent != null)
            {
                lock (_changeLock)
                {
                    agents.Add(newAgent);
                    _agentsDirty = true;
                }
                return;
            }

            if (oldAgent == null) return; // baja nula ignorada

            lock (_changeLock)
            {
                if (changesSet.Contains(oldAgent.Id)) return;
                pendingChanges.Add((oldAgent, newAgent));  // puede ser null
                changesSet.Add(oldAgent.Id);
            }
        }

        // wrapper estatico
        public static void RequestStateChange(Agent? oldAgent, Agent? newAgent)
        {
            var sim = Current;
            if (sim == null) return;
            sim.EnqueueChange(oldAgent, newAgent);
        }

        public (int steps, TimeSpan elapsed) RunHeadless(
            int maxSteps,
            string csvPath,
            bool streamCsv = true,
            int flushEveryNSteps = 100,
            CancellationToken ct = default)
        {
            // arranca headless
            var t0 = Stopwatch.StartNew();

            try
            {
                if (streamCsv)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csvPath))!);
                    using var sw = new StreamWriter(csvPath, false);
                    sw.WriteLine("step,S,A,E,I,Q,R,T");

                    for (int s = 1; s <= maxSteps && !ct.IsCancellationRequested; s++)
                    {
                        Step();
                        var h = Historial[^1];
                        sw.WriteLine($"{s},{h.S},{h.A},{h.E},{h.I},{h.Q},{h.R},{h.T}");
                        if ((s % flushEveryNSteps) == 0) sw.Flush();
                    }
                }
                else
                {
                    for (int s = 1; s <= maxSteps && !ct.IsCancellationRequested; s++)
                        Step();

                    WriteHistoryCsv(csvPath);
                }
            }
            catch (Exception ex)
            {
                    Console.WriteLine("error en headless: " + ex.Message);
                Console.WriteLine(ex.ToString());
                throw;
            }
            finally
            {
                t0.Stop();
            // headless termino
            }

            return (Historial.Count, t0.Elapsed);
        }

        public (int steps, TimeSpan elapsed) RunHeadlessThenReplay(
        int maxSteps,
        string csvPath,
        TopologyKind topologyKind,
        InitOptions initOptions,
        ReplayOptions? replayOptions = null,
        bool streamCsv = true,
        int flushEveryNSteps = 100,
        CancellationToken ct = default)
        {
            var result = RunHeadless(maxSteps, csvPath, streamCsv, flushEveryNSteps, ct);

            // replay auto
            replayOptions ??= new ReplayOptions();
            try
            {
                StartReplayFromCsv(csvPath, topologyKind, initOptions, replayOptions);
            }
            catch (Exception ex)
            {
                // error en replay
            }
            return result;
        }

        private static List<FrameCounts> ParseCsvFrames(string csvPath)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException("No se encontró el CSV de salida.", csvPath);

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
                throw new InvalidOperationException("CSV sin datos.");

            // validar header
            var header = lines[0].Split(',');
            if (header.Length != 8)
                throw new InvalidOperationException($"Encabezado CSV inválido. Esperado 8 columnas, encontrado {header.Length}");

            var frames = new List<FrameCounts>(lines.Length - 1);
            for (int i = 1; i < lines.Length; i++)
            {
                var ln = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(ln)) continue;
                var p = ln.Split(',');
                if (p.Length != 8) continue;

                int step = int.Parse(p[0]);
                int s = int.Parse(p[1]);
                int a = int.Parse(p[2]);
                int e = int.Parse(p[3]);
                int ii = int.Parse(p[4]);
                int q = int.Parse(p[5]);
                int r = int.Parse(p[6]);
                int t = int.Parse(p[7]);

                frames.Add(new FrameCounts(step, s, a, e, ii, q, r, t));
            }
            if (frames.Count == 0) throw new InvalidOperationException("CSV sin frames válidos.");
            return frames;

        }

        public void StartReplayFromCsv(string csvPath, TopologyKind topology, InitOptions opts, ReplayOptions options)
        {
            // inicia startreplayfromcsv

            // parse csv
            _replayFrames = ParseCsvFrames(csvPath);
            _replayIndex = 0;
            // frames leidos del csv

            var first = _replayFrames[0];
            //Console.WriteLine($"primer frame: step={first.Step}, S={first.S}, A={first.A}, E={first.E}, I={first.I}, Q={first.Q}, R={first.R}, T={first.T}");

            // limpiar y construir topologia
            agents.Clear();
            conexionesFijas.Clear();
            topologyEdgesTT.Clear();
            tNodesCache.Clear();
            tCompId.Clear();
            Historial.Clear();

            Current = this; // por si tu renderer lo usa
            Agent.FrozenPositions = true;
            CanvasWidth = opts.Width;
            CanvasHeight = opts.Height;

            // generando topologia

            // generar t
            switch (topology)
            {
                case TopologyKind.Bus: GenerateBusT(opts); break;
                case TopologyKind.Star: GenerateStarT(opts); break;
                case TopologyKind.Ring: GenerateRingT(opts); break;
                case TopologyKind.Mesh: GenerateMeshT(opts); break;
                case TopologyKind.Tree: GenerateTreeT(opts); break;
                case TopologyKind.CustomCsv:
                    throw new NotSupportedException("Para replay usa una topología explícita (Bus/Star/Ring/Mesh/Tree).");
            }

            // reponer edges tt
            foreach (var (o, d) in topologyEdgesTT)
                AddEdge(o, d);

            // cache t
            tNodesCache = agents.OfType<Transmission>().ToList();
            // tnodes en escena

            // crear pool dispositivos
            int nMax = _replayFrames.Max(f => f.NDevices);
            // max devices
            BuildReplayPool(nMax, opts, options.SeedDevices);
            // replay pool creado

            // estado inicial
            // aplicando frame inicial
            ApplyFrame(_replayIndex);
            InvalidateRequested?.Invoke();
            // frame 0 aplicado

            //  Bandera y timer
            _isReplay = true;
            _agentsCache = agents.ToArray(); // snapshot
            _agentsDirty = false;

            // info t mismatch
            int tTop = tNodesCache.Count;
            int tCsvFirst = _replayFrames[0].T;
            if (options.WarnOnTCountMismatch && tTop != tCsvFirst)
                // t_topologia difiere

            // loop asincrono
            _replayCts?.Cancel();
            _replayCts = new System.Threading.CancellationTokenSource();
            if (options.AutoPlay)
            {
                // replay loop async
                _ = ReplayLoopAsync(options.StepDurationMs, _replayCts.Token);
            }
            else
            {
                // autoplay desactivado
                InvalidateRequested?.Invoke();
            }

            // startreplayfromcsv termino
        }


        private void BuildReplayPool(int n, InitOptions opts, int seed)
        {
            // borrar pc previo
            var ts = agents.OfType<Transmission>().ToList();
            var rng = new Random(seed);

            _replayPool.Clear();
            _replayPoolPos.Clear();

            // Crear n dispositivos (todos inicialmente Susceptible, da igual; el frame los reasigna)
            for (int i = 0; i < n; i++)
            {
                double x = rng.NextDouble() * (opts.Width - 2 * opts.Margin) + opts.Margin;
                double y = rng.NextDouble() * (opts.Height - 2 * opts.Margin) + opts.Margin;

                var dev = new Susceptible(x, y); // placeholder; luego reasignamos estado
                _replayPool.Add(dev);
                _replayPoolPos.Add((x, y));
                agents.Add(dev);
            }

            // conectar a t mas cercano
            if (ts.Count > 0)
            {
                foreach (var dev in _replayPool)
                {
                    var near = FindNearestT(dev, ts);
                    AddEdge(dev, near);
                }
            }

            // refrescar cache
            _agentsCache = agents.ToArray();
            _agentsDirty = false;

        }

        private void ApplyFrame(int index)
        {
            if (index < 0 || index >= _replayFrames.Count)
            {
                //Console.WriteLine($"[Sim] ApplyFrame index fuera de rango: {index}");
                return;
            }

            var f = _replayFrames[index];
            if(f.Step % 100 == 0)
            {
                // applyframe procesando
            }
            
            int nReq = f.NDevices;
            int nPool = _replayPool.Count;
            //Console.WriteLine($"[Sim] nReq={nReq}, nPool={nPool}");

            //  Ajustar visibilidad (por posición on/off-screen)
            for (int i = 0; i < nPool; i++)
            {
                var dev = _replayPool[i];
                if (i < nReq)
                {
                    // visible
                    var (x, y) = _replayPoolPos[i];
                    dev.SetPosition(x, y); 
                }
                else
                {
                    // oculto fuera
                    dev.SetPosition(HIDDEN_X, HIDDEN_Y);
                }
            }

            // asignar estados
            int cursor = 0;
            void PaintRange(int count, Compartments.Compartment state)
            {
                for (int k = 0; k < count && cursor < nReq; k++, cursor++)
                {
                    var a = _replayPool[cursor];
                    // reemplazar tipo
                    if (a.State == state) continue; // ya está
                    ReplaceAgentStateInPlace(a, state);
                    _replayPool[cursor] = agents[^1]; // mantener ref
                }
            }

            PaintRange(f.S, Compartments.Compartment.S);
            PaintRange(f.A, Compartments.Compartment.A);
            PaintRange(f.E, Compartments.Compartment.E);
            PaintRange(f.I, Compartments.Compartment.I);
            PaintRange(f.Q, Compartments.Compartment.Q);
            PaintRange(f.R, Compartments.Compartment.R);

            // los ocultos no importan

            // refrescar caché para el renderer
            _agentsCache = agents.ToArray();
            _agentsDirty = false;

        }

        private void ReplaceAgentStateInPlace(Agent oldA, Compartments.Compartment targetState)
        {
            Agent newA = targetState switch
            {
                Compartments.Compartment.S => new Susceptible(oldA.X, oldA.Y),
                Compartments.Compartment.A => new Antidote(oldA.X, oldA.Y),
                Compartments.Compartment.E => new Exposed(oldA.X, oldA.Y),
                Compartments.Compartment.I => new Infected(oldA.X, oldA.Y),
                Compartments.Compartment.Q => new Quarantine(oldA.X, oldA.Y),
                Compartments.Compartment.R => new Recovered(oldA.X, oldA.Y),
                _ => throw new InvalidOperationException("Estado inválido para dispositivo.")
            };

            // Reutilizar conexiones fijas (PC–T) y vecinos
            RewireEdgesOnReplace(oldA, newA);

            // Sustituir en la lista global
            agents.Remove(oldA);
            agents.Add(newA);

        }

        private async Task ReplayLoopAsync(int stepMs, CancellationToken ct)
        {
            // replay loop async inicio
            int localCounter = 0;

            try
            {
                while (!ct.IsCancellationRequested && _isReplay)
                {
                    // pintar
                    InvalidateRequested?.Invoke();

                    // avanzar
                    _replayIndex++;
                    if (_replayIndex >= _replayFrames.Count)
                        _replayIndex = 0; // loop infinito

                    ApplyFrame(_replayIndex);

                    if (localCounter < 10)
                        // replay frame
                    localCounter++;

                    await Task.Delay(stepMs, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // replay cancelado
            }
            // replay termino
        }

        public void StopReplay()
        {
            _isReplay = false;
            _replayCts?.Cancel();

        }

        // compatibilidad con simulationvm
        private bool running = false;

        public bool IsRunning => running;

        // Llamado por el VM en cada Tick (~30 FPS)
        public void RunTick()
        {
            if (!running) return;
            Step();
        }

        public void Start() => running = true;

        public void Stop() => running = false;

        public bool IsInReplay => _isReplay && _replayFrames.Count > 0;

        public bool StepReplayOnce()
        {
            if (!_isReplay || _replayFrames.Count == 0)
                return false;

            if (_replayIndex >= _replayFrames.Count - 1)
            {
                _isReplay = false;
                // replay completado
                return false;   // no hay más frames que avanzar
            }

            _replayIndex++;
            ApplyFrame(_replayIndex);
            InvalidateRequested?.Invoke();
            return true;
        }
    }
}