using System.Collections.Generic;
using UnityEngine;

namespace MaritimeScenario.Chart
{
    /// <summary>
    /// Holds, in the scene, the vector "source of truth" of the chart (LNDARE/DEPARE
    /// polygons and buoy/rock points) generated together with the 3D scenario. It is
    /// this list, not the 3D mesh, that represents the nautical chart and that should be
    /// exported/used by the navigation and perception modules (Digital Twin report:
    /// "vector to navigate, mesh to show on screen").
    /// </summary>
    public class ChartFeatureSource : MonoBehaviour
    {
        /// <summary>Area features of the chart, such as land and depth areas.</summary>
        public List<ChartFeature> Polygons = new();
        /// <summary>Point features of the chart, such as buoys and rocks.</summary>
        public List<ChartPointFeature> Points = new();

        [Header("Desenho de depuração (Scene view)")]
        /// <summary>Whether the features are outlined in the Scene view.</summary>
        public bool DrawGizmos = true;

        /// <summary>
        /// Draws the chart polygons (colored by class/depth) and points in the Scene
        /// view, as a debug overlay of the vector source of truth.
        /// </summary>
        void OnDrawGizmos()
        {
            if (!DrawGizmos) return;

            foreach (var f in Polygons)
            {
                Gizmos.color = f.ObjectClass == ObjClass.LNDARE
                    ? new Color(0.3f, 0.85f, 0.35f)
                    : Color.Lerp(new Color(0.4f, 0.85f, 1f), new Color(0.05f, 0.15f, 0.55f),
                                 Mathf.InverseLerp(0f, 20f, f.DRVAL2));

                DrawRing(f.RingXZ);
                DrawRing(f.HoleXZ);
            }

            Gizmos.color = Color.yellow;
            foreach (var p in Points)
                Gizmos.DrawSphere(new Vector3(p.PositionXZ.x, 1f, p.PositionXZ.y), 1.2f);
        }

        /// <summary>Draws the closed outline of a polygon ring, slightly above the water.</summary>
        /// <param name="r">Ring vertices in the local X,Z plane.</param>
        static void DrawRing(List<Vector2> r)
        {
            if (r == null || r.Count < 2) return;
            for (int i = 0; i < r.Count; i++)
            {
                Vector3 a = new(r[i].x, 0.5f, r[i].y);
                Vector3 b = new(r[(i + 1) % r.Count].x, 0.5f, r[(i + 1) % r.Count].y);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
