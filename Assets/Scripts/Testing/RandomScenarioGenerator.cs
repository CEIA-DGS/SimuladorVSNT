using System.Collections.Generic;
using UnityEngine;

namespace MaritimeScenario.Testing
{
    /// <summary>
    /// Generates STRESS-TEST scenarios: many vessels spawned at random positions, each
    /// following its own random route, to see how a navigation algorithm copes with a
    /// busy and unpredictable environment.
    ///
    /// Everything is driven by a single global seed. The same seed always produces the
    /// exact same scenario — spawn positions, routes, speeds and sizes — which is what
    /// makes a failed run debuggable: note the seed, reproduce the failure, fix it,
    /// re-run the same seed to confirm.
    ///
    /// Reproducibility relies on a dedicated <see cref="System.Random"/> instance rather
    /// than Unity's global <c>Random</c>: the global one is shared state, so any other
    /// script drawing from it would shift this generator's sequence and silently break
    /// the guarantee.
    ///
    /// Targets are only ever placed on navigable water, checked against the terrain
    /// collider — no vessel is born on land.
    /// </summary>
    public class RandomScenarioGenerator : MonoBehaviour
    {
        [Header("Semente")]
        [Tooltip("Semente global. O mesmo número reproduz exatamente o mesmo cenário. " +
                 "Anote a semente de uma execução que falhou para depurá-la depois.")]
        public int Seed = 20260813;

        [Header("Área de geração")]
        [Tooltip("Centro da área onde os alvos podem nascer, em coordenadas locais da cena. " +
                 "Se ficar em zero, usa a posição de partida do USV.")]
        public Vector2 AreaCenterXZ = Vector2.zero;

        [Tooltip("Raio da área de geração, em metros.")]
        public float AreaRadius = 2000f;

        [Tooltip("Distância mínima do USV em que um alvo pode nascer, em metros. " +
                 "Evita que o cenário comece já em cima de uma colisão.")]
        public float MinDistanceFromUsv = 300f;

        [Header("Quantidade de alvos")]
        [Min(0)] public int MinTargets = 6;
        [Min(0)] public int MaxTargets = 14;

        [Header("Faixas dos alvos")]
        [Tooltip("Faixa de velocidade dos alvos, em nós (mín, máx).")]
        public Vector2 SpeedRangeKnots = new Vector2(4f, 16f);

        [Tooltip("Faixa de comprimento dos alvos, em metros (mín, máx).")]
        public Vector2 LengthRangeMeters = new Vector2(15f, 120f);

        [Tooltip("Proporção de alvos parados (0 = todos se movem, 1 = todos parados).")]
        [Range(0f, 1f)] public float StaticTargetRatio = 0.15f;

        [Header("Rotas dos alvos")]
        [Min(2)] public int MinRoutePoints = 2;
        [Min(2)] public int MaxRoutePoints = 4;

        [Tooltip("Comprimento típico de cada perna da rota, em metros (mín, máx).")]
        public Vector2 LegLengthMeters = new Vector2(400f, 1500f);

        [Header("Cenário gerado")]
        [Tooltip("Ponto de partida do USV no cenário gerado.")]
        public Vector2 UsvStartXZ = new Vector2(9900f, 7500f);

        [Tooltip("Se o cenário gerado deve publicar uma rota aleatória para o USV. Ligado, o USV " +
                 "navega através do tráfego — que é o que se quer medir num teste de estresse. " +
                 "Desligue apenas quando os waypoints vierem de fora (ROS); sem rota o USV fica parado.")]
        public bool PublishUsvWaypoints = true;

        [Tooltip("Duração máxima do cenário gerado, em segundos.")]
        public float MaxDurationSeconds = 300f;

        [Tooltip("Distância mínima de segurança usada para avaliar o resultado, em metros.")]
        public float MinSafeDistanceMeters = 100f;

