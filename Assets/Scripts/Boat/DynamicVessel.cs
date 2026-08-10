using System.Collections.Generic;
using UnityEngine;

namespace MaritimeScenario.Boat
{
    /// <summary>
    /// DYNAMIC vessel (moving obstacle): travels a smooth looping path defined by
    /// waypoints and interpolated by a Catmull-Rom spline (it passes through the points,
    /// with feasible curves — neither a straight line nor an impossible turn).
    ///
    /// It exposes the "state vector" that the requirements report asks for on dynamic
    /// objects: pose (transform), velocity and heading — the basis for the future
    /// CPA/collision computation. The spline is implemented in-house, so it does not
    /// depend on an external package and moves/tests even without com.unity.splines.
    /// </summary>
    public class DynamicVessel : MonoBehaviour
    {
        public List<Vector3> Waypoints = new();
        public float Speed = 5f;   // m/s
        public bool Loop = true;
        public float WaterHeight = 0f;
        [Tooltip("Distância inicial ao longo da rota (m) — espalha vários barcos na mesma rota.")]
        public float InitialDistance = 0f;

        [Header("Dimensões (vetor de estado / colisão)")]
        public float Length = 20f;
        public float Beam = 6f;
        public string Kind = "vessel";

        // ---- exposed dynamic state ----
        public Vector3 CurrentVelocity { get; private set; }
        public float HeadingDegrees { get; private set; }

        Vector3[] samples;
        float[] cumulativeDistance;
        float totalLength;
        float distance;
        Vector3 previousPosition;

        /// <summary>
        /// Builds the spline sample table and places the vessel at its starting offset
        /// along the route (so several vessels can be spread over the same path).
        /// </summary>
        void Start()
        {
            BuildTable();
            if (samples != null && samples.Length > 0)
            {
                distance = totalLength > 0.01f ? Mathf.Repeat(InitialDistance, totalLength) : 0f;
                var p = PositionAtDistance(distance); p.y = WaterHeight;
                transform.position = p;
                previousPosition = p;
            }
        }

        /// <summary>
        /// Samples the Catmull-Rom spline through the waypoints and precomputes the
        /// cumulative arc length, so the vessel can move at constant speed along it.
        /// </summary>
        void BuildTable()
        {
            if (Waypoints == null || Waypoints.Count < 2) return;
            int n = Waypoints.Count;
            const int samplesPerSegment = 24;
            var pts = new List<Vector3>();
            int segs = Loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = Waypoints[(i - 1 + n) % n];
                Vector3 p1 = Waypoints[i % n];
                Vector3 p2 = Waypoints[(i + 1) % n];
                Vector3 p3 = Waypoints[(i + 2) % n];
                for (int j = 0; j < samplesPerSegment; j++)
                    pts.Add(CatmullRom(p0, p1, p2, p3, j / (float)samplesPerSegment));
            }
            if (!Loop) pts.Add(Waypoints[n - 1]);
            samples = pts.ToArray();

            cumulativeDistance = new float[samples.Length];
            for (int i = 1; i < samples.Length; i++)
                cumulativeDistance[i] = cumulativeDistance[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
            float extra = Loop ? Vector3.Distance(samples[samples.Length - 1], samples[0]) : 0f;
            totalLength = cumulativeDistance[samples.Length - 1] + extra;
        }

        /// <summary>Evaluates a Catmull-Rom spline segment at parameter t in [0, 1].</summary>
        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Advances the vessel along the path at its speed, orients it along the tangent,
        /// and updates the exposed velocity and heading (the state vector).
        /// </summary>
        void Update()
        {
            if (samples == null || samples.Length < 2 || totalLength <= 0.01f) return;

            distance += Speed * Time.deltaTime;
            distance = Loop ? Mathf.Repeat(distance, totalLength) : Mathf.Clamp(distance, 0f, totalLength);

            Vector3 pos = PositionAtDistance(distance);
            pos.y = WaterHeight;
            transform.position = pos;

            Vector3 delta = pos - previousPosition;
            Vector3 flatDir = new Vector3(delta.x, 0f, delta.z);
            if (flatDir.sqrMagnitude > 1e-4f)
            {
                var target = Quaternion.LookRotation(flatDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 2f);
                HeadingDegrees = target.eulerAngles.y;
            }
            CurrentVelocity = delta / Mathf.Max(Time.deltaTime, 1e-4f);
            previousPosition = pos;
        }

        /// <summary>
        /// Returns the world position at arc-length distance d along the path,
        /// interpolating between the two nearest samples.
        /// </summary>
        /// <param name="d">Distance along the path, in meters.</param>
        /// <returns>World position at that distance.</returns>
        Vector3 PositionAtDistance(float d)
        {
            for (int i = 1; i < samples.Length; i++)
                if (cumulativeDistance[i] >= d)
                {
                    float t = Mathf.InverseLerp(cumulativeDistance[i - 1], cumulativeDistance[i], d);
                    return Vector3.Lerp(samples[i - 1], samples[i], t);
                }
            if (Loop)
            {
                float segLen = totalLength - cumulativeDistance[samples.Length - 1];
                float t = segLen > 0.001f ? (d - cumulativeDistance[samples.Length - 1]) / segLen : 0f;
                return Vector3.Lerp(samples[samples.Length - 1], samples[0], Mathf.Clamp01(t));
            }
            return samples[samples.Length - 1];
        }

        /// <summary>Draws the waypoints and route in the editor when the object is selected.</summary>
        void OnDrawGizmosSelected()
        {
            if (Waypoints == null || Waypoints.Count < 2) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            for (int i = 0; i < Waypoints.Count; i++)
            {
                Gizmos.DrawSphere(Waypoints[i], 25f);
                Gizmos.DrawLine(Waypoints[i], Waypoints[(i + 1) % Waypoints.Count]);
            }
        }
    }
}
