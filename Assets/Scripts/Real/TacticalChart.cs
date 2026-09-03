using UnityEngine;
using UnityEngine.InputSystem;
using MaritimeScenario.Boat;
using MaritimeScenario.Sensor;

namespace MaritimeScenario.Real
{
    /// <summary>
    /// Live 2D tactical view. Two modes (M key):
    ///   • Follow  — top-down camera follows the boat closely (immediate surroundings);
    ///   • Overview — shows the WHOLE BAY as a flat CHART (image colored by depth: blue
    ///     water, green land) with markers for every vessel + the boat. The chart image
    ///     (ChartImage) is produced by the builder, so it has high contrast and does not
    ///     depend on the 3D lighting/water.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TacticalChart : MonoBehaviour
    {
        /// <summary>Transform the chart follows in follow mode, normally the USV.</summary>
        public Transform Target;
        /// <summary>Half-height of the visible area when following the target, in meters.</summary>
        public float FollowViewSize = 180f;   // half-height of the view when following (m)
        /// <summary>Height at which the chart camera sits above the scene, in meters.</summary>
        public float Altitude = 600f;

        [Header("Carta (visão geral)")]
        /// <summary>Depth-coloured image of the region, produced by the scenario builder.</summary>
        public Texture2D ChartImage;    // depth-colored image (from the builder)
        /// <summary>Ground size covered by the chart image, in meters, as (width, height).</summary>
        public Vector2 WorldSize = new Vector2(20000f, 15000f); // size of the covered world (m)

        Camera cam;
        bool overviewMode;
        const int WATER_LAYER = 4; // "Water"

        DynamicVessel[] fleet = new DynamicVessel[0];
        VesselSensor sensor;
        float nextRefreshTime;

        readonly Rect followViewport = new Rect(0.70f, 0.70f, 0.29f, 0.29f);
        readonly Rect overviewViewport = new Rect(0.28f, 0.14f, 0.70f, 0.82f);

        /// <summary>
        /// Configures the dedicated orthographic top-down camera (North up) and makes it
        /// exclude the water layer, so the chart is not "washed out" by the 3D water.
        /// </summary>
        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = Altitude + 400f;
            cam.depth = 10;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.08f, 0.16f);
            cam.rect = followViewport;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // North up