        [Header("Água navegável")]
        [Tooltip("Colisor do terreno, usado para garantir que nenhum alvo nasça em terra. " +
                 "Se vazio, procura por 'TerrenoCarta' na cena.")]
        public Collider TerrainCollider;

        [Tooltip("Acima desta altura (Y do terreno) considera-se terra firme.")]
        public float LandHeightThreshold = 0.3f;

        [Tooltip("Tentativas de sorteio por ponto antes de desistir de achar água.")]
        [Min(1)] public int WaterSearchAttempts = 30;

        /// <summary>The seed actually used by the last generation, for the report.</summary>
        public int LastUsedSeed { get; private set; }

        System.Random rng;

        /// <summary>
        /// Builds a complete scenario from the configured seed. Calling it twice with the
        /// same seed and the same settings yields an identical scenario.
        /// </summary>
        /// <returns>The generated scenario definition.</returns>
        public ScenarioDefinition Generate()
        {
            return Generate(Seed);
        }

        /// <summary>
        /// Builds a complete scenario from an explicit seed, ignoring the inspector value.
        /// Useful to sweep many seeds in a batch and reproduce a specific failure later.
        /// </summary>
        /// <param name="seed">The seed to drive every random draw.</param>
        /// <returns>The generated scenario definition.</returns>
        public ScenarioDefinition Generate(int seed)
        {
            LastUsedSeed = seed;
            rng = new System.Random(seed);

            ResolveTerrainCollider();

            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();

            // The seed is deliberately kept OUT of the name: it travels in its own column
            // of the results, in the report label and in the exported file names. Baking it
            // into the name would repeat it three times over and, worse, make every seed
            // look like a different scenario — so the runs of one stress test could no
            // longer be grouped together when analysing the CSV.
            scenario.DisplayName = "Estresse aleatório";
            scenario.Description =
                $"Cenário de estresse gerado automaticamente com a semente {seed}. " +
                "Reutilize a mesma semente para reproduzir exatamente estas posições e rotas.";

            scenario.UsvStartXZ = UsvStartXZ;
            scenario.UsvStartHeadingDegrees = (float)(rng.NextDouble() * 360.0);
            scenario.UsvCruiseSpeedKnots = 12f;
            scenario.PublishWaypoints = PublishUsvWaypoints;
            scenario.MaxDurationSeconds = MaxDurationSeconds;
            scenario.MinSafeDistanceMeters = MinSafeDistanceMeters;
            scenario.Targets = new List<TargetSpec>();

            Vector2 center = AreaCenterXZ == Vector2.zero ? UsvStartXZ : AreaCenterXZ;

            int count = RangeInt(MinTargets, MaxTargets);
            for (int i = 0; i < count; i++)
            {
                TargetSpec target = GenerateTarget(i, center, scenario.UsvStartXZ);
                if (target != null) scenario.Targets.Add(target);
            }

            // A route is only generated when the scenario is meant to publish one.
            scenario.WaypointOffsetsXZ = PublishUsvWaypoints
                ? GenerateUsvRoute(center)
                : new List<Vector2>();

            Debug.Log($"[RandomScenarioGenerator] Cenário gerado com semente {seed}: " +
                      $"{scenario.Targets.Count} alvos.");

            return scenario;
        }

        // ---------------- geração dos alvos ----------------

