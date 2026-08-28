using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// Translates between the YAML configuration files and the objects the bench runs:
    /// <see cref="ScenarioDefinition"/> and the settings of <see cref="RandomScenarioGenerator"/>.
    ///
    /// Declaring a scenario in a text file — instead of only in a .asset created through
    /// the Editor — is what makes a test battery reviewable in a diff, shareable outside
    /// Unity and easy to vary: changing one number and re-running is a one-line edit.
    ///
    /// Every field has a default taken from the corresponding class, so a file only needs
    /// to state what differs from it.
    /// </summary>
    public static class ScenarioConfig
    {
        /// <summary>Default file extension of the configuration files.</summary>
        public const string EXTENSION = ".yaml";

        // ---------------- files ----------------

        /// <summary>
        /// Turns a path written relative to the project root into an absolute one.
        /// Absolute paths are kept as they are, so a suite can live outside the project.
        /// </summary>
        /// <param name="path">Path from the configuration file or the Inspector.</param>
        /// <returns>The absolute path, or null when the input is empty.</returns>
        public static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        /// <summary>
        /// Reads a single scenario from a YAML file.
        /// </summary>
        /// <param name="path">Path to the file, relative to the project root or absolute.</param>
        /// <returns>The scenario described by the file.</returns>
        public static ScenarioDefinition Load(string path)
        {
            string fullPath = ResolveProjectPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Arquivo de cenário não encontrado: {fullPath}");

            YamlNode root = YamlLite.Parse(File.ReadAllText(fullPath));

            // A scenario file may be written on its own or wrapped in a 'scenario:' key,
            // which is what an exported file looks like.
            YamlNode node = root.Child("scenario") ?? root;

            ScenarioDefinition scenario = FromYaml(node);
            if (string.IsNullOrEmpty(scenario.DisplayName))
                scenario.DisplayName = Path.GetFileNameWithoutExtension(fullPath);

            return scenario;
        }

        /// <summary>
        /// Writes a scenario to a YAML file, creating the folder when needed.
        /// </summary>
        /// <param name="scenario">The scenario to write.</param>
        /// <param name="path">Destination path, relative to the project root or absolute.</param>
        /// <returns>The absolute path written.</returns>
        public static string Save(ScenarioDefinition scenario, string path)
        {
            string fullPath = ResolveProjectPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var document = YamlNode.NewMapping();
            document.Add("scenario", ToYaml(scenario));

            File.WriteAllText(fullPath, YamlLite.Write(document));
            return fullPath;
        }

        // ---------------- scenario ----------------

        /// <summary>
        /// Builds a scenario from its YAML node.
        /// </summary>
        /// <param name="node">Mapping describing the scenario.</param>
        /// <returns>A new scenario instance, not saved as an asset.</returns>
        public static ScenarioDefinition FromYaml(YamlNode node)
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();

            scenario.DisplayName = node.Child("name", "displayName")?.AsString(scenario.DisplayName)
                                   ?? scenario.DisplayName;
            scenario.Description = node.GetString("description", scenario.Description);

            YamlNode usv = node.Child("usv");
            if (usv != null)
            {
                scenario.UsvStartXZ = usv.GetVector2("startXZ", scenario.UsvStartXZ);
                scenario.UsvStartHeadingDegrees =
                    usv.GetFloat("startHeadingDegrees", scenario.UsvStartHeadingDegrees);
                scenario.UsvCruiseSpeedKnots =
                    usv.GetFloat("cruiseSpeedKnots", scenario.UsvCruiseSpeedKnots);
                scenario.PublishWaypoints = usv.GetBool("publishWaypoints", scenario.PublishWaypoints);

                YamlNode waypoints = usv.Child("waypointOffsetsXZ", "waypoints");
                if (waypoints != null) scenario.WaypointOffsetsXZ = waypoints.AsVector2List();
            }

            YamlNode criteria = node.Child("criteria");
            if (criteria != null)
            {
                scenario.MaxDurationSeconds =
                    criteria.GetFloat("maxDurationSeconds", scenario.MaxDurationSeconds);
                scenario.MinSafeDistanceMeters =
                    criteria.GetFloat("minSafeDistanceMeters", scenario.MinSafeDistanceMeters);
            }

            scenario.Targets = new List<TargetSpec>();
            YamlNode targets = node.Child("targets");
            if (targets != null)
                foreach (YamlNode target in targets.Items)
                    scenario.Targets.Add(TargetFromYaml(target));

            return scenario;
        }

        /// <summary>
        /// Describes a scenario as a YAML node, ready to be written to a file.
        /// </summary>
        /// <param name="scenario">The scenario to describe.</param>
        /// <returns>The mapping node.</returns>
        public static YamlNode ToYaml(ScenarioDefinition scenario)
        {
            var node = YamlNode.NewMapping();
            node.Add("name", scenario.DisplayName);
            if (!string.IsNullOrEmpty(scenario.Description))
                node.Add("description", scenario.Description);

            var usv = YamlNode.NewMapping();
            usv.Add("startXZ", scenario.UsvStartXZ);
            usv.Add("startHeadingDegrees", scenario.UsvStartHeadingDegrees);
            usv.Add("cruiseSpeedKnots", scenario.UsvCruiseSpeedKnots);
            usv.Add("publishWaypoints", scenario.PublishWaypoints);
            usv.Add("waypointOffsetsXZ", YamlNode.FromVector2List(scenario.WaypointOffsetsXZ));
            node.Add("usv", usv);

            var criteria = YamlNode.NewMapping();
            criteria.Add("maxDurationSeconds", scenario.MaxDurationSeconds);
            criteria.Add("minSafeDistanceMeters", scenario.MinSafeDistanceMeters);
            node.Add("criteria", criteria);

            var targets = YamlNode.NewSequence();
            if (scenario.Targets != null)
                foreach (TargetSpec target in scenario.Targets)
                    targets.AddItem(TargetToYaml(target));
            node.Add("targets", targets);

            return node;
        }

        // ---------------- targets ----------------

        /// <summary>
        /// Builds one target from its YAML node.
        /// </summary>
        /// <param name="node">Mapping describing the target.</param>
        /// <returns>The target specification.</returns>
        static TargetSpec TargetFromYaml(YamlNode node)
        {
            var target = new TargetSpec();

            target.Name = node.GetString("name", target.Name);
            target.Behaviour = node.GetEnum("behaviour", target.Behaviour);
            target.StartOffsetXZ = node.GetVector2("startOffsetXZ", target.StartOffsetXZ);
            target.HeadingDegrees = node.GetFloat("headingDegrees", target.HeadingDegrees);
            target.SpeedKnots = node.GetFloat("speedKnots", target.SpeedKnots);
            target.Length = node.Child("lengthMeters", "length")?.AsFloat(target.Length) ?? target.Length;
            target.Beam = node.Child("beamMeters", "beam")?.AsFloat(target.Beam) ?? target.Beam;
            target.HullColor = node.GetColor("hullColor", target.HullColor);
            target.LoopRoute = node.GetBool("loopRoute", target.LoopRoute);

            YamlNode route = node.Child("routeOffsetsXZ", "route");
            target.RouteOffsetsXZ = route != null ? route.AsVector2List() : new List<Vector2>();

            return target;
        }

        /// <summary>
        /// Describes one target as a YAML node.
        /// </summary>
        /// <param name="target">The target to describe.</param>
        /// <returns>The mapping node.</returns>
        static YamlNode TargetToYaml(TargetSpec target)
        {
            var node = YamlNode.NewMapping();
            node.Add("name", target.Name);
            node.Add("behaviour", target.Behaviour.ToString());
            node.Add("startOffsetXZ", target.StartOffsetXZ);
            node.Add("headingDegrees", target.HeadingDegrees);
            node.Add("speedKnots", target.SpeedKnots);
            node.Add("lengthMeters", target.Length);
            node.Add("beamMeters", target.Beam);
            node.Add("hullColor", target.HullColor);

            // The route only matters for targets that follow one; writing it for every
            // target would bury the interesting numbers under empty lists.
            if (target.Behaviour == TargetBehaviour.Route)
            {
                node.Add("loopRoute", target.LoopRoute);
                node.Add("routeOffsetsXZ", YamlNode.FromVector2List(target.RouteOffsetsXZ));
            }

            return node;
        }

        // ---------------- random generator ----------------

        /// <summary>
        /// Applies the settings of a 'random' entry to the generator component. Only the
        /// keys present in the file are touched, so the Inspector values act as defaults.
        /// </summary>
        /// <param name="node">Mapping with the generator settings.</param>
        /// <param name="generator">The generator to configure.</param>
        public static void ApplyGeneratorSettings(YamlNode node, RandomScenarioGenerator generator)
        {
            if (node == null || generator == null) return;

            generator.AreaCenterXZ = node.GetVector2("areaCenterXZ", generator.AreaCenterXZ);
            generator.AreaRadius = node.GetFloat("areaRadius", generator.AreaRadius);
            generator.MinDistanceFromUsv = node.GetFloat("minDistanceFromUsv", generator.MinDistanceFromUsv);

            // Counts and ranges are written as [min, max] pairs: one line instead of two,
            // and it is impossible to set the minimum without seeing the maximum.
            YamlNode targetCount = node.Child("targetCount");
            if (targetCount != null)
            {
                Vector2 range = targetCount.AsVector2(new Vector2(generator.MinTargets, generator.MaxTargets));
                generator.MinTargets = Mathf.RoundToInt(range.x);
                generator.MaxTargets = Mathf.RoundToInt(range.y);
            }

            generator.SpeedRangeKnots = node.GetVector2("speedRangeKnots", generator.SpeedRangeKnots);
            generator.LengthRangeMeters = node.GetVector2("lengthRangeMeters", generator.LengthRangeMeters);
            generator.StaticTargetRatio = node.GetFloat("staticTargetRatio", generator.StaticTargetRatio);

            YamlNode routePoints = node.Child("routePoints");
            if (routePoints != null)
            {
                Vector2 range = routePoints.AsVector2(new Vector2(generator.MinRoutePoints, generator.MaxRoutePoints));
                generator.MinRoutePoints = Mathf.RoundToInt(range.x);
                generator.MaxRoutePoints = Mathf.RoundToInt(range.y);
            }

            generator.LegLengthMeters = node.GetVector2("legLengthMeters", generator.LegLengthMeters);
            generator.UsvStartXZ = node.GetVector2("usvStartXZ", generator.UsvStartXZ);
            generator.PublishUsvWaypoints = node.GetBool("publishUsvWaypoints", generator.PublishUsvWaypoints);
            generator.MaxDurationSeconds = node.GetFloat("maxDurationSeconds", generator.MaxDurationSeconds);
            generator.MinSafeDistanceMeters = node.GetFloat("minSafeDistanceMeters", generator.MinSafeDistanceMeters);
            generator.LandHeightThreshold = node.GetFloat("landHeightThreshold", generator.LandHeightThreshold);
            generator.WaterSearchAttempts = node.GetInt("waterSearchAttempts", generator.WaterSearchAttempts);
        }

        /// <summary>
        /// Describes the current generator settings as a YAML node, so a stress run can be
        /// written down exactly as it was executed.
        /// </summary>
        /// <param name="generator">The generator to describe.</param>
        /// <returns>The mapping node.</returns>
        public static YamlNode GeneratorToYaml(RandomScenarioGenerator generator)
        {
            var node = YamlNode.NewMapping();
            node.Add("seed", generator.Seed);
            node.Add("areaCenterXZ", generator.AreaCenterXZ);
            node.Add("areaRadius", generator.AreaRadius);
            node.Add("minDistanceFromUsv", generator.MinDistanceFromUsv);
            node.Add("targetCount", new Vector2(generator.MinTargets, generator.MaxTargets));
            node.Add("speedRangeKnots", generator.SpeedRangeKnots);
            node.Add("lengthRangeMeters", generator.LengthRangeMeters);
            node.Add("staticTargetRatio", generator.StaticTargetRatio);
            node.Add("routePoints", new Vector2(generator.MinRoutePoints, generator.MaxRoutePoints));
            node.Add("legLengthMeters", generator.LegLengthMeters);
            node.Add("usvStartXZ", generator.UsvStartXZ);
            node.Add("publishUsvWaypoints", generator.PublishUsvWaypoints);
            node.Add("maxDurationSeconds", generator.MaxDurationSeconds);
            node.Add("minSafeDistanceMeters", generator.MinSafeDistanceMeters);
            node.Add("landHeightThreshold", generator.LandHeightThreshold);
            node.Add("waterSearchAttempts", generator.WaterSearchAttempts);
            return node;
        }
    }
}
