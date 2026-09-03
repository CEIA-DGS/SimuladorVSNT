using System.Collections.Generic;
using UnityEngine;
using MaritimeScenario.Boat;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Builds the vessel (VSNT/DGS-15) by code: tapered hull, inflatable tubes,
    /// console, "A"-frame mast with sensor, outboard motor. Kept separate so it
    /// can be reused by the fictional and the real scenario without duplication.
    /// Real specs (PRISMA Project): length 4.5 m, beam 2.0 m, draft 0.55 m,
    /// waterline -> mast top 2.30 m.
    /// </summary>
    public static class EmbarcacaoFactory
    {
        /// <summary>Hull length of the real VSNT/DGS-15, in meters.</summary>
        public const float COMPRIMENTO = 4.5f;
        /// <summary>Hull beam of the real VSNT/DGS-15, in meters.</summary>
        public const float LARGURA = 2.0f;
        /// <summary>Draught of the real VSNT/DGS-15, in meters.</summary>
        public const float CALADO = 0.55f;
        /// <summary>Mast height of the real VSNT/DGS-15 above the waterline, in meters.</summary>
        public const float ALTURA_MASTRO = 2.30f;

        static Material Lit(Color cor, float smoothness = 0.4f, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", smoothness);
            if (metallic > 0f) m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static GameObject Criar(Transform pai, Vector3 posicao, Collider colisorTerreno,
                                       float ondaAmplitude, float ondaEscala, float ondaVelocidade,
                                       float alturaFlutuacao = 0.05f)
        {
            var raiz = new GameObject("USV_DGS15");
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = posicao;

            var cascoGO = new GameObject("Casco");
            cascoGO.transform.SetParent(raiz.transform, false);
            cascoGO.AddComponent<MeshFilter>().sharedMesh = GerarMalhaCasco(COMPRIMENTO, LARGURA, CALADO);
            var matCasco = Lit(new Color(0.12f, 0.12f, 0.13f), 0.35f);
            matCasco.SetFloat("_Cull", 0f); // double-sided
            cascoGO.AddComponent<MeshRenderer>().sharedMaterial = matCasco;

            var matTubo = Lit(new Color(0.65f, 0.08f, 0.08f), 0.5f);
            CriarTubo(cascoGO.transform, +1f, matTubo);
            CriarTubo(cascoGO.transform, -1f, matTubo);

            float alturaConvesMeio = CALADO * 0.80f;
            float zConsole = -0.5f;

            var consoleGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            consoleGO.name = "Console";
            consoleGO.transform.SetParent(cascoGO.transform, false);
            consoleGO.transform.localPosition = new Vector3(0f, alturaConvesMeio + 0.28f, zConsole);
            consoleGO.transform.localScale = new Vector3(LARGURA * 0.55f, 0.56f, 0.45f);
            Object.DestroyImmediate(consoleGO.GetComponent<Collider>());
            consoleGO.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.75f, 0.76f, 0.78f), 0.4f);

            var matMastro = Lit(new Color(0.08f, 0.08f, 0.08f));
            float alturaApice = alturaConvesMeio + (ALTURA_MASTRO - alturaConvesMeio) * 0.55f;
            var baseEsq = new Vector3(-0.4f, alturaConvesMeio + 0.4f, zConsole);
            var baseDir = new Vector3(0.4f, alturaConvesMeio + 0.4f, zConsole);
            var apice = new Vector3(0f, alturaApice, zConsole - 0.1f);
            var topoMastro = new Vector3(0f, ALTURA_MASTRO, zConsole - 0.1f);
            CriarHaste(cascoGO.transform, baseEsq, apice, 0.045f, matMastro);
            CriarHaste(cascoGO.transform, baseDir, apice, 0.045f, matMastro);
            CriarHaste(cascoGO.transform, apice, topoMastro, 0.045f, matMastro);
            CriarHaste(cascoGO.transform, apice + new Vector3(0.15f, 0f, 0f), apice + new Vector3(0.15f, 0.9f, -0.05f), 0.012f, matMastro);
            CriarHaste(cascoGO.transform, apice + new Vector3(-0.15f, 0f, 0f), apice + new Vector3(-0.22f, 0.75f, 0.05f), 0.012f, matMastro);

            var sensorGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sensorGO.name = "Sensor";
            sensorGO.transform.SetParent(cascoGO.transform, false);
            sensorGO.transform.localPosition = topoMastro;
            sensorGO.transform.localScale = new Vector3(0.24f, 0.20f, 0.24f);
            Object.DestroyImmediate(sensorGO.GetComponent<Collider>());
            sensorGO.GetComponent<Renderer>().sharedMaterial = Lit(Color.white, 0.6f);

            float zPopa = -COMPRIMENTO * 0.5f - 0.25f;
            var matMotor = Lit(new Color(0.08f, 0.08f, 0.08f), 0.45f);

            var capuz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capuz.name = "MotorCapuz";
            capuz.transform.SetParent(cascoGO.transform, false);
            capuz.transform.localPosition = new Vector3(0f, alturaConvesMeio + 0.15f, zPopa);
            capuz.transform.localScale = new Vector3(0.42f, 0.32f, 0.55f);
            Object.DestroyImmediate(capuz.GetComponent<Collider>());
            capuz.GetComponent<Renderer>().sharedMaterial = matMotor;

            var perna = GameObject.CreatePrimitive(PrimitiveType.Cube);
            perna.name = "MotorPerna";
            perna.transform.SetParent(cascoGO.transform, false);
            float alturaPerna = alturaConvesMeio + 0.15f - (-CALADO * 1.3f);
            perna.transform.localPosition = new Vector3(0f, (alturaConvesMeio + 0.15f - CALADO * 1.3f) * 0.5f, zPopa + 0.08f);
            perna.transform.localScale = new Vector3(0.18f, alturaPerna, 0.22f);
            Object.DestroyImmediate(perna.GetComponent<Collider>());
            perna.GetComponent<Renderer>().sharedMaterial = matMotor;

            var helice = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            helice.name = "Helice";
            helice.transform.SetParent(cascoGO.transform, false);
            helice.transform.localPosition = new Vector3(0f, -CALADO * 1.3f, zPopa + 0.08f);
            helice.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            helice.transform.localScale = new Vector3(0.03f, 0.28f, 0.03f);
            Object.DestroyImmediate(helice.GetComponent<Collider>());
            helice.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.6f, 0.6f, 0.62f), 0.7f, 0.6f);

            var boat = raiz.AddComponent<BoatController>();
            boat.Length = COMPRIMENTO;
            boat.Beam = LARGURA;
            boat.WaveAmplitude = ondaAmplitude;
            boat.WaveScale = ondaEscala;
            boat.WaveSpeed = ondaVelocidade;
            boat.TerrainCollider = colisorTerreno;
            boat.BuoyancyHeight = alturaFlutuacao;

            return raiz;
        }

        static void CriarTubo(Transform pai, float lado, Material mat)
        {
            var tubo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tubo.name = lado > 0 ? "TuboBoreste" : "TuboBombordo";
            tubo.transform.SetParent(pai, false);
            tubo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tubo.transform.localScale = new Vector3(0.16f, COMPRIMENTO * 0.46f, 0.16f);
            tubo.transform.localPosition = new Vector3(lado * LARGURA * 0.52f, CALADO * 0.55f, 0f);
            Object.DestroyImmediate(tubo.GetComponent<Collider>());
            tubo.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void CriarHaste(Transform pai, Vector3 a, Vector3 b, float espessura, Material mat)
        {
            var haste = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            haste.name = "Haste";
            haste.transform.SetParent(pai, false);
            haste.transform.localPosition = (a + b) * 0.5f;
            haste.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            haste.transform.localScale = new Vector3(espessura, Vector3.Distance(a, b) * 0.5f, espessura);
            Object.DestroyImmediate(haste.GetComponent<Collider>());
            haste.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Mesh GerarMalhaCasco(float comprimento, float boca, float calado)
        {
            float meiaBoca = boca * 0.5f;
            float L = comprimento * 0.5f;

            var estacoes = new (float z, float larg, float fundo, float conves)[]
            {
                (-L,          meiaBoca * 0.85f, -calado * 0.85f, calado * 0.55f),
                (-L * 0.60f,  meiaBoca * 0.98f, -calado * 0.95f, calado * 0.65f),
                (-L * 0.09f,  meiaBoca,         -calado,          calado * 0.75f),
                ( L * 0.40f,  meiaBoca * 0.75f, -calado * 0.60f,  calado * 0.95f),
                ( L * 0.76f,  meiaBoca * 0.35f, -calado * 0.15f,  calado * 1.20f),
            };
            var ponta = new Vector3(0f, calado * 1.30f, L);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var aneis = new int[estacoes.Length, 4];

            for (int i = 0; i < estacoes.Length; i++)
            {
                var e = estacoes[i];
                aneis[i, 0] = verts.Count; verts.Add(new Vector3(-e.larg, e.fundo, e.z));
                aneis[i, 1] = verts.Count; verts.Add(new Vector3(e.larg, e.fundo, e.z));
                aneis[i, 2] = verts.Count; verts.Add(new Vector3(e.larg, e.conves, e.z));
                aneis[i, 3] = verts.Count; verts.Add(new Vector3(-e.larg, e.conves, e.z));
            }
            int idxPonta = verts.Count;
            verts.Add(ponta);

            void Quad(int a, int b, int c, int d)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }

            Quad(aneis[0, 0], aneis[0, 3], aneis[0, 2], aneis[0, 1]);
            for (int i = 0; i < estacoes.Length - 1; i++)
            {
                int bl0 = aneis[i, 0], br0 = aneis[i, 1], tr0 = aneis[i, 2], tl0 = aneis[i, 3];
                int bl1 = aneis[i + 1, 0], br1 = aneis[i + 1, 1], tr1 = aneis[i + 1, 2], tl1 = aneis[i + 1, 3];
                Quad(bl0, br0, br1, bl1);
                Quad(br0, tr0, tr1, br1);
                Quad(tr0, tl0, tl1, tr1);
                Quad(tl0, bl0, bl1, tl1);
            }
            int u = estacoes.Length - 1;
            tris.Add(aneis[u, 0]); tris.Add(aneis[u, 1]); tris.Add(idxPonta);
            tris.Add(aneis[u, 1]); tris.Add(aneis[u, 2]); tris.Add(idxPonta);
            tris.Add(aneis[u, 2]); tris.Add(aneis[u, 3]); tris.Add(idxPonta);
            tris.Add(aneis[u, 3]); tris.Add(aneis[u, 0]); tris.Add(idxPonta);

            var mesh = new Mesh { name = "CascoUSV" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
