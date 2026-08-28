using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// How a target behaves during the scenario.
    /// </summary>
    public enum TargetBehaviour
    {
        /// <summary>Fixed obstacle: never moves (rock, buoy, anchored vessel).</summary>
        Static,

        /// <summary>Moves in a straight line at constant heading and speed.</summary>
        StraightLine,

        /// <summary>Follows its own multi-waypoint route (used by the random generator).</summary>
        Route
    }

    /// <summary>
    /// One target (obstacle) of the scenario, described in local scenario coordinates
    /// relative to the USV start position. Using relative coordinates lets the same
    /// scenario be replayed anywhere on the chart without rewriting the numbers.
    /// </summary>
    [Serializable]
    public class TargetSpec
    {
        [Tooltip("Nome do alvo (aparece na cena e no relatório).")]
        public string Name = "Target";

        [Tooltip("Posição inicial em metros, RELATIVA ao ponto de partida do USV (X = Leste, Z = Norte).")]
        public Vector2 StartOffsetXZ = new Vector2(0f, 500f);

        [Tooltip("Rumo inicial em graus (0 = Norte, 90 = Leste).")]
        public float HeadingDegrees = 180f;

        [Tooltip("Velocidade em nós. Ignorado quando o comportamento é Static.")]
        public float SpeedKnots = 10f;

        [Tooltip("Comportamento do alvo durante o cenário.")]
        public TargetBehaviour Behaviour = TargetBehaviour.StraightLine;

        [Tooltip("Rota própria do alvo, em metros RELATIVOS ao ponto de partida do USV. " +
                 "Usada apenas quando o comportamento é Route; o primeiro ponto é o spawn.")]
        public List<Vector2> RouteOffsetsXZ = new List<Vector2>();

        [Tooltip("Se o alvo repete a rota em laço ao chegar ao fim (só para Route).")]
        public bool LoopRoute = true;

        [Header("Dimensões (m)")]
        public float Length = 40f;
        public float Beam = 10f;

        [Tooltip("Cor do casco, só para diferenciar os alvos visualmente.")]
        public Color HullColor = new Color(0.70f, 0.25f, 0.20f);

        /// <summary>Speed converted to meters per second (1 knot = 0.514444 m/s).</summary>
        public float SpeedMs => SpeedKnots * 0.514444f;
    }

    /// <summary>
    /// A DETERMINISTIC navigation test scenario: a fixed set of initial conditions that
    /// always produces the same run, so navigation algorithms can be compared against
    /// each other on equal terms.
    ///
    /// It describes the USV start pose, the waypoints the simulation itself publishes
    /// (no ROS needed), and the targets it must react to. Nothing here is randomized —
    /// randomized scenarios with a reproducibility seed are a separate concern.
    ///
    /// The four standard encounters (static obstacle, crossing, head-on and overtaking)
    /// mirror the COLREGS/RIPEAM situations that collision-avoidance algorithms are
    /// expected to handle.
    /// </summary>
    [CreateAssetMenu(menuName = "Simulador/Cenário de Teste", fileName = "ScenarioDefinition")]
    public class ScenarioDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("Nome curto do cenário (aparece no relatório).")]
        public string DisplayName = "Cenário";

        [TextArea(2, 4)]
        [Tooltip("O que este cenário testa e qual reação se espera do USV.")]
        public string Description = "";

        [Header("USV")]
        [Tooltip("Ponto de partida do USV em coordenadas locais da cena (X = Leste, Z = Norte). " +
                 "Todas as posições de alvo e waypoints são relativas a este ponto.")]
        public Vector2 UsvStartXZ = new Vector2(9900f, 7500f);

        [Tooltip("Rumo inicial do USV em graus (0 = Norte, 90 = Leste).")]
        public float UsvStartHeadingDegrees = 0f;

        [Tooltip("Velocidade de cruzeiro desejada, em nós.")]
        public float UsvCruiseSpeedKnots = 12f;

        [Header("Rota do USV")]
        [Tooltip("Se a própria simulação publica a rota no WaypointManager. Desligue quando os " +
                 "waypoints vierem de fora (ex.: do ROS) — é o caso dos cenários de estresse, " +
                 "onde o que importa é o tráfego aleatório e não a rota pré-definida.")]
        public bool PublishWaypoints = true;

        [Tooltip("Waypoints em metros, RELATIVOS ao ponto de partida do USV. " +
                 "O primeiro ponto costuma ser (0,0) — a própria partida.")]
        public List<Vector2> WaypointOffsetsXZ = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1000f)
        };

        [Header("Alvos")]
        public List<TargetSpec> Targets = new List<TargetSpec>();

        [Header("Critérios")]
        [Tooltip("Tempo máximo de simulação, em segundos. O cenário encerra ao atingir este limite.")]
        public float MaxDurationSeconds = 180f;

        [Tooltip("Distância mínima de segurança, em metros. Passar mais perto que isso de um alvo " +
                 "conta como falha (o valor típico de CPA aceitável depende da via navegável).")]
        public float MinSafeDistanceMeters = 100f;

        /// <summary>USV cruise speed converted to meters per second.</summary>
        public float UsvCruiseSpeedMs => UsvCruiseSpeedKnots * 0.514444f;

        /// <summary>
        /// Returns the waypoints in absolute scene coordinates, ready for
        /// <c>WaypointManager.SetPath</c>.
        /// </summary>
        /// <param name="waterHeight">Y coordinate to place the waypoints at (water level).</param>
        /// <returns>Absolute waypoints, or an empty array when fewer than two are defined.</returns>
        public Vector3[] BuildAbsoluteWaypoints(float waterHeight = 0f)
        {
            if (WaypointOffsetsXZ == null || WaypointOffsetsXZ.Count < 2)
                return Array.Empty<Vector3>();

            var waypoints = new Vector3[WaypointOffsetsXZ.Count];
            for (int i = 0; i < WaypointOffsetsXZ.Count; i++)
            {
                Vector2 offset = WaypointOffsetsXZ[i];
                waypoints[i] = new Vector3(UsvStartXZ.x + offset.x, waterHeight, UsvStartXZ.y + offset.y);
            }
            return waypoints;
        }

        /// <summary>
        /// Returns the absolute start position of a target, in scene coordinates.
        /// </summary>
        /// <param name="target">The target specification.</param>
        /// <param name="waterHeight">Y coordinate to place the target at (water level).</param>
        /// <returns>Absolute start position.</returns>
        public Vector3 BuildAbsoluteTargetStart(TargetSpec target, float waterHeight = 0f)
        {
            return new Vector3(
                UsvStartXZ.x + target.StartOffsetXZ.x,
                waterHeight,
                UsvStartXZ.y + target.StartOffsetXZ.y);
        }

        /// <summary>
        /// Returns a target's own route in absolute scene coordinates.
        /// </summary>
        /// <param name="target">The target specification.</param>
        /// <param name="waterHeight">Y coordinate to place the route at (water level).</param>
        /// <returns>Absolute route points, or an empty list when the target has no route.</returns>
        public List<Vector3> BuildAbsoluteTargetRoute(TargetSpec target, float waterHeight = 0f)
        {
            var route = new List<Vector3>();
            if (target.RouteOffsetsXZ == null) return route;

            foreach (Vector2 offset in target.RouteOffsetsXZ)
            {
                route.Add(new Vector3(
                    UsvStartXZ.x + offset.x,
                    waterHeight,
                    UsvStartXZ.y + offset.y));
            }
            return route;
        }
    }
}
