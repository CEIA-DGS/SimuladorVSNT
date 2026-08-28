using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// The outcome of a single run, flattened into the numbers a comparison needs.
    /// Keeping this separate from <see cref="ScenarioMetrics"/> matters: the metrics
    /// object holds live references to scene transforms and dies with the run, while this
    /// record survives the whole suite and can be written to a file afterwards.
    /// </summary>
    public class ScenarioRunResult
    {
        /// <summary>Position of this run in the suite, starting at 1.</summary>
        public int Index;

        /// <summary>Scenario name, as declared in the configuration file.</summary>
        public string ScenarioName = "";

        /// <summary>Where the scenario came from: a file name, the suite itself or the generator.</summary>
        public string Source = "";

        /// <summary>Seed of a generated run. Meaningless when <see cref="HasSeed"/> is false.</summary>
        public int Seed;

        /// <summary>True when this run came from the random generator.</summary>
        public bool HasSeed;

        /// <summary>True when the run met the scenario's criteria.</summary>
        public bool Passed;

        /// <summary>True when the USV came close enough to a target to count as contact.</summary>
        public bool CollisionDetected;

        /// <summary>Name of whatever was hit, when a collision happened.</summary>
        public string CollidedWith = "";

        /// <summary>True when the USV reached the last waypoint of its route.</summary>
        public bool MissionCompleted;

        /// <summary>Simulated duration of the run, in seconds.</summary>
        public float DurationSeconds;

        /// <summary>Safety margin the run was judged against, in meters.</summary>
        public float MinSafeDistanceMeters;

        /// <summary>Smallest CPA observed against any target, in meters.</summary>
        public float MinCpaMeters = float.MaxValue;

        /// <summary>Name of the target that produced the smallest CPA.</summary>
        public string MinCpaTarget = "";

        /// <summary>Instant of the smallest CPA, in simulated seconds.</summary>
        public float MinCpaTimeSeconds;

        /// <summary>How many targets the scenario had.</summary>
        public int TargetCount;

        /// <summary>How many targets were approached closer than the safety margin.</summary>
        public int SafetyViolations;

        /// <summary>Path of the map exported for this run, when there is one.</summary>
        public string MapPath = "";

        /// <summary>Per-target measurements, kept for the detailed CPA table.</summary>
        public readonly List<TargetEncounterResult> Encounters = new();

        /// <summary>How the run should be identified in a report.</summary>
        public string Label => HasSeed ? $"{ScenarioName} (semente {Seed})" : ScenarioName;

        /// <summary>
        /// Builds a result record out of a finished run.
        /// </summary>
        /// <param name="metrics">The measurements collected during the run.</param>
        /// <param name="scenario">The scenario that was run.</param>
        /// <returns>The flattened result.</returns>
        public static ScenarioRunResult From(ScenarioMetrics metrics, ScenarioDefinition scenario)
        {
            var result = new ScenarioRunResult
            {
                ScenarioName = scenario != null ? scenario.DisplayName : "?",
                MinSafeDistanceMeters = scenario != null ? scenario.MinSafeDistanceMeters : 0f
            };

            if (metrics == null) return result;

            result.Passed = metrics.Passed;
            result.CollisionDetected = metrics.CollisionDetected;
            result.CollidedWith = metrics.CollidedWith ?? "";
            result.MissionCompleted = metrics.MissionCompleted;
            result.DurationSeconds = metrics.ElapsedSeconds;

            foreach (TargetEncounterResult encounter in metrics.Results)
            {
                result.Encounters.Add(encounter);
                result.TargetCount++;
                if (encounter.SafetyViolated) result.SafetyViolations++;

                if (encounter.MinDistance < result.MinCpaMeters)
                {
                    result.MinCpaMeters = encounter.MinDistance;
                    result.MinCpaTarget = encounter.Name;
                    result.MinCpaTimeSeconds = encounter.TimeOfMinDistance;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Writes the analyses of a finished suite to disk: two CSV tables for whoever wants
    /// to plot or aggregate the numbers, and a Markdown summary for whoever just wants to
    /// know whether it passed.
    ///
    /// Numbers are always written with a dot as the decimal separator, regardless of the
    /// machine's locale. This is not cosmetic: on a pt-BR system the default formatting
    /// would emit "0,05", which collides with the CSV separator and silently corrupts
    /// every file the team tries to analyse.
    /// </summary>
    public static class ScenarioResultsExporter
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>
        /// Writes the CSV tables and the Markdown summary of a suite.
        /// </summary>
        /// <param name="suite">The suite that was run, for its name and output settings.</param>
        /// <param name="results">The results, in run order.</param>
        /// <returns>The paths written, in the order runs/CPA/summary.</returns>
        public static List<string> Export(TestSuiteConfig suite, IReadOnlyList<ScenarioRunResult> results)
        {
            var written = new List<string>();

            string folder = ScenarioConfig.ResolveProjectPath(suite.OutputFolder);
            Directory.CreateDirectory(folder);

            // The timestamp keeps successive runs of the same battery side by side, which
            // is exactly what a before/after comparison of an algorithm needs.
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Invariant);
            string prefix = $"{ScenarioRunner.SanitizeFileName(suite.Name)}_{stamp}";
            string separator = string.IsNullOrEmpty(suite.CsvSeparator) ? "," : suite.CsvSeparator;

            written.Add(Write(Path.Combine(folder, prefix + "_execucoes.csv"), BuildRunsCsv(results, separator)));
            written.Add(Write(Path.Combine(folder, prefix + "_cpa.csv"), BuildEncountersCsv(results, separator)));
            written.Add(Write(Path.Combine(folder, prefix + "_resumo.md"), BuildSummary(suite, results)));

            return written;
        }

        /// <summary>Writes a text file and returns its path.</summary>
        /// <param name="path">Destination path.</param>
        /// <param name="content">File contents.</param>
        /// <returns>The path written.</returns>
        static string Write(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(true));
            return path;
        }

        // ---------------- tables ----------------

        /// <summary>Builds the one-row-per-run table.</summary>
        /// <param name="results">The results, in run order.</param>
        /// <param name="separator">CSV column separator.</param>
        /// <returns>The CSV text.</returns>
        static string BuildRunsCsv(IReadOnlyList<ScenarioRunResult> results, string separator)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Row(separator,
                "indice", "cenario", "origem", "semente", "aprovado", "colisao", "colidiu_com",
                "rota_concluida", "duracao_s", "cpa_min_m", "cpa_min_alvo", "cpa_min_t_s",
                "distancia_seguranca_m", "alvos", "violacoes", "mapa"));

            foreach (ScenarioRunResult r in results)
            {
                sb.AppendLine(Row(separator,
                    r.Index.ToString(Invariant),
                    r.ScenarioName,
                    r.Source,
                    r.HasSeed ? r.Seed.ToString(Invariant) : "",
                    Flag(r.Passed),
                    Flag(r.CollisionDetected),
                    r.CollidedWith,
                    Flag(r.MissionCompleted),
                    Number(r.DurationSeconds),
                    Number(r.MinCpaMeters),
                    r.MinCpaTarget,
                    Number(r.MinCpaTimeSeconds),
                    Number(r.MinSafeDistanceMeters),
                    r.TargetCount.ToString(Invariant),
                    r.SafetyViolations.ToString(Invariant),
                    r.MapPath));
            }

            return sb.ToString();
        }

        /// <summary>Builds the one-row-per-encounter table, the detailed CPA data.</summary>
        /// <param name="results">The results, in run order.</param>
        /// <param name="separator">CSV column separator.</param>
        /// <returns>The CSV text.</returns>
        static string BuildEncountersCsv(IReadOnlyList<ScenarioRunResult> results, string separator)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Row(separator,
                "indice", "cenario", "semente", "alvo", "cpa_m", "t_cpa_s",
                "distancia_contato_m", "violou_seguranca"));

            foreach (ScenarioRunResult r in results)
                foreach (TargetEncounterResult e in r.Encounters)
                    sb.AppendLine(Row(separator,
                        r.Index.ToString(Invariant),
                        r.ScenarioName,
                        r.HasSeed ? r.Seed.ToString(Invariant) : "",
                        e.Name,
                        Number(e.MinDistance),
                        Number(e.TimeOfMinDistance),
                        Number(e.ContactDistance),
                        Flag(e.SafetyViolated)));

            return sb.ToString();
        }

        /// <summary>Builds the human-readable summary of the whole battery.</summary>
        /// <param name="suite">The suite that was run.</param>
        /// <param name="results">The results, in run order.</param>
        /// <returns>The Markdown text.</returns>
        static string BuildSummary(TestSuiteConfig suite, IReadOnlyList<ScenarioRunResult> results)
        {
            int passed = 0;
            int collisions = 0;
            float worstCpa = float.MaxValue;
            string worstCpaRun = "";
            float cpaSum = 0f;
            int cpaCount = 0;

            foreach (ScenarioRunResult r in results)
            {
                if (r.Passed) passed++;
                if (r.CollisionDetected) collisions++;

                if (r.MinCpaMeters < float.MaxValue)
                {
                    cpaSum += r.MinCpaMeters;
                    cpaCount++;
                    if (r.MinCpaMeters < worstCpa)
                    {
                        worstCpa = r.MinCpaMeters;
                        worstCpaRun = r.Label;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {suite.Name}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(suite.Description))
            {
                sb.AppendLine(suite.Description);
                sb.AppendLine();
            }

            sb.AppendLine($"- Executado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"- Arquivo da suíte: `{suite.SourcePath}`");
            sb.AppendLine($"- Passo de física: {Number(suite.FixedTimeStep)} s " +
                          $"({Number(1f / Mathf.Max(0.0001f, suite.FixedTimeStep))} Hz)");
            sb.AppendLine();

            sb.AppendLine("## Resultado geral");
            sb.AppendLine();
            float passRate = results.Count > 0 ? 100f * passed / results.Count : 0f;
            sb.AppendLine($"- Execuções: **{results.Count}**");
            sb.AppendLine($"- Aprovadas: **{passed}** ({Percent(passRate)}%)");
            sb.AppendLine($"- Com colisão: **{collisions}**");
            if (cpaCount > 0)
            {
                sb.AppendLine($"- CPA médio (menor por execução): **{Number(cpaSum / cpaCount)} m**");
                sb.AppendLine($"- Pior CPA: **{Number(worstCpa)} m** em _{worstCpaRun}_");
            }
            sb.AppendLine();

            sb.AppendLine("## Execuções");
            sb.AppendLine();
            sb.AppendLine("| # | Cenário | Semente | Resultado | Duração (s) | CPA mín. (m) | Alvo do CPA | Colisão |");
            sb.AppendLine("|---|---------|---------|-----------|-------------|--------------|-------------|---------|");

            foreach (ScenarioRunResult r in results)
            {
                string cpa = r.MinCpaMeters < float.MaxValue ? Number(r.MinCpaMeters) : "n/d";
                sb.AppendLine($"| {r.Index} | {r.ScenarioName} | {(r.HasSeed ? r.Seed.ToString(Invariant) : "-")} " +
                              $"| {(r.Passed ? "APROVADO" : "REPROVADO")} | {Number(r.DurationSeconds)} " +
                              $"| {cpa} | {(string.IsNullOrEmpty(r.MinCpaTarget) ? "-" : r.MinCpaTarget)} " +
                              $"| {(r.CollisionDetected ? r.CollidedWith : "-")} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Reprovações");
            sb.AppendLine();

            bool anyFailure = false;
            foreach (ScenarioRunResult r in results)
            {
                if (r.Passed) continue;
                anyFailure = true;

                sb.AppendLine($"### {r.Label}");
                if (r.HasSeed)
                    sb.AppendLine($"Reproduza com a semente `{r.Seed}`.");
                if (r.CollisionDetected)
                    sb.AppendLine($"- Colisão com **{r.CollidedWith}**.");

                foreach (TargetEncounterResult e in r.Encounters)
                    if (e.SafetyViolated)
                        sb.AppendLine($"- {e.Name}: CPA {Number(e.MinDistance)} m aos " +
                                      $"{Number(e.TimeOfMinDistance)} s (limite {Number(r.MinSafeDistanceMeters)} m).");

                if (!string.IsNullOrEmpty(r.MapPath))
                    sb.AppendLine($"- Mapa: `{r.MapPath}`");
                sb.AppendLine();
            }

            if (!anyFailure) sb.AppendLine("Nenhuma.");

            return sb.ToString();
        }

        // ---------------- formatting ----------------

        /// <summary>Formats a number with a dot decimal separator, whatever the locale.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The formatted text, or empty for an unmeasured value.</returns>
        static string Number(float value)
        {
            if (float.IsNaN(value) || value >= float.MaxValue) return "";
            return value.ToString("0.###", Invariant);
        }

        /// <summary>Formats a percentage with one decimal, always with a dot separator.</summary>
        /// <param name="value">The percentage.</param>
        /// <returns>The formatted text.</returns>
        static string Percent(float value) => value.ToString("0.0", Invariant);

        /// <summary>Formats a boolean as 1/0, which every spreadsheet and dataframe averages.</summary>
        /// <param name="value">The flag.</param>
        /// <returns>"1" or "0".</returns>
        static string Flag(bool value) => value ? "1" : "0";

        /// <summary>Joins the fields of a CSV row, quoting the ones that need it.</summary>
        /// <param name="separator">Column separator.</param>
        /// <param name="fields">The field values.</param>
        /// <returns>The formatted row.</returns>
        static string Row(string separator, params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(separator);
                sb.Append(Escape(fields[i] ?? "", separator));
            }
            return sb.ToString();
        }

        /// <summary>Quotes a CSV field when it carries the separator, a quote or a line break.</summary>
        /// <param name="value">The raw value.</param>
        /// <param name="separator">Column separator.</param>
        /// <returns>The field, quoted when needed.</returns>
        static string Escape(string value, string separator)
        {
            bool needsQuotes = value.Contains(separator) || value.Contains("\"")
                            || value.Contains("\n") || value.Contains("\r");

            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
