using UnityEngine;

namespace MaritimeScenario.Boat
{
    /// <summary>Visual hull style (drives the procedural superstructure).</summary>
    public enum HullStyle { Cargo, Medium, Launch }

    /// <summary>
    /// Vessel type (data-driven). Inspired by the AIS type codes to give plausible
    /// size/speed — but at this stage it is only a label for synthetic generation,
    /// NOT real AIS data. Each asset defines the length range, the beam ratio, the
    /// speed range and the color, used by the procedural vessel factory.
    ///
    /// AIS reference: 70–79 cargo, 80–89 tanker, 60–69 passenger, 52 tug,
    /// 37 pleasure, 30 fishing. (In real AIS, length = A+B and beam = C+D.)
    /// </summary>
    [CreateAssetMenu(menuName = "Simulador/Tipo de Embarcação", fileName = "VesselType")]
    public class VesselType : ScriptableObject
    {
        /// <summary>Name of the vessel type, shown in the Editor and in reports.</summary>
        public string DisplayName = "Embarcação";

        /// <summary>Type code from the AIS standard. A plausibility label, not real AIS data at this stage.</summary>
        [Tooltip("Código de tipo do padrão AIS (rótulo de plausibilidade; não é dado AIS real nesta fase).")]
        public int AisCode = 70;

        /// <summary>Plausible length range in meters, as (minimum, maximum).</summary>
        [Tooltip("Faixa de comprimento plausível (min, max) em metros.")]
        public Vector2 LengthRangeM = new Vector2(120f, 200f);

        /// <summary>Beam as a fraction of the length.</summary>
        [Range(0.08f, 0.35f), Tooltip("Boca = comprimento × razão.")]
        public float BeamRatio = 0.16f;

        /// <summary>Typical speed range in knots, as (minimum, maximum).</summary>
        [Tooltip("Faixa de velocidade típica (min, max) em nós.")]
        public Vector2 SpeedRangeKn = new Vector2(8f, 14f);

        /// <summary>Hull colour applied when a vessel of this type is created.</summary>
        public Color HullColor = new Color(0.35f, 0.30f, 0.28f);
        /// <summary>Hull shape used to build the mesh.</summary>
        public HullStyle Style = HullStyle.Cargo;

        /// <summary>Draws a random length (m) within this type's plausible range.</summary>
        public float RollLengthM() => Random.Range(LengthRangeM.x, LengthRangeM.y);

        /// <summary>Draws a random speed within this type's range and converts it to m/s.</summary>
        public float RollSpeedMs() => Random.Range(SpeedRangeKn.x, SpeedRangeKn.y) * 0.514444f; // knots -> m/s
    }
}
