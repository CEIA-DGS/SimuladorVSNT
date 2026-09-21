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

        /// <summary>How many separate times the vehicle entered this target's risk zone.</summary>
        public int RiskApproaches;

        /// <summary>How many separate times the hulls touched.</summary>
        public int Contacts;

        /// <summary>
        /// Whether the vehicle is inside the risk zone right now. Kept so an approach is
        /// counted once when it begins, instead of once per physics step while it lasts.
        /// </summary>
        public bool InRiskZone;

        /// <summary>Whether the hulls are touching right now, for the same reason as <see cref="InRiskZone"/>.</summary>
        public bool InContact;
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

        /// <summary>
        /// A risk approach only ends once the gap widens past the safety distance by this
        /// factor. Without the margin a vehicle skirting the threshold would open and
        /// close the same approach over and over, inflating the count.
        /// </summary>
        const float RISK_ZONE_EXIT_FACTOR = 1.2f;

        int stepCount;
        Vector3 lastUsvPosition;
        bool hasLastUsvPosition;

        /// <summary>Path actually travelled by the USV during the run.</summary>
        public List<Vector3> UsvTrack { get; } = new();

        /// <summary>Everything worth reporting that happened during the run, in order.</summary>
        public MissionEventLog Events { get; } = new();

        /// <summary>
        /// Horizontal distance travelled by the USV, in meters. Accumulated step by step
        /// rather than from the sampled track, which would cut corners and under-report it.
        /// </summary>
        public float DistanceTravelledMeters { get; private set; }

        /// <summary>
        /// How many times the USV entered the risk zone of some target. Counts events, so
        /// the same target approached twice counts twice.
        /// </summary>
        public int RiskApproachCount { get; private set; }

        /// <summary>How many separate contacts happened during the run.</summary>
        public int CollisionCount { get; private set; }

        /// <summary>How many times the USV moved on to the next leg of its route.</summary>
        public int WaypointTransitions { get; private set; }

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

            AccumulateDistance(usv.position);

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

                TrackRiskZone(result, distance, a);
                TrackContact(result, distance, a);
            }
        }

        /// <summary>
        /// Adds the ground distance covered since the previous step. The first step only
        /// seeds the reference position, so placing the USV at its start pose is not
        /// counted as travel.
        /// </summary>
        /// <param name="usvPosition">Current position of the USV.</param>
        void AccumulateDistance(Vector3 usvPosition)
        {
            if (hasLastUsvPosition)
            {
                DistanceTravelledMeters += new Vector2(
                    usvPosition.x - lastUsvPosition.x,
                    usvPosition.z - lastUsvPosition.z).magnitude;
            }

            lastUsvPosition = usvPosition;
            hasLastUsvPosition = true;
        }

        /// <summary>
        /// Opens and closes risk approaches against one target. The approach is counted
        /// when it begins and only ends once the gap widens past the exit margin, so a
        /// single close pass is one event instead of one per physics step.
        /// </summary>
        /// <param name="result">Running result of the target.</param>
        /// <param name="distance">Current distance to the target, in meters.</param>
        /// <param name="usvPosition">Current position of the USV.</param>
        void TrackRiskZone(TargetEncounterResult result, float distance, Vector3 usvPosition)
        {
            if (!result.InRiskZone && distance < minSafeDistance)
            {
                result.InRiskZone = true;
                result.RiskApproaches++;
                RiskApproachCount++;

                Events.Record(ElapsedSeconds, MissionEventType.RiskApproach, usvPosition,
                              result.Name, distance,
                              "abaixo da distancia de seguranca");
                return;
            }

            if (result.InRiskZone && distance > minSafeDistance * RISK_ZONE_EXIT_FACTOR)
            {
                result.InRiskZone = false;
                Events.Record(ElapsedSeconds, MissionEventType.RiskCleared, usvPosition,
                              result.Name, distance, "afastou-se da zona de risco");
            }
        }

        /// <summary>
        /// Opens and closes contacts against one target, on the same principle as the risk
        /// zone. Contact is measured by distance instead of physics colliders on purpose:
        /// giving the targets colliders would make the sensor's occlusion linecast hit them
        /// and report every target as hidden.
        /// </summary>
        /// <param name="result">Running result of the target.</param>
        /// <param name="distance">Current distance to the target, in meters.</param>
        /// <param name="usvPosition">Current position of the USV.</param>
        void TrackContact(TargetEncounterResult result, float distance, Vector3 usvPosition)
        {
            if (!result.InContact && distance < result.ContactDistance)
            {
                result.InContact = true;
                result.Contacts++;
                RecordCollision(result.Name, distance, usvPosition);
                return;
            }

            if (result.InContact && distance > result.ContactDistance * RISK_ZONE_EXIT_FACTOR)
                result.InContact = false;
        }

        /// <summary>Records that the USV hit something.</summary>
        /// <param name="otherName">Name of the object that was hit.</param>
        /// <param name="distanceMeters">Gap between the hulls when the contact was detected.</param>
        /// <param name="usvPosition">Position of the USV at the moment of contact.</param>
        public void RecordCollision(string otherName, float distanceMeters = 0f, Vector3 usvPosition = default)
        {
            CollisionCount++;

            // The aggregate name keeps the first thing hit, which is the one that explains
            // the run; a later contact is usually a consequence of the first.
            if (!CollisionDetected)
            {
                CollisionDetected = true;
                CollidedWith = otherName;
            }

            Events.Record(ElapsedSeconds, MissionEventType.Collision, usvPosition,
                          otherName, distanceMeters, "cascos em contato");
        }

        /// <summary>
        /// Records that the route advanced to its next leg. Driven from outside because the
        /// transition itself is decided by the waypoint manager.
        /// </summary>
        /// <param name="usvPosition">Position of the USV when the leg changed.</param>
        /// <param name="waypointIndex">Index of the waypoint that was reached.</param>
        public void RecordWaypointTransition(Vector3 usvPosition, int waypointIndex)
        {
            WaypointTransitions++;
            Events.Record(ElapsedSeconds, MissionEventType.WaypointReached, usvPosition,
                          detail: "waypoint " + waypointIndex);
        }

        /// <summary>Records the start of the mission, which anchors the log at time zero.</summary>
        /// <param name="usvPosition">Start position of the USV.</param>
        /// <param name="detail">Free text with the initial conditions of the run.</param>
        public void RecordMissionStart(Vector3 usvPosition, string detail = "")
        {
            Events.Record(0f, MissionEventType.MissionStart, usvPosition, detail: detail);
        }

        /// <summary>Records how the mission ended, either by finishing the route or by timing out.</summary>
        /// <param name="usvPosition">Position of the USV when the run ended.</param>
        public void RecordMissionEnd(Vector3 usvPosition)
        {
            MissionEventType type = MissionCompleted
                ? MissionEventType.MissionComplete
                : MissionEventType.MissionTimeout;

            // The distance column means "the gap this event is about", so the total
            // travelled goes in the text instead of overloading it with a second meaning.
            string outcome = MissionCompleted ? "rota concluida" : "tempo limite atingido";

            Events.Record(ElapsedSeconds, type, usvPosition,
                          detail: $"{outcome}, {DistanceTravelledMeters:0} m percorridos");
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
            sb.AppendLine($"Distância percorrida: {DistanceTravelledMeters:F0} m");
            sb.AppendLine($"Trocas de waypoint: {WaypointTransitions}");
            sb.AppendLine($"Aproximações de risco: {RiskApproachCount}");
            sb.AppendLine($"Colisões: {CollisionCount}");
            sb.AppendLine($"Distância mínima de segurança exigida: {minSafeDistance:F0} m");

            if (CollisionDetected)
                sb.AppendLine($"Primeira colisão com: {CollidedWith}");

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
