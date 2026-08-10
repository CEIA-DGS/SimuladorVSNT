using UnityEngine;

namespace MaritimeScenario.Water
{
    /// <summary>
    /// Single wave formula (sum of sines), used both by the visual water mesh
    /// (WaterAnimator) and by the vessel buoyancy (BoatController) — so the boat
    /// rises and falls exactly in line with what is seen on the water.
    /// </summary>
    public static class WaveUtil
    {
        /// <summary>
        /// Evaluates the wave height at a world (x, z) point and time.
        /// </summary>
        /// <param name="x">World X coordinate.</param>
        /// <param name="z">World Z coordinate.</param>
        /// <param name="time">Current time, in seconds.</param>
        /// <param name="amplitude">Wave amplitude.</param>
        /// <param name="scale">Spatial frequency of the wave.</param>
        /// <param name="speed">Wave animation speed.</param>
        /// <returns>The wave height at that point.</returns>
        public static float Height(float x, float z, float time, float amplitude, float scale, float speed)
        {
            float t = time * speed;
            float wave = Mathf.Sin((x + z) * scale + t)
                        + Mathf.Sin((x - z) * scale * 1.7f + t * 1.3f)
                        + Mathf.Sin((x * 0.6f + z * 1.3f) * scale * 2.3f + t * 0.7f) * 0.5f;
            return wave * amplitude * 0.65f; // 0.65 offsets the extra amplitude of the 3rd term
        }
    }
}
