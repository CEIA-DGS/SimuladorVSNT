using System.Collections.Generic;
using UnityEngine;
using MaritimeScenario.Boat;

namespace MaritimeScenario.Sensor
{
    /// <summary>
    /// Dynamic-object sensor mounted on the USV (layer 2 — perception). It does NOT
    /// create vessels: it DETECTS the ones that already exist (ground truth) within a
    /// range and keeps a list of CONTACTS (create/update/remove), like a real radar/AIS.
    ///
    /// • Periodic sweep (not every frame).
    /// • Optional occlusion: if land (an island) is on the line of sight, no detection.
    /// • Dictionary-based tracking: an existing contact is updated, a new one is created,
    ///   and a contact not seen for 'ForgetTime' seconds is removed.
    /// </summary>
    public class VesselSensor : MonoBehaviour
    {
        [Header("Sensor")]
        /// <summary>Detection range, in meters.</summary>
        public float Range = 3500f;             // m
        /// <summary>Time between scans, in seconds.</summary>
        public float ScanInterval = 0.25f;       // s (4x/s)
        /// <summary>Time without a sighting, in seconds, after which the contact is dropped.</summary>
        public float ForgetTime = 3f;            // s without a sighting -> remove
        /// <summary>Whether land between the sensor and the target blocks detection.</summary>
        public bool UseOcclusion = true;         // land blocks detection

        // Keyed by the vessel reference, not by GetInstanceID (obsolete in Unity 6.5).
        readonly Dictionary<DynamicVessel, Contact> contacts = new();
        readonly List<DynamicVessel> pendingRemoval = new();
        float nextScanTime;

        /// <summary>Contacts currently tracked by the sensor (read-only).</summary>
        public IReadOnlyCollection<Contact> Contacts => contacts.Values;

        /// <summary>
        /// Triggers the sweep at fixed intervals, to approximate a real radar and save
        /// processing. Runs on the physics step so the sweep instants are reproducible
        /// across runs (a requirement of the deterministic test scenarios).
        /// </summary>
        void FixedUpdate()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + ScanInterval;
            Scan();
        }

        /// <summary>
        /// Iterates over the fleet, detects the vessels within range (respecting
        /// occlusion), creates or updates each contact, and drops the lost ones.
        /// </summary>
        void Scan()
        {
            Vector3 origin = transform.position;
            var fleet = Object.FindObjectsByType<DynamicVessel>();

            foreach (var v in fleet)
            {
                Vector3 pos = v.transform.position;
                if (Vector3.Distance(origin, pos) > Range) continue;
                if (UseOcclusion && IsOccluded(origin, pos)) continue;

                if (!contacts.TryGetValue(v, out var c))
                {
                    c = new Contact { Id = v.GetHashCode(), FirstSeen = Time.time };
                    contacts[v] = c;
                }
                c.Position = pos;
                c.Velocity = v.CurrentVelocity;
                c.Heading = v.HeadingDegrees;
                c.Length = v.Length;
                c.LastSeen = Time.time;
            }

            // Remove lost contacts (not seen for 'ForgetTime' seconds) or destroyed ones.
            pendingRemoval.Clear();
            foreach (var kv in contacts)
                if (kv.Key == null || Time.time - kv.Value.LastSeen > ForgetTime) pendingRemoval.Add(kv.Key);
            foreach (var v in pendingRemoval) contacts.Remove(v);
        }

        /// <summary>
        /// Returns whether the target is hidden by land on the line of sight. The line is
        /// traced slightly above the water; since the only collider in the scene is the
        /// terrain, a Linecast hit means there is an island between the sensor and target.
        /// </summary>
        /// <param name="a">Sensor (USV) position.</param>
        /// <param name="b">Target position.</param>
        /// <returns>True if land blocks the line of sight.</returns>
        bool IsOccluded(Vector3 a, Vector3 b)
        {
            var a1 = new Vector3(a.x, 1.5f, a.z);
            var b1 = new Vector3(b.x, 1.5f, b.z);
            return Physics.Linecast(a1, b1);
        }

        /// <summary>Draws the sensor range in the editor when the object is selected.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Range);
        }
    }
}
