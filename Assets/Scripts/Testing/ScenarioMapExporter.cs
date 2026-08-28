using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// Draws the result of a scenario run as a top-down PNG map, so a test can be read at
    /// a glance instead of only through the numbers in the Console.
    ///
    /// The background reuses the depth-coloured chart image that the scenario builder
    /// already produces for the tactical chart, which puts the run in its real geographic
    /// context — the coastline and the depth bands of the bay.
    ///
    /// On top of it the map shows:
    ///  • the planned route of the USV (dashed) versus the path it actually travelled;
    ///  • where each target started and the path it followed;
    ///  • a marker at each closest approach (CPA), red when it broke the safety margin.
    /// </summary>
    public static class ScenarioMapExporter
    {
        static readonly Color RoutePlannedColor = new Color(0.15f, 0.95f, 1f);
        static readonly Color UsvTrackColor = new Color(1f, 0.95f, 0.25f);
        static readonly Color TargetTrackColor = new Color(1f, 0.45f, 0.15f);
        static readonly Color CpaOkColor = new Color(0.30f, 1f, 0.40f);
        static readonly Color CpaViolationColor = new Color(1f, 0.15f, 0.15f);

        /// <summary>
        /// Renders and saves the map of a run.
        /// </summary>
        /// <param name="scenario">The scenario that was executed.</param>
        /// <param name="metrics">The measurements collected during the run.</param>
        /// <param name="chartImage">Depth-coloured chart of the whole area (from the tactical chart).</param>
        /// <param name="worldSize">Size in meters that the chart image covers (X, Z).</param>
        /// <param name="waterHeight">Water level, used only to project the routes.</param>
        /// <param name="outputPath">Absolute path of the PNG file to write.</param>
        /// <returns>The path written, or null when the map could not be produced.</returns>
        public static string Export(
            ScenarioDefinition scenario,
            ScenarioMetrics metrics,
            Texture2D chartImage,
            Vector2 worldSize,
            float waterHeight,
            string outputPath)
        {
            if (scenario == null || metrics == null) return null;

            if (chartImage == null || !chartImage.isReadable)
            {
                Debug.LogWarning("[ScenarioMapExporter] Carta indisponível ou não legível — " +
                                 "o mapa não foi gerado. Construa o cenário real (Cenário Real > 1).");
                return null;
            }

            // Working on a copy keeps the chart used by the tactical view untouched.
            var map = new Texture2D(chartImage.width, chartImage.height, TextureFormat.RGBA32, false);
            map.SetPixels(chartImage.GetPixels());

            // Planned route first, so the travelled path is drawn over it and stays readable.
            DrawPolyline(map, scenario.BuildAbsoluteWaypoints(waterHeight), worldSize, RoutePlannedColor, 2, dashed: true);

            foreach (TargetEncounterResult result in metrics.Results)
            {
                DrawPolyline(map, result.Track, worldSize, TargetTrackColor, 2, dashed: false);

                if (result.Track.Count > 0)
                    DrawMarker(map, result.Track[0], worldSize, TargetTrackColor, 7);
            }

            DrawPolyline(map, metrics.UsvTrack, worldSize, UsvTrackColor, 3, dashed: false);

            // The closest approaches go last: they are the point of the whole map.
            foreach (TargetEncounterResult result in metrics.Results)
            {
                if (result.MinDistance >= float.MaxValue) continue;

                Color color = result.SafetyViolated ? CpaViolationColor : CpaOkColor;
                DrawLine(map, result.UsvPositionAtMinDistance, result.TargetPositionAtMinDistance,
                         worldSize, color, 2);
                DrawMarker(map, result.UsvPositionAtMinDistance, worldSize, color, 6);
                DrawMarker(map, result.TargetPositionAtMinDistance, worldSize, color, 6);
            }

            map.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, map.EncodeToPNG());
            Object.Destroy(map);

            return outputPath;
        }

        // ---------------- desenho ----------------

        /// <summary>
        /// Converts a world position to a pixel of the chart image. North stays up and
        /// East to the right, matching how the tactical chart presents the area.
        /// </summary>
        /// <returns>False when the position falls outside the charted area.</returns>
        static bool WorldToPixel(Texture2D map, Vector3 world, Vector2 worldSize, out int px, out int py)
        {
            px = py = 0;
            if (worldSize.x <= 0f || worldSize.y <= 0f) return false;

            float u = world.x / worldSize.x;
            float v = world.z / worldSize.y;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            px = Mathf.Clamp(Mathf.RoundToInt(u * (map.width - 1)), 0, map.width - 1);
            py = Mathf.Clamp(Mathf.RoundToInt(v * (map.height - 1)), 0, map.height - 1);
            return true;
        }

        /// <summary>Draws a sequence of connected segments.</summary>
        static void DrawPolyline(Texture2D map, IReadOnlyList<Vector3> points, Vector2 worldSize,
                                 Color color, int thickness, bool dashed)
        {
            if (points == null || points.Count < 2) return;

            for (int i = 1; i < points.Count; i++)
            {
                if (dashed && i % 2 == 0) continue;
                DrawLine(map, points[i - 1], points[i], worldSize, color, thickness);
            }
        }

        /// <summary>Draws one segment between two world positions (Bresenham).</summary>
        static void DrawLine(Texture2D map, Vector3 from, Vector3 to, Vector2 worldSize,
                             Color color, int thickness)
        {
            if (!WorldToPixel(map, from, worldSize, out int x0, out int y0)) return;
            if (!WorldToPixel(map, to, worldSize, out int x1, out int y1)) return;

            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                PaintDot(map, x0, y0, color, thickness);
                if (x0 == x1 && y0 == y1) break;

                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
        }

        /// <summary>Draws a filled square centred on a world position.</summary>
        static void DrawMarker(Texture2D map, Vector3 world, Vector2 worldSize, Color color, int size)
        {
            if (!WorldToPixel(map, world, worldSize, out int px, out int py)) return;
            PaintDot(map, px, py, color, size);
        }

        /// <summary>Paints a square block of pixels, clipped to the image bounds.</summary>
        static void PaintDot(Texture2D map, int cx, int cy, Color color, int size)
        {
            int half = Mathf.Max(1, size) / 2;
            for (int y = cy - half; y <= cy + half; y++)
            {
                if (y < 0 || y >= map.height) continue;
                for (int x = cx - half; x <= cx + half; x++)
                {
                    if (x < 0 || x >= map.width) continue;
                    map.SetPixel(x, y, color);
                }
            }
        }
    }
}
