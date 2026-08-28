using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// One entry of a test suite: either a deterministic scenario or a batch of generated
    /// stress scenarios, one per seed.
    /// </summary>
    public class SuiteEntry
    {
        /// <summary>The deterministic scenario to run. Null for generated entries.</summary>
        public ScenarioDefinition Scenario;

        /// <summary>Generator settings for a stress entry. Null for deterministic entries.</summary>
        public YamlNode RandomSettings;

        /// <summary>
        /// Seeds to run for a stress entry, one run each. A seed is the whole reason a
        /// random test is useful: it turns "it failed sometimes" into a run anyone can
        /// reproduce exactly.
        /// </summary>
        public readonly List<int> Seeds = new();

        /// <summary>Where this entry came from: a file path or the suite itself.</summary>
        public string Source = "";

        /// <summary>True when this entry produces randomly generated scenarios.</summary>
        public bool IsRandom => RandomSettings != null;

        /// <summary>How many runs this entry contributes to the suite.</summary>
        public int RunCount => IsRandom ? Mathf.Max(1, Seeds.Count) : 1;
    }

    /// <summary>
    /// A whole battery of navigation tests, declared in a single YAML file: the scenarios
    /// to run, the simulation settings shared by all of them and where the results go.
    ///
    /// This is what makes the bench usable as a benchmark instead of a one-off experiment.
    /// Running the same file against two navigation algorithms produces two sets of
    /// numbers measured under identical conditions, which is the only way a comparison
    /// means anything.
    ///
    /// File format (every key is optional except the list of scenarios):
    /// <code>
    /// suite: Bateria padrao RIPEAM
    /// description: Os quatro encontros classicos.
    /// environment:
    ///   waterHeight: 0.05
    ///   fixedTimeStep: 0.02
    ///   randomSeed: 12345
    ///   timeScale: 8
    /// output:
    ///   folder: Assets/CartaReal/Testes
    ///   exportMaps: true
    ///   exportResults: true
    ///   csvSeparator: ","
    /// scenarios:
    ///   - file: Assets/Simulador/Cenarios/Cenario01_AlvoEstatico.yaml
    ///   - name: Cenario escrito aqui mesmo
    ///     usv:
    ///       startXZ: [9900, 7500]
    ///     targets:
    ///       - name: Alvo
    ///         behaviour: StraightLine
    ///         startOffsetXZ: [0, 800]
    ///   - random:
    ///       seeds: [1, 2, 3]
    ///       targetCount: [8, 16]
    /// </code>
    /// </summary>
    public class TestSuiteConfig
    {
        /// <summary>Name of the battery, used in the report and in the exported file names.</summary>
        public string Name = "Suite";

        /// <summary>What this battery is meant to measure.</summary>
        public string Description = "";

        /// <summary>Water level (Y) where the USV and the targets are placed.</summary>
        public float WaterHeight = 0.05f;

        /// <summary>Physics step, in seconds. Shared by every run so they are comparable.</summary>
        public float FixedTimeStep = 0.02f;

        /// <summary>Seed applied to Unity's global random, so incidental noise repeats.</summary>
        public int RandomSeed = 12345;

        /// <summary>
        /// How much faster than real time the battery runs. Purely a matter of pacing: it
        /// changes how much simulated time passes per second of wall clock, never the size
        /// of the physics step, so the sequence of steps — and therefore every measurement
        /// — is identical to a run at 1x.
        ///
        /// That only holds because everything that affects a measurement runs on the fixed
        /// step: dynamics, controller, guidance, waypoints, targets, sensor and the bench
        /// itself. Anything moved to a per-frame Update would break the guarantee.
        ///
        /// Unity caps how many fixed steps it will run per frame, so a value too high for
        /// the frame rate simply is not reached — it never produces a wrong result.
        /// </summary>
        public float TimeScale = 1f;

        /// <summary>Folder for the maps and the result files, relative to the project root.</summary>
        public string OutputFolder = "Assets/CartaReal/Testes";

        /// <summary>Whether each run also produces its PNG map.</summary>
        public bool ExportMaps = true;

        /// <summary>Whether the suite writes the CSV and Markdown analyses at the end.</summary>
        public bool ExportResults = true;

        /// <summary>
        /// Column separator of the exported CSV. The default comma is what pandas and R
        /// read without any argument; a team opening the files in a pt-BR Excel usually
        /// wants ';' instead.
        /// </summary>
        public string CsvSeparator = ",";

        /// <summary>The entries, in the order they will run.</summary>
        public readonly List<SuiteEntry> Entries = new();

        /// <summary>Absolute path of the file this suite was read from.</summary>
        public string SourcePath = "";

        /// <summary>Total number of runs, counting one per seed on the stress entries.</summary>
        public int TotalRuns
        {
            get
            {
                int total = 0;
                foreach (SuiteEntry entry in Entries) total += entry.RunCount;
                return total;
            }
        }

        /// <summary>
        /// Reads a suite from a YAML file. Scenario files referenced with 'file:' are
        /// resolved relative to the suite's own folder first, then to the project root,
        /// so a battery can be moved around as a single directory.
        /// </summary>
        /// <param name="path">Path to the suite file, relative to the project root or absolute.</param>
        /// <returns>The parsed suite.</returns>
        public static TestSuiteConfig Load(string path)
        {
            string fullPath = ScenarioConfig.ResolveProjectPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Arquivo de suíte não encontrado: {fullPath}");

            YamlNode root = YamlLite.Parse(File.ReadAllText(fullPath));

            var suite = new TestSuiteConfig { SourcePath = fullPath };
            suite.Name = root.Child("suite", "name")?.AsString(Path.GetFileNameWithoutExtension(fullPath))
                         ?? Path.GetFileNameWithoutExtension(fullPath);
            suite.Description = root.GetString("description", "");

            YamlNode environment = root.Child("environment");
            if (environment != null)
            {
                suite.WaterHeight = environment.GetFloat("waterHeight", suite.WaterHeight);
                suite.FixedTimeStep = environment.GetFloat("fixedTimeStep", suite.FixedTimeStep);
                suite.RandomSeed = environment.GetInt("randomSeed", suite.RandomSeed);
                suite.TimeScale = Mathf.Max(0.01f, environment.GetFloat("timeScale", suite.TimeScale));
            }

            YamlNode output = root.Child("output");
            if (output != null)
            {
                suite.OutputFolder = output.GetString("folder", suite.OutputFolder);
                suite.ExportMaps = output.GetBool("exportMaps", suite.ExportMaps);
                suite.ExportResults = output.GetBool("exportResults", suite.ExportResults);
                suite.CsvSeparator = output.GetString("csvSeparator", suite.CsvSeparator);
            }

            YamlNode scenarios = root.Child("scenarios", "cenarios");
            if (scenarios == null || scenarios.Count == 0)
                throw new YamlParseException(
                    $"A suíte '{fullPath}' não tem nenhum cenário: falta a lista 'scenarios:'.");

            string suiteFolder = Path.GetDirectoryName(fullPath);
            foreach (YamlNode node in scenarios.Items)
                suite.Entries.Add(ReadEntry(node, suiteFolder));

            return suite;
        }

        /// <summary>
        /// Reads one entry of the 'scenarios' list, in any of its three forms.
        /// </summary>
        /// <param name="node">The entry node.</param>
        /// <param name="suiteFolder">Folder of the suite file, used to resolve 'file:' entries.</param>
        /// <returns>The entry.</returns>
        static SuiteEntry ReadEntry(YamlNode node, string suiteFolder)
        {
            YamlNode random = node.Child("random", "aleatorio");
            if (random != null)
            {
                var entry = new SuiteEntry { RandomSettings = random, Source = "gerador" };

                YamlNode seeds = random.Child("seeds", "sementes");
                if (seeds != null)
                {
                    foreach (YamlNode seed in seeds.Items) entry.Seeds.Add(seed.AsInt());
                }
                else
                {
                    // A single 'seed:' is the common case; the plural form is for sweeps.
                    entry.Seeds.Add(random.GetInt("seed", 20260813));
                }

                if (entry.Seeds.Count == 0)
                    throw new YamlParseException(
                        $"Linha {random.Line}: a entrada aleatória não tem nenhuma semente.");

                return entry;
            }

            string file = node.GetString("file", null);
            if (!string.IsNullOrEmpty(file))
            {
                string resolved = ResolveScenarioFile(file, suiteFolder);
                return new SuiteEntry
                {
                    Scenario = ScenarioConfig.Load(resolved),
                    Source = file
                };
            }

            // Anything else is a scenario written inline in the suite file.
            return new SuiteEntry
            {
                Scenario = ScenarioConfig.FromYaml(node),
                Source = "suíte"
            };
        }

        /// <summary>
        /// Finds a referenced scenario file next to the suite, falling back to a path
        /// relative to the project root.
        /// </summary>
        /// <param name="file">Path as written in the suite file.</param>
        /// <param name="suiteFolder">Folder of the suite file.</param>
        /// <returns>The path to load.</returns>
        static string ResolveScenarioFile(string file, string suiteFolder)
        {
            if (Path.IsPathRooted(file)) return file;

            string besideSuite = Path.GetFullPath(Path.Combine(suiteFolder, file));
            return File.Exists(besideSuite) ? besideSuite : file;
        }

        /// <summary>
        /// Writes this suite back to YAML text, with every scenario expanded inline.
        /// Used by the Editor tool that turns the existing .asset scenarios into a file.
        /// </summary>
        /// <returns>The formatted document.</returns>
        public string ToYaml()
        {
            var root = YamlNode.NewMapping();
            root.Add("suite", Name);
            if (!string.IsNullOrEmpty(Description)) root.Add("description", Description);

            var environment = YamlNode.NewMapping();
            environment.Add("waterHeight", WaterHeight);
            environment.Add("fixedTimeStep", FixedTimeStep);
            environment.Add("randomSeed", RandomSeed);
            environment.Add("timeScale", TimeScale);
            root.Add("environment", environment);

            var output = YamlNode.NewMapping();
            output.Add("folder", OutputFolder);
            output.Add("exportMaps", ExportMaps);
            output.Add("exportResults", ExportResults);
            output.Add("csvSeparator", CsvSeparator);
            root.Add("output", output);

            var scenarios = YamlNode.NewSequence();
            foreach (SuiteEntry entry in Entries)
            {
                if (entry.IsRandom)
                {
                    var wrapper = YamlNode.NewMapping();
                    wrapper.Add("random", entry.RandomSettings);
                    scenarios.AddItem(wrapper);
                    continue;
                }

                if (entry.Scenario != null)
                    scenarios.AddItem(ScenarioConfig.ToYaml(entry.Scenario));
            }
            root.Add("scenarios", scenarios);

            return YamlLite.Write(root);
        }
    }
}
