using UnityEngine;
using UnityEngine.InputSystem;
using MaritimeScenario.Water;

namespace MaritimeScenario.Boat
{
    /// <summary>
    /// Simple vessel control to test the scenario during Play: W/A/S/D or arrow keys
    /// move and turn the USV. The buoyancy samples the same water wave (WaveUtil) at 4
    /// hull points (bow/stern/port/starboard) to produce heave (up/down) and tilt
    /// (pitch/roll) consistent with what is seen — it is not a real hydrodynamic model,
    /// just a visual approximation to "feel" the scale of the scenario. It also shows,
    /// in real time, the local position (meters) and the geographic position (lat/lon),
    /// as a live demonstration of the georeferencing.
    /// </summary>
    public class BoatController : MonoBehaviour
    {
        public float Speed = 10f;
        public float TurnSpeed = 70f;
        public float BuoyancyHeight = 0.5f;
        public bool ShowHud = true;

        [Header("Limite de terra")]
        [Tooltip("Acima desta altura (Y do terreno) é considerado terra firme — o barco não entra.")]
        public float LandHeightThreshold = 0.3f;
        [Tooltip("Colisor do terreno usado para checar a altura embaixo do barco. Se vazio, o limite de terra fica desativado.")]
        public Collider TerrainCollider;

        [Header("Casco (para a flutuação)")]
        public float Length = 4.5f;
        public float Beam = 2.2f;

        [Header("Onda (deve bater com a Água)")]
        public float WaveAmplitude = 0.15f;
        public float WaveScale = 0.05f;
        public float WaveSpeed = 0.6f;

        IGeoReference geo;

        /// <summary>
        /// Finds any georeferencing provider (fictional = tangent plane, real = UTM)
        /// through the shared IGeoReference interface, so the HUD can show lat/lon.
        /// </summary>
        void Start()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>())
                if (mb is IGeoReference g) { geo = g; break; }
        }

        /// <summary>
        /// Reads the keyboard, moves/turns the USV, blocks it from climbing onto land,
        /// and applies the wave buoyancy every frame.
        /// </summary>
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float forward = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) forward += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) forward -= 1f;

                float turn = 0f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn -= 1f;

                Vector3 previousPosition = transform.position;
                transform.Rotate(Vector3.up, turn * TurnSpeed * Time.deltaTime);
                transform.position += transform.forward * forward * Speed * Time.deltaTime;

                if (IsOverLand(transform.position))
                    transform.position = previousPosition; // block the boat from climbing onto the island
            }

            ApplyBuoyancy();
        }

        /// <summary>
        /// Tests only against the terrain collider (not "anything in the scene"), so it
        /// never risks hitting the boat itself or another object. Casts a ray straight
        /// down and reports whether the terrain height there is above the land threshold.
        /// </summary>
        /// <param name="pos">World position to test.</param>
        /// <returns>True if that position is over land.</returns>
        bool IsOverLand(Vector3 pos)
        {
            if (TerrainCollider == null) return false;

            var ray = new Ray(pos + Vector3.up * 300f, Vector3.down);
            if (TerrainCollider.Raycast(ray, out var hit, 1000f))
                return hit.point.y > LandHeightThreshold;
            return false;
        }

        /// <summary>
        /// Samples the wave at four hull points to make the boat heave and tilt
        /// (pitch/roll) following the surface. Purely visual, not a physical model.
        /// </summary>
        void ApplyBuoyancy()
        {
            Vector3 p = transform.position;
            Vector3 front = transform.forward * (Length * 0.5f);
            Vector3 side = transform.right * (Beam * 0.5f);

            float bowH = WaveUtil.Height(p.x + front.x, p.z + front.z, Time.time, WaveAmplitude, WaveScale, WaveSpeed);
            float sternH = WaveUtil.Height(p.x - front.x, p.z - front.z, Time.time, WaveAmplitude, WaveScale, WaveSpeed);
            float starboardH = WaveUtil.Height(p.x + side.x, p.z + side.z, Time.time, WaveAmplitude, WaveScale, WaveSpeed);
            float portH = WaveUtil.Height(p.x - side.x, p.z - side.z, Time.time, WaveAmplitude, WaveScale, WaveSpeed);

            float averageHeight = (bowH + sternH + starboardH + portH) * 0.25f;
            p.y = Mathf.Lerp(p.y, BuoyancyHeight + averageHeight, Time.deltaTime * 6f);
            transform.position = p;

            float pitch = Mathf.Atan2(sternH - bowH, Length) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(portH - starboardH, Beam) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(pitch, transform.eulerAngles.y, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 4f);
        }

        /// <summary>Draws the on-screen HUD with the local and geographic positions.</summary>
        void OnGUI()
        {
            if (!ShowHud) return;

            var pos = transform.position;
            string text = $"Local (X, Z): {pos.x:F1} m, {pos.z:F1} m";
            if (geo != null)
            {
                var (lat, lon) = geo.LocalToGeographic(pos.x, pos.z);
                text += $"\nGeográfica (lat, lon): {lat:F6}, {lon:F6}";
            }
            text += "\nWASD / setas para mover";

            GUI.Label(new Rect(10, 10, 460, 70), text, HudStyle());
        }

        static GUIStyle hudStyle;

        /// <summary>Lazily builds and caches the GUIStyle used by the HUD label.</summary>
        static GUIStyle HudStyle()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                hudStyle.normal.textColor = Color.white;
            }
            return hudStyle;
        }
    }
}
