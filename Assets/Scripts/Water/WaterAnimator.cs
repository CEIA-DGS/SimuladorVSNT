using UnityEngine;

namespace CenarioMaritimo.Water
{
    /// <summary>
    /// Anima suavemente os vértices de uma malha plana de água, usando a mesma
    /// fórmula de onda (OndaUtil) que a flutuação da embarcação — assim o barco
    /// sobe e desce em sincronia com o que se vê. Não tem função física — o
    /// modelo hidrodinâmico de verdade fica a cargo do restante do simulador.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class WaterAnimator : MonoBehaviour
    {
        public float amplitude = 0.15f;
        public float velocidade = 0.6f;
        public float escala = 0.05f;

        Mesh mesh;
        Vector3[] baseVerts;
        Vector3[] verts;

        void Start()
        {
            mesh = GetComponent<MeshFilter>().mesh; // instancia (cópia) automaticamente
            baseVerts = mesh.vertices;
            verts = new Vector3[baseVerts.Length];
        }

        void Update()
        {
            if (mesh == null) return;
            for (int i = 0; i < baseVerts.Length; i++)
            {
                var v = baseVerts[i];
                v.y = OndaUtil.Altura(v.x, v.z, Time.time, amplitude, escala, velocidade);
                verts[i] = v;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }
}
