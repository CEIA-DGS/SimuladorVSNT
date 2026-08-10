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
        public int Id;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Heading;
        public float Length;
        public float FirstSeen;
        public float LastSeen;

        /// <summary>True while the contact is recent (first seen less than 1.5 s ago).</summary>
        public bool IsNew => Time.time - FirstSeen < 1.5f;

        /// <summary>Time in seconds since the contact was last seen.</summary>
        public float Age => Time.time - LastSeen;
    }
}
