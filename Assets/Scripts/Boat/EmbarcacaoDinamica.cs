using System.Collections.Generic;
using UnityEngine;

namespace CenarioMaritimo.Boat
{
    /// <summary>
    /// Embarcação DINÂMICA (obstáculo móvel): percorre uma trajetória suave em
    /// laço, definida por waypoints e interpolada por spline Catmull-Rom (passa
    /// pelos pontos, com curvas factíveis — nem reta, nem curva impossível).
    ///
    /// Expõe o "vetor de estado" que o relatório de requisitos pede para objetos
    /// dinâmicos: pose (transform), velocidade e rumo — base para o cálculo futuro
    /// de CPA/colisão. Implementação própria da spline: não depende de pacote
    /// externo, então move e é testável mesmo sem o com.unity.splines.
    /// </summary>
    public class EmbarcacaoDinamica : MonoBehaviour
    {
        public List<Vector3> waypoints = new();
        public float velocidade = 5f;   // m/s
        public bool loop = true;
        public float alturaAgua = 0f;
        [Tooltip("Distância inicial ao longo da rota (m) — espalha vários barcos na mesma rota.")]
        public float distanciaInicial = 0f;

        [Header("Dimensões (vetor de estado / colisão)")]
        public float comprimento = 20f;
        public float largura = 6f;
        public string tipo = "embarcacao";

        // ---- estado dinâmico exposto ----
        public Vector3 VelocidadeAtual { get; private set; }
        public float RumoGraus { get; private set; }

        Vector3[] amostras;
        float[] cumDist;
        float comprimentoTotal;
        float dist;
        Vector3 posAnterior;

        void Start()
        {
            ConstruirTabela();
            if (amostras != null && amostras.Length > 0)
            {
                dist = comprimentoTotal > 0.01f ? Mathf.Repeat(distanciaInicial, comprimentoTotal) : 0f;
                var p = PosicaoNaDistancia(dist); p.y = alturaAgua;
                transform.position = p;
                posAnterior = p;
            }
        }

        void ConstruirTabela()
        {
            if (waypoints == null || waypoints.Count < 2) return;
            int n = waypoints.Count;
            const int porSeg = 24;
            var pts = new List<Vector3>();
            int segs = loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = waypoints[(i - 1 + n) % n];
                Vector3 p1 = waypoints[i % n];
                Vector3 p2 = waypoints[(i + 1) % n];
                Vector3 p3 = waypoints[(i + 2) % n];
                for (int j = 0; j < porSeg; j++)
                    pts.Add(CatmullRom(p0, p1, p2, p3, j / (float)porSeg));
            }
            if (!loop) pts.Add(waypoints[n - 1]);
            amostras = pts.ToArray();

            cumDist = new float[amostras.Length];
            for (int i = 1; i < amostras.Length; i++)
                cumDist[i] = cumDist[i - 1] + Vector3.Distance(amostras[i - 1], amostras[i]);
            float extra = loop ? Vector3.Distance(amostras[amostras.Length - 1], amostras[0]) : 0f;
            comprimentoTotal = cumDist[amostras.Length - 1] + extra;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void Update()
        {
            if (amostras == null || amostras.Length < 2 || comprimentoTotal <= 0.01f) return;

            dist += velocidade * Time.deltaTime;
            dist = loop ? Mathf.Repeat(dist, comprimentoTotal) : Mathf.Clamp(dist, 0f, comprimentoTotal);

            Vector3 pos = PosicaoNaDistancia(dist);
            pos.y = alturaAgua;
            transform.position = pos;

            Vector3 delta = pos - posAnterior;
            Vector3 dirPlana = new Vector3(delta.x, 0f, delta.z);
            if (dirPlana.sqrMagnitude > 1e-4f)
            {
                var alvo = Quaternion.LookRotation(dirPlana.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, alvo, Time.deltaTime * 2f);
                RumoGraus = alvo.eulerAngles.y;
            }
            VelocidadeAtual = delta / Mathf.Max(Time.deltaTime, 1e-4f);
            posAnterior = pos;
        }

        Vector3 PosicaoNaDistancia(float d)
        {
            for (int i = 1; i < amostras.Length; i++)
                if (cumDist[i] >= d)
                {
                    float t = Mathf.InverseLerp(cumDist[i - 1], cumDist[i], d);
                    return Vector3.Lerp(amostras[i - 1], amostras[i], t);
                }
            if (loop)
            {
                float segLen = comprimentoTotal - cumDist[amostras.Length - 1];
                float t = segLen > 0.001f ? (d - cumDist[amostras.Length - 1]) / segLen : 0f;
                return Vector3.Lerp(amostras[amostras.Length - 1], amostras[0], Mathf.Clamp01(t));
            }
            return amostras[amostras.Length - 1];
        }

        void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count < 2) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawSphere(waypoints[i], 25f);
                Gizmos.DrawLine(waypoints[i], waypoints[(i + 1) % waypoints.Count]);
            }
        }
    }
}