        /// <summary>
        /// Draws one target: a spawn point on navigable water, a route, a speed and a size.
        /// </summary>
        /// <param name="index">Index used to name the target.</param>
        /// <param name="center">Center of the generation area, in scene coordinates.</param>
        /// <param name="usvStart">USV start position, used to keep a safety gap and to make offsets relative.</param>
        /// <returns>The target, or null when no navigable spawn point was found.</returns>
        TargetSpec GenerateTarget(int index, Vector2 center, Vector2 usvStart)
        {
            if (!TryFindWaterPoint(center, usvStart, out Vector2 spawn)) return null;

            float length = RangeFloat(LengthRangeMeters.x, LengthRangeMeters.y);
            bool isStatic = rng.NextDouble() < StaticTargetRatio;

            var target = new TargetSpec
            {
                Name = $"Alvo{index:00}",
                StartOffsetXZ = spawn - usvStart,
                HeadingDegrees = (float)(rng.NextDouble() * 360.0),
                SpeedKnots = isStatic ? 0f : RangeFloat(SpeedRangeKnots.x, SpeedRangeKnots.y),
                Length = length,
                Beam = length * RangeFloat(0.12f, 0.30f),
                HullColor = RandomHullColor(),
                Behaviour = isStatic ? TargetBehaviour.Static : TargetBehaviour.Route,
                LoopRoute = true
            };

            if (!isStatic)
                target.RouteOffsetsXZ = GenerateTargetRoute(spawn, usvStart, out float initialHeading);
            else
                target.RouteOffsetsXZ = new List<Vector2>();

            // The initial heading must match the first leg, otherwise the vessel would
            // visibly snap to its route on the first physics step.
            if (!isStatic && target.RouteOffsetsXZ.Count >= 2)
            {
                Vector2 first = target.RouteOffsetsXZ[0];
                Vector2 second = target.RouteOffsetsXZ[1];
                Vector2 leg = second - first;
                if (leg.sqrMagnitude > 0.01f)
                    target.HeadingDegrees = Mathf.Atan2(leg.x, leg.y) * Mathf.Rad2Deg;
            }

            return target;
        }

        /// <summary>
        /// Builds a random multi-leg route starting at the spawn point, keeping every leg
        /// end on navigable water.
        /// </summary>
        /// <param name="spawn">Spawn point in scene coordinates.</param>
        /// <param name="usvStart">USV start, used to convert to relative offsets.</param>
        /// <param name="initialHeading">Heading of the first leg, in degrees.</param>
        /// <returns>The route as offsets relative to the USV start.</returns>
        List<Vector2> GenerateTargetRoute(Vector2 spawn, Vector2 usvStart, out float initialHeading)
        {
            var route = new List<Vector2> { spawn - usvStart };
            initialHeading = 0f;

            int legs = RangeInt(MinRoutePoints, MaxRoutePoints) - 1;
            Vector2 current = spawn;
            float heading = (float)(rng.NextDouble() * 360.0);

            for (int i = 0; i < legs; i++)
            {
                // Turns between legs stay moderate, so the routes look like plausible
                // navigation instead of zig-zag noise.
                if (i > 0) heading += RangeFloat(-70f, 70f);

                float legLength = RangeFloat(LegLengthMeters.x, LegLengthMeters.y);
                float rad = heading * Mathf.Deg2Rad;
                var candidate = current + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * legLength;

                // Land ahead: turn away and shorten the leg instead of ploughing into it.
                if (!IsNavigableWater(candidate))
                {
                    heading += RangeFloat(120f, 240f);
                    rad = heading * Mathf.Deg2Rad;
                    candidate = current + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (legLength * 0.5f);
                    if (!IsNavigableWater(candidate)) break;
                }

                route.Add(candidate - usvStart);
                current = candidate;
                if (i == 0) initialHeading = heading;
            }

            return route;
        }

        /// <summary>Builds a simple random route for the USV, when the scenario publishes one.</summary>
        /// <param name="center">Center of the generation area.</param>
        /// <returns>Waypoint offsets relative to the USV start.</returns>
        List<Vector2> GenerateUsvRoute(Vector2 center)
        {
            var route = new List<Vector2> { Vector2.zero };
            Vector2 current = UsvStartXZ;
            float heading = (float)(rng.NextDouble() * 360.0);

            for (int i = 0; i < 3; i++)
            {
                heading += RangeFloat(-60f, 60f);
                float rad = heading * Mathf.Deg2Rad;
                float legLength = RangeFloat(LegLengthMeters.x, LegLengthMeters.y);
                var candidate = current + new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * legLength;

                if (!IsNavigableWater(candidate)) break;

                route.Add(candidate - UsvStartXZ);
                current = candidate;
            }

            // A route needs at least two points to be followed at all.
            if (route.Count < 2) route.Add(new Vector2(0f, 800f));
            return route;
        }

