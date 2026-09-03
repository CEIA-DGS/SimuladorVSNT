using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MaritimeScenario.Boat;
using MaritimeScenario.Real;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// Runs a deterministic navigation test scenario: places the USV and the targets at
    /// their exact initial conditions, publishes the route through the simulation itself
    /// (no ROS required) and measures what happens.
    ///
    /// Determinism comes from three choices:
    ///  • everything advances on the fixed physics step, never per frame;
    ///  • no value is randomized — the whole scenario is declared in the asset;
    ///  • the random seed is still fixed, so any incidental use of Random (such as the
    ///    sensor noise models) repeats identically between runs.
    ///
    /// Put this component on an empty GameObject in the scene, point it at a
    /// ScenarioDefinition and press Play.
    /// </summary>
    public class ScenarioRunner : MonoBehaviour
    {
        [Header("Modo de teste")]
        /// <summary>Cleared runs the deterministic scenario chosen below; set draws a stress scenario from the generator.</summary>
        [Tooltip("DESMARCADO: roda o cenário determinístico escolhido abaixo (encontros padrão).\n" +
                 "MARCADO: sorteia um cenário de estresse com tráfego aleatório, usando a semente " +
                 "do componente Random Scenario Generator.")]
        public bool UseRandomGenerator = false;

        [Header("Cenário")]
        /// <summary>The scenario to run. Used when the random mode is off.</summary>
        [Tooltip("O cenário de teste a executar. Usado quando o modo aleatório está desmarcado.")]
        public ScenarioDefinition Scenario;

        /// <summary>Generator used in random mode. Empty looks for the component on this object.</summary>
        [Tooltip("Gerador usado no modo aleatório. Se ficar vazio, procura o componente " +
                 "no próprio objeto.")]
        public RandomScenarioGenerator Generator;

        /// <summary>Whether the run starts automatically on entering Play.</summary>
        [Tooltip("Executa automaticamente ao entrar em Play.")]
        public bool RunOnStart = true;

        [Header("USV")]
        /// <summary>The USV under test. Empty searches the scene for the first one found.</summary>
        [Tooltip("O USV a testar. Se vazio, procura na cena pelo primeiro que encontrar.")]
        public Transform Usv;

        [Header("Determinismo")]
        /// <summary>Seed applied to the global random generator, so that any incidental use of it, such as sensor noise, repeats between runs.</summary>
        [Tooltip("Semente fixa aplicada ao gerador aleatório, para que qualquer uso incidental de " +
                 "Random (ex.: ruído dos sensores) se repita igual entre execuções.")]
        public int RandomSeed = 12345;

        /// <summary>Physics step, in seconds. 0.02 is 50 Hz.</summary>
        [Tooltip("Passo fixo de física, em segundos. 0.02 = 50 Hz (padrão do Unity).")]
        public float FixedTimeStep = 0.02f;

        [Header("Relatório")]
        /// <summary>Whether a PNG map of the run is drawn over the chart at the end.</summary>
        [Tooltip("Gera um mapa PNG do teste ao final, desenhado sobre a carta da região.")]
        public bool ExportMap = true;

        /// <summary>Folder where the maps are written, relative to the project root.</summary>
        [Tooltip("Pasta onde os mapas são salvos, relativa à raiz do projeto.")]
        public string MapOutputFolder = "Assets/CartaReal/Testes";

        [Header("Ambiente")]
        /// <summary>Water level (Y) where the USV and the targets are placed.</summary>
        [Tooltip("Altura da água (Y) onde o USV e os alvos são posicionados. " +
                 "0.05 é a altura da malha de água criada pelo builder do cenário real.")]
        public float WaterHeight = 0.05f;

        /// <summary>
        /// Suffix added to the exported file names, and shown in the report. The suite
        /// runner uses it to carry the seed of a generated run, which is the one piece of
        /// information needed to reproduce a failure.
        /// </summary>
        [HideInInspector] public string RunLabel = "";

        /// <summary>Measurements of the current (or last) run.</summary>
        public ScenarioMetrics Metrics { get; private set; }

        /// <summary>True while a scenario is executing.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Path of the map exported by the last run, or null when none was produced.</summary>
        public string LastMapPath { get; private set; }

        /// <summary>
        /// Raised once a run has ended and its measurements are complete. This is what
        /// lets a whole battery of scenarios be chained in a single Play session.
        /// </summary>
        public event System.Action<ScenarioRunner> RunFinished;

        WaypointManager waypointManager;
        LosGuidance guidance;
        readonly List<GameObject> spawnedTargets = new();
        GameObject targetsRoot;

        /// <summary>
        /// Whether this run published a route. When it did not (stress-test scenarios,
        /// where the waypoints come from outside), an inactive mission is the normal
        /// state and must not be mistaken for "route finished".
        /// </summary>
        bool routePublished;

        void Start()
        {
            if (RunOnStart) Run();
        }

        /// <summary>
        /// Runs a scenario handed over directly, instead of the one set in the Inspector.
        /// Used by <see cref="ScenarioSuiteRunner"/> to play a battery read from a YAML
        /// file, where the scenarios exist only in memory.
        /// </summary>
        /// <param name="scenario">The scenario to run.</param>
        /// <param name="label">Suffix for the exported file names, such as the seed.</param>
        public void Run(ScenarioDefinition scenario, string label = "")
        {
            UseRandomGenerator = false;
            Scenario = scenario;
            RunLabel = label;
            Run();
        }

        /// <summary>
        /// Sets up and starts the scenario. Safe to call again to re-run: everything
        /// previously spawned is cleared first, so a repeated run starts from the very
        /// same state.
        /// </summary>
        public void Run()
        {
            if (UseRandomGenerator)
            {
                if (Generator == null) Generator = GetComponent<RandomScenarioGenerator>();

                if (Generator == null)
                {
                    Debug.LogError("[ScenarioRunner] Modo aleatório ligado, mas não há " +
                                   "RandomScenarioGenerator neste objeto nem no campo Generator.");
                    return;
                }
                Scenario = Generator.Generate();
            }

            if (Scenario == null)
            {
                Debug.LogError("[ScenarioRunner] Nenhum cenário para executar: escolha um asset no " +
                               "campo Scenario ou marque 'Use Random Generator'.");
                return;
            }

            // A fixed seed and a fixed physics step are what make the run reproducible.
            Random.InitState(RandomSeed);
            Time.fixedDeltaTime = FixedTimeStep;

            ClearTargets();

            if (!ResolveUsv()) return;

            Metrics = new ScenarioMetrics(Scenario.MinSafeDistanceMeters);

            PlaceUsv();
            SpawnTargets();
            PublishRoute();

            IsRunning = true;
            Debug.Log($"[ScenarioRunner] Cenário iniciado: {Scenario.DisplayName}");
        }

        /// <summary>
        /// Stops the run, prints the report and leaves the measurements available in
        /// <see cref="Metrics"/>.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;

            // Zeroing the cruise speed brings the USV to a stop when the run ends by timeout
            // (when it ends by finishing the route, LosGuidance already commands zero).
            if (guidance != null) guidance.cruiseSpeed = 0f;

            string report = Metrics.BuildReport(Scenario.DisplayName);

            // The seed is the single piece of information needed to replay a failure.
            if (UseRandomGenerator && Generator != null)
                report += $"Semente para reproduzir: {Generator.LastUsedSeed}\n";
            else if (!string.IsNullOrEmpty(RunLabel))
                report += $"Identificação da execução: {RunLabel}\n";

            LastMapPath = ExportMap ? ExportRunMap() : null;
            if (LastMapPath != null) report += $"Mapa do teste: {LastMapPath}\n";

            if (Metrics.Passed) Debug.Log(report);
            else Debug.LogWarning(report);

            RunFinished?.Invoke(this);
        }

        /// <summary>
        /// Samples the measurements once per physics step and ends the run when the route
        /// is finished or the time limit is reached.
        /// </summary>
        void FixedUpdate()
        {
            if (!IsRunning || Metrics == null) return;

            Metrics.Sample(Usv, Time.fixedDeltaTime);

            // Only a run that actually published a route can "finish" it.
            if (routePublished && waypointManager != null && !waypointManager.IsMissionActive)
            {
                Metrics.MissionCompleted = true;
                Stop();
                return;
            }

            if (Metrics.ElapsedSeconds >= Scenario.MaxDurationSeconds)
                Stop();
        }

        /// <summary>
        /// Renders the map of the finished run. The background comes from the tactical
        /// chart already present in the scene, which is the same depth-coloured image the
        /// scenario builder produced for the region.
        /// </summary>
        /// <returns>Path of the PNG written, or null when it could not be produced.</returns>
        string ExportRunMap()
        {
            var chart = Object.FindAnyObjectByType<TacticalChart>();
            if (chart == null || chart.ChartImage == null)
            {
                Debug.LogWarning("[ScenarioRunner] Carta tática não encontrada na cena — " +
                                 "o mapa do teste não foi gerado.");
                return null;
            }

            // File names carry the scenario and, for generated runs, the seed — which is
            // what lets a specific run be found again later.
            string safeName = SanitizeFileName(Scenario.DisplayName);
            string tag = !string.IsNullOrEmpty(RunLabel)
                ? "_" + SanitizeFileName(RunLabel)
                : UseRandomGenerator && Generator != null
                    ? $"_seed{Generator.LastUsedSeed}"
                    : "";
            string fileName = $"{safeName}{tag}.png";
            string fullPath = Path.Combine(Application.dataPath, "..", MapOutputFolder, fileName);

            return ScenarioMapExporter.Export(
                Scenario, Metrics, chart.ChartImage, chart.WorldSize, WaterHeight,
                Path.GetFullPath(fullPath));
        }

        /// <summary>
        /// Turns a scenario name into something safe to use as a file name. Scenario names
        /// are free text written by whoever declared the test, so they can carry spaces,
        /// slashes and accents that the file system would reject.
        /// </summary>
        /// <param name="value">The raw name.</param>
        /// <returns>The sanitized name.</returns>
        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "cenario";

            var sb = new System.Text.StringBuilder(value.Length);
            foreach (char c in value.Replace(' ', '_'))
                sb.Append(System.Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '-' : c);

            return sb.ToString();
        }

        // ---------------- setup ----------------

        /// <summary>
        /// Finds the USV and makes sure it carries the autonomous navigation stack, with
        /// the manual controllers switched off so the run is not affected by input.
        /// </summary>
        /// <returns>True when a usable USV is available.</returns>
        bool ResolveUsv()
        {
            if (Usv == null)
            {
                var dynamics = Object.FindAnyObjectByType<UsvDynamics>();
                if (dynamics != null) Usv = dynamics.transform;
            }

            if (Usv == null)
            {
                var boat = Object.FindAnyObjectByType<BoatController>();
                if (boat != null) Usv = boat.transform;
            }

            if (Usv == null)
            {
                Debug.LogError("[ScenarioRunner] USV não encontrado na cena. " +
                               "Construa o cenário (Cenário Real > 1) ou atribua o campo Usv.");
                return false;
            }

            // Manual driving would make the run non-reproducible.
            var manualBoat = Usv.GetComponent<BoatController>();
            if (manualBoat != null) manualBoat.enabled = false;

            var manualUsv = Usv.GetComponent<UsvManualController>();
            if (manualUsv != null) manualUsv.enabled = false;

            // The autonomous stack: waypoints -> LOS guidance -> controller -> dynamics.
            EnsureComponent<Rigidbody>(Usv.gameObject);
            EnsureComponent<UsvDynamics>(Usv.gameObject);
            waypointManager = EnsureComponent<WaypointManager>(Usv.gameObject);
            guidance = EnsureComponent<LosGuidance>(Usv.gameObject);
            EnsureComponent<UsvController>(Usv.gameObject);

            guidance.cruiseSpeed = Scenario.UsvCruiseSpeedMs;
            return true;
        }

        /// <summary>Returns the component on the object, adding it when missing.</summary>
        static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        /// <summary>Places the USV at the scenario's exact start pose, at rest.</summary>
        void PlaceUsv()
        {
            Usv.position = new Vector3(Scenario.UsvStartXZ.x, WaterHeight, Scenario.UsvStartXZ.y);
            Usv.rotation = Quaternion.Euler(0f, Scenario.UsvStartHeadingDegrees, 0f);

            var body = Usv.GetComponent<Rigidbody>();
            if (body == null) return;

            // UsvDynamics is a 3-DOF surface model (surge, sway, yaw): it rewrites the
            // velocity every step with no vertical component and never accounts for
            // gravity. Leaving gravity on makes the physics engine add downward speed
            // after each step, and the hull slowly sinks. Constraining the vertical axis
            // and the roll/pitch keeps the vessel on the exact plane the model works in.
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY
                             | RigidbodyConstraints.FreezeRotationX
                             | RigidbodyConstraints.FreezeRotationZ;

            // Starting from rest removes any leftover motion from a previous run.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Creates the targets described by the scenario. They are built procedurally
        /// (no prefabs) so a scenario is fully defined by its asset.
        /// </summary>
        void SpawnTargets()
        {
            if (Scenario.Targets == null || Scenario.Targets.Count == 0) return;

            targetsRoot = new GameObject("AlvosDoCenario");
            targetsRoot.transform.SetParent(transform, false);

            foreach (TargetSpec spec in Scenario.Targets)
            {
                Vector3 start = Scenario.BuildAbsoluteTargetStart(spec, WaterHeight);
                GameObject target = CreateTargetBody(spec, start);
                target.transform.SetParent(targetsRoot.transform, true);

                if (spec.Behaviour == TargetBehaviour.StraightLine)
                    ConfigureStraightLineMotion(target, spec, start);
                else if (spec.Behaviour == TargetBehaviour.Route)
                    ConfigureRouteMotion(target, spec);

                spawnedTargets.Add(target);

                // Hulls touch when the gap closes to roughly half a beam on each side.
                float contactDistance = (spec.Beam * 0.5f) + 3f;
                Metrics.RegisterTarget(target.transform, spec.Name, contactDistance);
            }
        }

        /// <summary>Builds the visible body of a target, sized by its length and beam.</summary>
        static GameObject CreateTargetBody(TargetSpec spec, Vector3 position)
        {
            var target = new GameObject($"Alvo_{spec.Name}");
            target.transform.position = position;
            target.transform.rotation = Quaternion.Euler(0f, spec.HeadingDegrees, 0f);

            var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "Casco";
            hull.transform.SetParent(target.transform, false);
            hull.transform.localScale = new Vector3(spec.Beam, Mathf.Max(2f, spec.Beam * 0.4f), spec.Length);

            // No collider: the sensor's occlusion linecast must only ever hit the terrain.
            Object.Destroy(hull.GetComponent<Collider>());

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", spec.HullColor);
            hull.GetComponent<Renderer>().sharedMaterial = material;

            return target;
        }

        /// <summary>
        /// Makes a target travel in a straight line along its heading, by giving it a
        /// two-waypoint route long enough to cover the whole scenario.
        /// </summary>
        void ConfigureStraightLineMotion(GameObject target, TargetSpec spec, Vector3 start)
        {
            float headingRad = spec.HeadingDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Sin(headingRad), 0f, Mathf.Cos(headingRad));

            // Long enough that the target never runs out of route before the time limit.
            float travel = Mathf.Max(1000f, spec.SpeedMs * Scenario.MaxDurationSeconds * 1.5f);

            var vessel = target.AddComponent<DynamicVessel>();
            vessel.Waypoints = new List<Vector3> { start, start + direction * travel };
            vessel.Loop = false;
            vessel.Speed = spec.SpeedMs;
            vessel.WaterHeight = WaterHeight;
            vessel.Length = spec.Length;
            vessel.Beam = spec.Beam;
            vessel.Kind = spec.Name;
        }

        /// <summary>
        /// Makes a target follow its own multi-waypoint route. Used by the randomly
        /// generated stress scenarios, where each vessel has its own path.
        /// </summary>
        void ConfigureRouteMotion(GameObject target, TargetSpec spec)
        {
            List<Vector3> route = Scenario.BuildAbsoluteTargetRoute(spec, WaterHeight);

            // A single point is not a route; fall back to straight-line motion so the
            // target still behaves sensibly instead of standing still by accident.
            if (route.Count < 2)
            {
                ConfigureStraightLineMotion(target, spec, target.transform.position);
                return;
            }

            var vessel = target.AddComponent<DynamicVessel>();
            vessel.Waypoints = route;
            vessel.Loop = spec.LoopRoute;
            vessel.Speed = spec.SpeedMs;
            vessel.WaterHeight = WaterHeight;
            vessel.Length = spec.Length;
            vessel.Beam = spec.Beam;
            vessel.Kind = spec.Name;
        }

        /// <summary>
        /// Publishes the route from the simulation itself, which is what the deterministic
        /// scenarios require — the run must not depend on an external ROS publisher.
        /// </summary>
        void PublishRoute()
        {
            routePublished = false;

            // Stress-test scenarios deliberately leave this off: what is being exercised is
            // the reaction to random traffic, with the route coming from outside (ROS).
            if (!Scenario.PublishWaypoints)
            {
                Debug.Log("[ScenarioRunner] Cenário não publica rota — o USV aguarda waypoints externos.");
                return;
            }

            Vector3[] waypoints = Scenario.BuildAbsoluteWaypoints(WaterHeight);
            if (waypoints.Length < 2)
            {
                Debug.LogError($"[ScenarioRunner] O cenário '{Scenario.DisplayName}' precisa de ao menos 2 waypoints.");
                return;
            }

            waypointManager.SetPath(waypoints);
            routePublished = true;
        }

        /// <summary>Destroys the targets created by a previous run.</summary>
        void ClearTargets()
        {
            foreach (GameObject target in spawnedTargets)
                if (target != null) Destroy(target);
            spawnedTargets.Clear();

            if (targetsRoot != null) Destroy(targetsRoot);
        }

        /// <summary>Draws the route and the target start positions in the Scene view.</summary>
        void OnDrawGizmos()
        {
            if (Scenario == null) return;

            Vector3[] waypoints = Scenario.BuildAbsoluteWaypoints(WaterHeight);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Gizmos.DrawSphere(waypoints[i], 8f);
                if (i > 0) Gizmos.DrawLine(waypoints[i - 1], waypoints[i]);
            }

            if (Scenario.Targets == null) return;

            foreach (TargetSpec spec in Scenario.Targets)
            {
                Vector3 start = Scenario.BuildAbsoluteTargetStart(spec, WaterHeight);

                // Static obstacles in grey, moving targets in orange: the difference is
                // what tells you at a glance whether an encounter will develop.
                Gizmos.color = spec.Behaviour == TargetBehaviour.Static
                    ? new Color(0.7f, 0.7f, 0.7f, 0.9f)
                    : new Color(1f, 0.4f, 0.2f, 0.9f);

                Gizmos.DrawWireCube(start, new Vector3(spec.Beam, 4f, spec.Length));

                if (spec.Behaviour == TargetBehaviour.Route)
                {
                    List<Vector3> route = Scenario.BuildAbsoluteTargetRoute(spec, WaterHeight);
                    for (int i = 1; i < route.Count; i++)
                        Gizmos.DrawLine(route[i - 1], route[i]);
                }
                else if (spec.Behaviour == TargetBehaviour.StraightLine)
                {
                    // Show where the target is heading, over the full scenario duration.
                    float rad = spec.HeadingDegrees * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                    Gizmos.DrawLine(start, start + direction * spec.SpeedMs * Scenario.MaxDurationSeconds);
                }
            }
        }
    }
}
