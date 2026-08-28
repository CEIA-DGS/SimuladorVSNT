using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// What was measured against a single target during a run.
    /// </summary>
    public class TargetEncounterResult
    {
        /// <summary>Target name, as defined in the scenario.</summary>
        public string Name;

        /// <summary>Smallest distance (m) ever observed between the USV and this target.</summary>
        public float MinDistance = float.MaxValue;

        /// <summary>Simulated time (s) at which the minimum distance happened — the observed TCPA.</summary>
        public float TimeOfMinDistance;

        /// <summary>True when the minimum distance violated the scenario's safety margin.</summary>
        public bool SafetyViolated;

        /// <summary>
        /// Distance (m) below which the hulls are considered to be touching. Derived from
        /// the vessel dimensions, so it scales with the size of each target.
        /// </summary>
        public float ContactDistance;

        /// <summary>Where the USV was when the closest approach happened.</summary>
        public Vector3 UsvPositionAtMinDistance;

        /// <summary>Where the target was when the closest approach happened.</summary>
        public Vector3 TargetPositionAtMinDistance;

        /// <summary>Path actually travelled by the target during the run.</summary>
        public readonly List<Vector3> Track = new();
    }

    /// <summary>
    /// Collects the objective measurements of a scenario run, so different navigation
    /// algorithms can be compared on the same numbers instead of "it looked fine".
    ///
    /// The key figure is the observed CPA (Closest Point of Approach): the smallest
    /// distance actually reached against each target, and when it happened. Unlike a
    /// predicted CPA, this one is measured from what really occurred in the run.
    /// </summary>
    public class ScenarioMetrics
    {
        readonly Dictionary<Transform, TargetEncounterResult> results = new();
        readonly float minSafeDistance;

        /// <summary>
        /// Only every Nth physics step is stored in the tracks. At 50 Hz a 300 s run would
        /// otherwise pile up 15.000 points per vessel, which is far more detail than a map
        /// can show.
        /// </summary>
        const int TRACK_SAMPLE_INTERVAL = 25;

        int stepCount;

        /// <summary>Path actually travelled by the USV during the run.</summary>
        public List<Vector3> UsvTrack { get; } = new();

        /// <summary>Simulated seconds elapsed since the run started.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>True when the USV physically collided with something during the run.</summary>
        public bool CollisionDetected { get; private set; }

        /// <summary>Name of whatever the USV collided with, when a collision happened.</summary>
        public string CollidedWith { get; private set; }

        /// <summary>True when the USV reached the last waypoint of its route.</summary>
        public bool MissionCompleted { get; set; }

        /// <summary>Per-target results, in the order the targets were registered.</summary>
        public IEnumerable<TargetEncounterResult> Results => results.Values;

        /// <summary>
        /// Creates a metrics collector for a run.
        /// </summary>
        /// <param name="minSafeDistanceMeters">Distance below which an approach counts as a safety violation.</param>
        public ScenarioMetrics(float minSafeDistanceMeters)
        {
            minSafeDistance = minSafeDistanceMeters;
        }

        /// <summary>
        /// Registers a target to be tracked during the run.
        /// </summary>
        /// <param name="target">The target's transform.</param>
        /// <param name="name">Display name used in the report.</param>
        /// <param name="contactDistance">Distance (m) below which the hulls count as touching.</param>
        public void RegisterTarget(Transform target, string name, float contactDistance)
        {
            if (target == null || results.ContainsKey(target)) return;
            results[target] = new TargetEncounterResult
            {
                Name = name,
                ContactDistance = contactDistance
            };
        }

        /// <summary>
        /// Samples the current distance to every registered target and keeps the minimum.
        /// Call once per physics step so the sampling rate is reproducible.
        /// </summary>
        /// <param name="usv">The USV transform.</param>
        /// <param name="deltaTime">Physics step duration, in seconds.</param>
        public void Sample(Transform usv, float deltaTime)
        {
            ElapsedSeconds += deltaTime;
            if (usv == null) return;

            bool recordTrack = (stepCount++ % TRACK_SAMPLE_INTERVAL) == 0;
            if (recordTrack) UsvTrack.Add(usv.position);

            foreach (var pair in results)
            {
                Transform target = pair.Key;
                if (target == null) continue;

                if (recordTrack) pair.Value.Track.Add(target.position);

                // Horizontal distance only: vertical bobbing from the waves is irrelevant here.
                Vector3 a = usv.position;
                Vector3 b = target.position;
                float distance = new Vector2(a.x - b.x, a.z - b.z).magnitude;

                TargetEncounterResult result = pair.Value;
                if (distance < result.MinDistance)
                {
                    result.MinDistance = distance;
                    result.TimeOfMinDistance = ElapsedSeconds;
                    result.SafetyViolated = distance < minSafeDistance;

                    // Kept so the map can mark exactly where the closest approach happened.
                    result.UsvPositionAtMinDistance = a;
                    result.TargetPositionAtMinDistance = b;
                }

                // Contact is measured by distance instead of physics colliders on purpose:
                // giving the targets colliders would make the sensor's occlusion linecast
                // hit them and report every target as hidden.
                if (distance < result.ContactDistance)
                    RecordCollision(result.Name);
            }
        }

        /// <summary>Records that the USV hit something.</summary>
        /// <param name="otherName">Name of the object that was hit.</param>
        public void RecordCollision(string otherName)
        {
            if (CollisionDetected) return;
            CollisionDetected = true;
            CollidedWith = otherName;
        }

        /// <summary>
        /// True when the run met the scenario's criteria: no collision and no approach
        /// closer than the safety margin.
        /// </summary>
        public bool Passed
        {
            get
            {
                if (CollisionDetected) return false;
                foreach (var result in results.Values)
                    if (result.SafetyViolated) return false;
                return true;
            }
        }

        /// <summary>
        /// Builds a human-readable report of the run, for the Console or a log file.
        /// </summary>
        /// <param name="scenarioName">Name of the scenario that was run.</param>
        /// <returns>The formatted report.</returns>
        public string BuildReport(string scenarioName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== Cenário: {scenarioName} =====");
            sb.AppendLine($"Resultado: {(Passed ? "APROVADO" : "REPROVADO")}");
            sb.AppendLine($"Duração: {ElapsedSeconds:F1} s");
            sb.AppendLine($"Rota concluída: {(MissionCompleted ? "sim" : "não")}");
            sb.AppendLine($"Distância mínima de segurança exigida: {minSafeDistance:F0} m");

            if (CollisionDetected)
                sb.AppendLine($"COLISÃO com: {CollidedWith}");

            foreach (var result in results.Values)
            {
                string distance = result.MinDistance < float.MaxValue
                    ? $"{result.MinDistance:F1} m"
                    : "n/d";
                string flag = result.SafetyViolated ? "  <-- ABAIXO DO LIMITE" : "";
                sb.AppendLine($"  {result.Name}: CPA {distance} aos {result.TimeOfMinDistance:F1} s{flag}");
            }

            return sb.ToString();
        }
    }
}