        // ---------------- água navegável ----------------

        /// <summary>
        /// Draws points inside the area until one lands on navigable water, far enough
        /// from the USV.
        /// </summary>
        /// <param name="center">Center of the generation area.</param>
        /// <param name="usvStart">USV start position.</param>
        /// <param name="point">The point found, in scene coordinates.</param>
        /// <returns>True when a valid point was found within the attempt budget.</returns>
        bool TryFindWaterPoint(Vector2 center, Vector2 usvStart, out Vector2 point)
        {
            for (int attempt = 0; attempt < WaterSearchAttempts; attempt++)
            {
                // Square root on the radius keeps the draw uniform over the disc area,
                // instead of clustering the targets near the center.
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = AreaRadius * Mathf.Sqrt((float)rng.NextDouble());
                var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (Vector2.Distance(candidate, usvStart) < MinDistanceFromUsv) continue;
                if (!IsNavigableWater(candidate)) continue;

                point = candidate;
                return true;
            }

            point = Vector2.zero;
            return false;
        }

        /// <summary>
        /// Tests a point against the terrain collider. Casting a ray straight down and
        /// reading the terrain height reuses the same idea the USV uses to refuse to climb
        /// onto land, so both agree on what counts as water.
        /// </summary>
        /// <param name="pointXZ">Point in scene coordinates (X, Z).</param>
        /// <returns>True when the point is over navigable water.</returns>
        public bool IsNavigableWater(Vector2 pointXZ)
        {
            // Without a terrain reference every point is accepted: better a usable
            // scenario than none, and the caller is warned at resolve time.
            if (TerrainCollider == null) return true;

            var ray = new Ray(new Vector3(pointXZ.x, 500f, pointXZ.y), Vector3.down);
            if (TerrainCollider.Raycast(ray, out RaycastHit hit, 2000f))
                return hit.point.y <= LandHeightThreshold;

            // No terrain under the point: outside the chart, treat as not navigable.
            return false;
        }

        /// <summary>Finds the terrain collider in the scene when it was not assigned.</summary>
        void ResolveTerrainCollider()
        {
            if (TerrainCollider != null) return;

            var terrain = GameObject.Find("TerrenoCarta");
            if (terrain != null) TerrainCollider = terrain.GetComponent<Collider>();

            if (TerrainCollider == null)
                Debug.LogWarning("[RandomScenarioGenerator] Terreno não encontrado: os alvos podem " +
                                 "nascer em terra. Construa o cenário (Cenário Real > 1) ou " +
                                 "atribua o campo TerrainCollider.");
        }

        // ---------------- sorteios ----------------

        /// <summary>Draws a float in [min, max] from the seeded generator.</summary>
        float RangeFloat(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Draws an int in [min, max] (both inclusive) from the seeded generator.</summary>
        int RangeInt(int min, int max) => max <= min ? min : rng.Next(min, max + 1);

        /// <summary>Draws a muted hull color, so the targets stay readable against the water.</summary>
        Color RandomHullColor()
        {
            return Color.HSVToRGB(
                (float)rng.NextDouble(),
                RangeFloat(0.25f, 0.65f),
                RangeFloat(0.35f, 0.75f));
        }

        /// <summary>Draws the generation area in the Scene view.</summary>
        void OnDrawGizmosSelected()
        {
            Vector2 center = AreaCenterXZ == Vector2.zero ? UsvStartXZ : AreaCenterXZ;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(new Vector3(center.x, 0f, center.y), AreaRadius);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(new Vector3(UsvStartXZ.x, 0f, UsvStartXZ.y), MinDistanceFromUsv);
        }
    }
}
