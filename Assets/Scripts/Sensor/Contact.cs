using UnityEngine;

namespace MaritimeScenario.Sensor
{
    /// <summary>
    /// A sensor CONTACT — what the USV "sees", separated from the real object
    /// (ground truth). Stores the last perceived pose/velocity and when it was
    /// last seen. It is the basis of what the PRISMA perception would publish
    /// as a tracked target (tracked_geo_target).
    /// </summary>
    public class Contact
    {
        /// <summary>Identifier kept stable while the contact remains tracked.</summary>
        public int Id;
        /// <summary>Last known position, in scene coordinates.</summary>
        public Vector3 Position;
        /// <summary>Velocity estimated from consecutive sightings.</summary>
        public Vector3 Velocity;
        /// <summary>Heading in degrees, derived from the estimated velocity.</summary>
        public float Heading;
        /// <summary>Hull length of the contact, in meters.</summary>
        public float Length;
        /// <summary>Simulated time of the first sighting, in seconds.</summary>
        public float FirstSeen;
        /// <summary>Simulated time of the most recent sighting, in seconds.</summary>
        public float LastSeen;

        /// <summary>True while the contact is recent (first seen less than 1.5 s ago).</summary>
        public bool IsNew => Time.time - FirstSeen < 1.5f;

        /// <summary>Time in seconds since the contact was last seen.</summary>
        public float Age => Time.time - LastSeen;
    }
}
