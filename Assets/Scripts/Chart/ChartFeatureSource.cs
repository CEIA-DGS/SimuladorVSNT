using System.Collections.Generic;
using UnityEngine;

namespace CenarioMaritimo.Chart
{
    /// <summary>
    /// Guarda, na cena, a "fonte de verdade" vetorial da carta (polígonos LNDARE/DEPARE
    /// e pontos de boias/rochedos) gerada junto com o cenário 3D. É essa lista e não
    /// a malha 3D que representa a carta náutica e que deve ser exportada/usada pelos
    /// módulos de navegação e percepção (Relatório do Digital Twin: "vetor para
    /// navegar, malha para mostrar na tela").
    /// </summary>
    public class ChartFeatureSource : MonoBehaviour
    {
        public List<ChartFeature> poligonos = new();
        public List<ChartPointFeature> pontos = new();

        [Header("Desenho de depuração (Scene view)")]
        public bool desenharGizmos = true;

        void OnDrawGizmos()
        {
            if (!desenharGizmos) return;

            foreach (var f in poligonos)
            {
                Gizmos.color = f.objectClass == ObjClass.LNDARE
                    ? new Color(0.3f, 0.85f, 0.35f)
                    : Color.Lerp(new Color(0.4f, 0.85f, 1f), new Color(0.05f, 0.15f, 0.55f),
                                 Mathf.InverseLerp(0f, 20f, f.DRVAL2));

                DesenharAnel(f.ringXZ);
                DesenharAnel(f.holeXZ);
            }

            Gizmos.color = Color.yellow;
            foreach (var p in pontos)
                Gizmos.DrawSphere(new Vector3(p.posicaoXZ.x, 1f, p.posicaoXZ.y), 1.2f);
        }

        static void DesenharAnel(List<Vector2> r)
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