            // The tactical camera does not render the water (terrain only).
            var water = GameObject.Find("Agua");
            if (water != null) water.layer = WATER_LAYER;
            cam.cullingMask = ~(1 << WATER_LAYER);
        }

        /// <summary>
        /// Toggles the mode on the M key and periodically refreshes the fleet list and
        /// the sensor reference (once per second, not every frame).
        /// </summary>
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) overviewMode = !overviewMode;

            if (Time.time >= nextRefreshTime)
            {
                fleet = Object.FindObjectsByType<DynamicVessel>();
                if (sensor == null && Target != null) sensor = Target.GetComponent<VesselSensor>();
                nextRefreshTime = Time.time + 1f;
            }
        }

        /// <summary>
        /// In overview mode with the chart image, the 3D camera does not need to render
        /// (the panel is drawn in OnGUI); otherwise it falls back to the 3D camera and
        /// keeps it centered above the target.
        /// </summary>
        void LateUpdate()
        {
            bool usingImage = overviewMode && ChartImage != null;
            cam.enabled = !usingImage;

            if (!overviewMode && Target != null)
            {
                cam.rect = followViewport;
                cam.orthographicSize = FollowViewSize;
                transform.position = new Vector3(Target.position.x, Altitude, Target.position.z);
            }
        }

        // ---------------- panel (OnGUI) ----------------

        static GUIStyle textStyle, triangleStyle;
        static Texture2D whiteTexture;

        /// <summary>Draws the tactical panel: title, chart/terrain and all markers.</summary>
        void OnGUI()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold };
                textStyle.normal.textColor = Color.white;
                triangleStyle = new GUIStyle { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                whiteTexture = Texture2D.whiteTexture;
            }

            Rect vp = overviewMode ? overviewViewport : followViewport;
            Rect panel = PanelPixels(vp);

            GUI.Label(new Rect(panel.x + 6, panel.y + 2, 340, 20),
                overviewMode ? "CARTA TÁTICA — VISÃO GERAL   (M: seguir barco)"
                             : "CARTA TÁTICA  (N ↑)   (M: abrir mapa)", textStyle);

            if (overviewMode && ChartImage != null)
                DrawOverview(panel);
            else
                DrawProjectedMarkers(); // follow mode (the 3D camera already rendered the terrain)
        }

        /// <summary>
        /// Overview mode: draws the chart image (aspect-preserved) and overlays the sensor
        /// range, the ground-truth vessels, the sensor contacts and the USV, all mapped
        /// linearly from world to image pixels.
        /// </summary>
        void DrawOverview(Rect panel)
        {
            // Dark background (letterbox borders).
            SetColor(new Color(0.04f, 0.08f, 0.16f));
            GUI.DrawTexture(panel, whiteTexture);
            SetColor(Color.white);

            // Chart image, preserving aspect ratio (FitAspect computes the inner rect).
            Rect img = FitAspect(panel, (float)ChartImage.width / ChartImage.height);
            GUI.DrawTexture(img, ChartImage);

            // Sensor range (circle around the USV).
            if (sensor != null && Target != null && MapLinear(img, Target.position, out Vector2 pc))
            {
                float radiusPx = sensor.Range / WorldSize.x * img.width;
                DrawRing(pc, radiusPx, new Color(0.2f, 0.9f, 1f, 0.5f));
            }

            // Ground truth: squares colored by size.
            foreach (var v in fleet)
                if (v != null) DrawSquareLinear(img, v.transform.position, ColorBySize(v.Length), 7f);

            // Sensor contacts: cyan rings (what the USV "sees").
            if (sensor != null)
                foreach (var c in sensor.Contacts)
                    if (MapLinear(img, c.Position, out Vector2 pcont))
                        DrawRing(pcont, 8f, c.IsNew ? new Color(1f, 1f, 0.3f, 1f) : new Color(0.2f, 0.95f, 1f, 1f));

            if (Target != null) DrawTriangleLinear(img, Target.position, Target.eulerAngles.y);
        }

        /// <summary>Draws a colored square at a world position, mapped linearly onto the chart image.</summary>
        void DrawSquareLinear(Rect img, Vector3 world, Color color, float size)
        {
            if (!MapLinear(img, world, out Vector2 p)) return;
            SetColor(color);
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), whiteTexture);
            SetColor(Color.white);
        }

        /// <summary>Draws the USV heading triangle at a world position on the chart image.</summary>
        void DrawTriangleLinear(Rect img, Vector3 world, float heading)
        {
            if (!MapLinear(img, world, out Vector2 p)) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(heading, p);
            triangleStyle.normal.textColor = new Color(1f, 0.25f, 0.15f);
            GUI.Label(new Rect(p.x - 15, p.y - 18, 30, 36), "▲", triangleStyle);
            GUI.matrix = m;
        }

        /// <summary>
        /// Maps a world (x, z) position to a pixel inside the image rect (North up, East
        /// right). Returns false when the point falls outside the covered world.
        /// </summary>
        bool MapLinear(Rect img, Vector3 world, out Vector2 screen)
        {
            screen = default;
            float u = world.x / WorldSize.x;
            float w = world.z / WorldSize.y;
            if (u < 0f || u > 1f || w < 0f || w > 1f) return false;
            screen = new Vector2(img.x + u * img.width, img.y + (1f - w) * img.height);
            return true;
        }

        // ---- follow mode: markers projected by the 3D camera ----

        /// <summary>Follow mode: overlays the vessels, sensor contacts and USV, projected by the 3D camera.</summary>
        void DrawProjectedMarkers()
        {
            if (cam == null) return;
            foreach (var v in fleet)
                if (v != null) DrawSquareProjected(v.transform.position, ColorBySize(v.Length), 6f);
            if (sensor != null)
                foreach (var c in sensor.Contacts)
                    if (Project(c.Position, out Vector2 p))
                        DrawRing(p, 9f, c.IsNew ? new Color(1f, 1f, 0.3f, 1f) : new Color(0.2f, 0.95f, 1f, 1f));
            if (Target != null) DrawTriangleProjected(Target.position, Target.eulerAngles.y);
        }

        /// <summary>
        /// Projects a world position to screen pixels through the 3D camera.
        /// Returns false when the point is behind or outside the camera viewport.
        /// </summary>
        bool Project(Vector3 world, out Vector2 screen)
        {
            screen = default;
            Vector3 v = cam.WorldToViewportPoint(world);
            if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            screen = new Vector2((cam.rect.x + v.x * cam.rect.width) * Screen.width,
                               Screen.height - (cam.rect.y + v.y * cam.rect.height) * Screen.height);
            return true;
        }

        /// <summary>Draws a colored square at a world position, projected by the 3D camera.</summary>
        void DrawSquareProjected(Vector3 world, Color color, float size)
        {
            if (!Project(world, out Vector2 p)) return;
            SetColor(color);
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), whiteTexture);
            SetColor(Color.white);
        }

        /// <summary>Draws the USV heading triangle at a world position, projected by the 3D camera.</summary>
        void DrawTriangleProjected(Vector3 world, float heading)
        {
            if (!Project(world, out Vector2 p)) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(heading, p);
            triangleStyle.normal.textColor = new Color(1f, 0.25f, 0.15f);
            GUI.Label(new Rect(p.x - 15, p.y - 18, 30, 36), "▲", triangleStyle);
            GUI.matrix = m;
        }

        // ---------------- helpers ----------------

        /// <summary>Converts a normalized viewport rect to a pixel rect in GUI (top-left) space.</summary>
        static Rect PanelPixels(Rect vp) => new Rect(
            vp.x * Screen.width,
            (1f - vp.y - vp.height) * Screen.height,
            vp.width * Screen.width,
            vp.height * Screen.height);

        /// <summary>Returns the largest rect inside 'panel' that preserves the given image aspect ratio.</summary>
        static Rect FitAspect(Rect panel, float imgAspect)
        {
            float panelAspect = panel.width / panel.height;
            if (panelAspect > imgAspect)
            {
                float w = panel.height * imgAspect;
                return new Rect(panel.x + (panel.width - w) * 0.5f, panel.y, w, panel.height);
            }
            else
            {
                float h = panel.width / imgAspect;
                return new Rect(panel.x, panel.y + (panel.height - h) * 0.5f, panel.width, h);
            }
        }

        /// <summary>Sets the current GUI tint color.</summary>
        static void SetColor(Color c) => GUI.color = c;

        static Texture2D ringTexture;

        /// <summary>Lazily builds and caches a white ring texture used to draw range/contact circles.</summary>
        static Texture2D RingTexture()
        {
            if (ringTexture != null) return ringTexture;
            const int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            float cx = s * 0.5f, cy = s * 0.5f, rOut = 62f, rIn = 55f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = (d <= rOut && d >= rIn) ? 1f : 0f;
                    px[x + y * s] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            ringTexture = t;
            return t;
        }

        /// <summary>Draws a ring of the given pixel radius centered at a screen point.</summary>
        static void DrawRing(Vector2 center, float radiusPx, Color color)
        {
            SetColor(color);
            GUI.DrawTexture(new Rect(center.x - radiusPx, center.y - radiusPx, radiusPx * 2f, radiusPx * 2f), RingTexture());
            SetColor(Color.white);
        }

        /// <summary>Returns the marker color for a vessel, by its length (size class).</summary>
        static Color ColorBySize(float length)
        {
            if (length > 80f) return new Color(1f, 0.30f, 0.15f);   // cargo/tanker
            if (length > 25f) return new Color(1f, 0.65f, 0.10f);   // medium
            return new Color(1f, 0.95f, 0.35f);                     // launch
        }
    }
}
