using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// Runs a whole battery of navigation tests declared in a YAML file, one scenario
    /// after the other in a single Play session, and writes the analyses at the end.
    ///
    /// This is the piece that turns the bench into something usable as a benchmark. A
    /// single component and a single text file replace "open the Inspector, pick a
    /// scenario, press Play, write the numbers down, repeat" — which is both slow and
    /// impossible to reproduce faithfully.
    ///
    /// Determinism is preserved across the battery: every run starts from the scenario's
    /// exact initial conditions, and the physics step and the global seed come from the
    /// file, so they are identical for every run and the numbers stay comparable.
    /// </summary>
    [RequireComponent(typeof(ScenarioRunner))]
    public class ScenarioSuiteRunner : MonoBehaviour
    {
        [Header("Arquivo de configuração")]
        /// <summary>YAML file describing the battery, relative to the project root.</summary>
        [Tooltip("Arquivo YAML da bateria de testes, relativo à raiz do projeto.")]
        public string SuiteFile = "Assets/Simulador/Cenarios/suite_padrao.yaml";

        /// <summary>Whether the battery starts automatically on entering Play.</summary>
        [Tooltip("Executa a bateria automaticamente ao entrar em Play.")]
        public bool RunOnStart = true;

        [Header("Componentes")]
        /// <summary>Component that executes one scenario. Empty looks for it on this object.</summary>
        [Tooltip("Executor de um cenário. Se ficar vazio, procura no próprio objeto.")]
        public ScenarioRunner Runner;

        /// <summary>Generator used by the random entries of the battery. Empty looks for it on this object.</summary>
        [Tooltip("Gerador usado pelas entradas aleatórias da suíte. Se ficar vazio, " +
                 "procura no próprio objeto.")]
        public RandomScenarioGenerator Generator;

        /// <summary>True while the battery is running.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Results of the runs finished so far, in run order.</summary>
        public IReadOnlyList<ScenarioRunResult> Results => results;

        /// <summary>The suite currently loaded, or null before the first run.</summary>
        public TestSuiteConfig Suite { get; private set; }

        /// <summary>One scheduled run: an entry plus, for stress entries, the seed to use.</summary>
        class PlannedRun
        {
            /// <summary>The suite entry this run came from.</summary>
            public SuiteEntry Entry;

            /// <summary>Seed for a generated run.</summary>
            public int Seed;

            /// <summary>True when the run is generated from a seed.</summary>
            public bool HasSeed;
        }

        readonly List<ScenarioRunResult> results = new();
        readonly List<PlannedRun> plan = new();
        int nextIndex;

        /// <summary>
        /// Physics steps to wait before starting the next scenario. Waiting instead of
        /// chaining immediately lets Unity actually destroy the targets of the previous
        /// run — Destroy is deferred — before the next run spawns its own.
        ///
        /// The exact count does not affect any measurement: each run re-places the USV,
        /// resets its velocities and starts a fresh metrics collector, so the idle steps
        /// in between are outside every run.
        /// </summary>
        const int STEPS_BETWEEN_RUNS = 2;

        /// <summary>Countdown to the next run; -1 means "waiting for the current run to end".</summary>
        int cooldownSteps = -1;

        void Awake()
        {
            if (Runner == null) Runner = GetComponent<ScenarioRunner>();
            if (Generator == null) Generator = GetComponent<RandomScenarioGenerator>();

            // The suite decides what runs and when; the runner must not start something of
            // its own on Play, or the first scenario would be played twice.
            if (Runner != null) Runner.RunOnStart = false;
        }

        void OnEnable()
        {
            if (Runner != null) Runner.RunFinished += OnRunFinished;
        }

        void OnDisable()
        {
            if (Runner != null) Runner.RunFinished -= OnRunFinished;

            // Leaving the accelerated pace behind would make everything else in the scene
            // run fast after the battery — including a Play session started by hand.
            Time.timeScale = 1f;
        }

        void Start()
        {
            if (RunOnStart) RunSuite();
        }

        /// <summary>
        /// Loads the configuration file and starts the battery. Safe to call again to
        /// re-run: the file is re-read, so editing it does not require leaving Play.
        /// </summary>
        public void RunSuite()
        {
            if (IsRunning)
            {
                Debug.LogWarning("[ScenarioSuiteRunner] A bateria já está em execução.");
                return;
            }

            if (Runner == null)
            {
                Debug.LogError("[ScenarioSuiteRunner] Falta o componente ScenarioRunner.");
                return;
            }

            if (!LoadSuite()) return;

            BuildPlan();
            if (plan.Count == 0)
            {
                Debug.LogError("[ScenarioSuiteRunner] A suíte não gerou nenhuma execução.");
                return;
            }

            results.Clear();
            nextIndex = 0;
            IsRunning = true;
            cooldownSteps = 1;

            // Pacing only: more simulated seconds per second of wall clock, same physics
            // step. Applied once for the whole battery so every run is paced alike.
            Time.timeScale = Suite.TimeScale;

            string pace = Suite.TimeScale > 1f ? $" a {YamlNode.Format(Suite.TimeScale)}x" : "";
            Debug.Log($"[ScenarioSuiteRunner] Bateria '{Suite.Name}': {plan.Count} execuções{pace} " +
                      $"a partir de {Suite.SourcePath}");
        }

        /// <summary>Reads the configuration file, reporting a readable error when it fails.</summary>
        /// <returns>True when the suite was loaded.</returns>
        bool LoadSuite()
        {
            try
            {
                Suite = TestSuiteConfig.Load(SuiteFile);
                return true;
            }
            catch (YamlParseException error)
            {
                Debug.LogError($"[ScenarioSuiteRunner] Erro no arquivo '{SuiteFile}': {error.Message}");
            }
            catch (FileNotFoundException error)
            {
                Debug.LogError($"[ScenarioSuiteRunner] {error.Message}\n" +
                               "Use 'Cenário Real > Ferramentas > Exportar Cenários para YAML' " +
                               "para criar a suíte padrão.");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"[ScenarioSuiteRunner] Não foi possível ler '{SuiteFile}': {error.Message}");
            }

            return false;
        }

        /// <summary>Expands the suite entries into the flat list of runs to perform.</summary>
        void BuildPlan()
        {
            plan.Clear();

            foreach (SuiteEntry entry in Suite.Entries)
            {
                if (!entry.IsRandom)
                {
                    plan.Add(new PlannedRun { Entry = entry });
                    continue;
                }

                // A stress entry becomes one run per seed: the seed sweep is the whole
                // point of a random battery, and each seed must stay individually
                // identifiable so a failure can be replayed on its own.
                foreach (int seed in entry.Seeds)
                    plan.Add(new PlannedRun { Entry = entry, Seed = seed, HasSeed = true });
            }
        }

        void FixedUpdate()
        {
            if (!IsRunning || cooldownSteps < 0) return;

            if (cooldownSteps > 0)
            {
                cooldownSteps--;
                return;
            }

            cooldownSteps = -1;
            StartNextRun();
        }

        /// <summary>Starts the next scheduled run, or finishes the battery.</summary>
        void StartNextRun()
        {
            if (nextIndex >= plan.Count)
            {
                FinishSuite();
                return;
            }

            PlannedRun run = plan[nextIndex++];

            ScenarioDefinition scenario = ResolveScenario(run);
            if (scenario == null)
            {
                // A broken entry must not abort the battery: the remaining scenarios still
                // carry information, and the missing rows are visible in the report.
                Debug.LogError($"[ScenarioSuiteRunner] Execução {nextIndex} ignorada: " +
                               "não foi possível preparar o cenário.");
                cooldownSteps = STEPS_BETWEEN_RUNS;
                return;
            }

            ApplySuiteSettings();

            string label = run.HasSeed ? $"seed{run.Seed}" : "";
            Debug.Log($"[ScenarioSuiteRunner] ({nextIndex}/{plan.Count}) {scenario.DisplayName}");
            Runner.Run(scenario, label);

            // Run() gives up early when the scene has no USV; without this check the
            // battery would wait forever for an event that will never be raised.
            if (!Runner.IsRunning)
            {
                Debug.LogError($"[ScenarioSuiteRunner] A execução '{scenario.DisplayName}' " +
                               "não pôde começar. Bateria interrompida.");
                FinishSuite();
            }
        }

        /// <summary>
        /// Produces the scenario of a scheduled run: the declared one, or a freshly
        /// generated one for a stress entry.
        /// </summary>
        /// <param name="run">The scheduled run.</param>
        /// <returns>The scenario, or null when it could not be prepared.</returns>
        ScenarioDefinition ResolveScenario(PlannedRun run)
        {
            if (!run.HasSeed) return run.Entry.Scenario;

            if (Generator == null)
            {
                Debug.LogError("[ScenarioSuiteRunner] A suíte tem entradas aleatórias, mas não há " +
                               "RandomScenarioGenerator no objeto.");
                return null;
            }

            ScenarioConfig.ApplyGeneratorSettings(run.Entry.RandomSettings, Generator);
            return Generator.Generate(run.Seed);
        }

        /// <summary>Applies the settings shared by every run of the battery.</summary>
        void ApplySuiteSettings()
        {
            Runner.WaterHeight = Suite.WaterHeight;
            Runner.FixedTimeStep = Suite.FixedTimeStep;
            Runner.RandomSeed = Suite.RandomSeed;
            Runner.ExportMap = Suite.ExportMaps;
            Runner.MapOutputFolder = Suite.OutputFolder;
        }

        /// <summary>Records a finished run and schedules the next one.</summary>
        /// <param name="runner">The runner that finished; always our own.</param>
        void OnRunFinished(ScenarioRunner runner)
        {
            if (!IsRunning) return;

            PlannedRun run = plan[nextIndex - 1];

            ScenarioRunResult result = ScenarioRunResult.From(runner.Metrics, runner.Scenario);
            result.Index = results.Count + 1;
            result.Source = run.Entry.Source;
            result.Seed = run.Seed;
            result.HasSeed = run.HasSeed;
            result.MapPath = runner.LastMapPath ?? "";
            results.Add(result);

            cooldownSteps = STEPS_BETWEEN_RUNS;
        }

        /// <summary>Closes the battery: prints the console summary and writes the analyses.</summary>
        void FinishSuite()
        {
            IsRunning = false;
            cooldownSteps = -1;
            Time.timeScale = 1f;

            int passed = 0;
            foreach (ScenarioRunResult result in results)
                if (result.Passed) passed++;

            var report = new System.Text.StringBuilder();
            report.AppendLine($"===== Bateria concluída: {Suite.Name} =====");
            report.AppendLine($"Execuções: {results.Count}   Aprovadas: {passed}   " +
                              $"Reprovadas: {results.Count - passed}");

            foreach (ScenarioRunResult result in results)
            {
                string cpa = result.MinCpaMeters < float.MaxValue
                    ? $"{result.MinCpaMeters:F1} m"
                    : "n/d";
                report.AppendLine($"  [{(result.Passed ? "OK " : "FALHA")}] {result.Label} — CPA mín. {cpa}");
            }

            if (Suite.ExportResults) report.Append(ExportResults());

            if (passed == results.Count) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>Writes the CSV and Markdown analyses, reporting where they went.</summary>
        /// <returns>The lines to append to the console report.</returns>
        string ExportResults()
        {
            try
            {
                List<string> files = ScenarioResultsExporter.Export(Suite, results);

                var sb = new System.Text.StringBuilder("Análises exportadas:\n");
                foreach (string file in files) sb.AppendLine($"  {file}");
                return sb.ToString();
            }
            catch (System.Exception error)
            {
                return $"Não foi possível exportar as análises: {error.Message}\n";
            }
        }
    }
}
