using UnityEngine;

namespace MaritimeScenario.Water
{
    /// <summary>
    /// Smoothly animates the vertices of a flat water mesh, using the same wave formula
    /// (WaveUtil) as the vessel buoyancy — so the boat rises and falls in sync with what
    /// is seen. It has no physical role; the real hydrodynamic model is left to the rest
    /// of the simulator.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class WaterAnimator : MonoBehaviour
    {
        public float Amplitude = 0.15f;
        public float Speed = 0.6f;
        public float Scale = 0.05f;

        Mesh mesh;
        Vector3[] baseVertices;
        Vector3[] vertices;

        /// <summary>Caches the mesh instance and its base vertices to animate from.</summary>
        void Start()
        {
            mesh = GetComponent<MeshFilter>().mesh; // accessing .mesh auto-instantiates a copy
            baseVertices = mesh.vertices;
            vertices = new Vector3[baseVertices.Length];
        }

        /// <summary>Displaces each vertex vertically by the wave height and updates the mesh.</summary>
        void Update()
        {
            if (mesh == null) return;
            for (int i = 0; i < baseVertices.Length; i++)
            {
                var v = baseVertices[i];
                v.y = WaveUtil.Height(v.x, v.z, Time.time, Amplitude, Scale, Speed);
                vertices[i] = v;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
        }
    }
}
