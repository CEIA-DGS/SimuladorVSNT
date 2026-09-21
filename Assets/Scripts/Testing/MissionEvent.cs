using System.Collections.Generic;
using UnityEngine;
using MaritimeScenario.Real;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// What happened at one instant of a mission.
    /// </summary>
    public enum MissionEventType
    {
        /// <summary>The mission started and the route was handed to the vehicle.</summary>
        MissionStart,

        /// <summary>The vehicle crossed into the next leg of its route.</summary>
        WaypointReached,

        /// <summary>The vehicle came closer to a target than the safety distance.</summary>
        RiskApproach,

        /// <summary>The vehicle moved back out of a target's risk zone.</summary>
        RiskCleared,

        /// <summary>The hulls touched.</summary>
        Collision,

        /// <summary>The last waypoint of the route was reached.</summary>
        MissionComplete,

        /// <summary>The run hit its time limit before finishing the route.</summary>
        MissionTimeout
    }

    /// <summary>
    /// One entry of the mission log: what happened, when, where and against whom.
    ///
    /// The position is recorded both in scene coordinates and in latitude/longitude, so
    /// the log can be cross-referenced with data recorded outside the simulator, where
    /// geographic coordinates are the common ground.
    /// </summary>
    public struct MissionEvent
    {
        /// <summary>Simulated seconds since the run started.</summary>
        public float TimeSeconds;

        /// <summary>What happened.</summary>
        public MissionEventType Type;

        /// <summary>Target involved, empty for events that do not involve one.</summary>
        public string Target;

        /// <summary>Position of the vehicle in scene coordinates, in meters.</summary>
        public Vector3 UsvPosition;

        /// <summary>Latitude of the vehicle in decimal degrees, when the scene is georeferenced.</summary>
        public double Latitude;

        /// <summary>Longitude of the vehicle in decimal degrees, when the scene is georeferenced.</summary>
        public double Longitude;

        /// <summary>Distance in meters that the event refers to, such as the gap to the target.</summary>
        public float DistanceMeters;

        /// <summary>Free text with whatever else is worth reading in the log.</summary>
        public string Detail;
    }

    /// <summary>
    /// Ordered log of everything worth reporting about a mission.
    ///
    /// Aggregate numbers say how bad a run was; the log says where and when it went
    /// wrong, which is what turns a failed run into something that can be diagnosed.
    /// </summary>
    public class MissionEventLog
    {
        readonly List<MissionEvent> events = new();

        /// <summary>The events, in the order they happened.</summary>
        public IReadOnlyList<MissionEvent> Events => events;

        /// <summary>
        /// Appends an event to the log, filling in the geographic position when the scene
        /// carries a georeference.
        /// </summary>
        /// <param name="timeSeconds">Simulated seconds since the run started.</param>
        /// <param name="type">What happened.</param>
        /// <param name="usvPosition">Position of the vehicle in scene coordinates.</param>
        /// <param name="target">Target involved, when there is one.</param>
        /// <param name="distanceMeters">Distance the event refers to, when it has one.</param>
        /// <param name="detail">Free text with extra context.</param>
        public void Record(
            float timeSeconds,
            MissionEventType type,
            Vector3 usvPosition,
            string target = "",
            float distanceMeters = 0f,
            string detail = "")
        {
            double latitude = 0.0;
            double longitude = 0.0;

            // A scenario can run on the fictional map, which has no UTM georeference; the
            // log stays valid either way, only without the geographic columns filled in.
            GeoReferenceUTM geoRef = GeoReferenceUTM.Instance;
            if (geoRef != null)
                (latitude, longitude) = geoRef.LocalToGeographic(usvPosition);

            events.Add(new MissionEvent
            {
                TimeSeconds = timeSeconds,
                Type = type,
                Target = target ?? "",
                UsvPosition = usvPosition,
                Latitude = latitude,
                Longitude = longitude,
                DistanceMeters = distanceMeters,
                Detail = detail ?? ""
            });
        }

        /// <summary>Counts how many events of a given type were recorded.</summary>
        /// <param name="type">The type to count.</param>
        /// <returns>The number of matching events.</returns>
        public int Count(MissionEventType type)
        {
            int total = 0;
            foreach (MissionEvent entry in events)
                if (entry.Type == type) total++;
            return total;
        }
    }
}
