using System.Collections.Generic;
using UnityEngine;
using MaritimeScenario.Boat;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Builds obstacle vessels from a VesselType (data-driven): rolls the length
    /// within the type's range, derives beam/height/draft, picks the superstructure
    /// by style and sets the speed. Adds the DynamicVessel component (spline
    /// movement + state vector) and an AisBroadcaster with a unique MMSI. Intentionally
    /// low-poly to allow many vessels in the scene.
    /// </summary>
    public static class EmbarcacaoObstaculoFactory
    {
        private static readonly HashSet<uint> usedMmsis = new HashSet<uint>();

        static Material Lit(Color cor, float smoothness = 0.3f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static GameObject Criar(Transform pai, VesselType tipo, Vector3 pos)
        {
            float comp = tipo.RollLengthM();
            float boca = comp * tipo.BeamRatio;
            float calado = Mathf.Clamp(comp * 0.045f, 0.6f, 9f);
            float altura = Mathf.Clamp(comp * 0.06f, 1.8f, 15f); // hull freeboard
            Color cor = tipo.HullColor;

            var raiz = new GameObject($"Obst_{tipo.DisplayName}");
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = pos;

            // ---- guarantees a Rigidbody for the AisBroadcaster ----
            var rb = raiz.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = raiz.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            // ---- hull ----
            var casco = new GameObject("Casco");
            casco.transform.SetParent(raiz.transform, false);
            casco.AddComponent<MeshFilter>().sharedMesh = GerarCasco(comp, boca, altura, calado);
            var matCasco = Lit(cor);
            matCasco.SetFloat("_Cull", 0f);
            casco.AddComponent<MeshRenderer>().sharedMaterial = matCasco;

            var matConves = Lit(new Color(cor.r * 0.6f, cor.g * 0.6f, cor.b * 0.6f));

            // ---- superstructure by style ----
            switch (tipo.Style)
            {
                case HullStyle.Cargo:
                    // tall bridge at the stern
                    AddCaixa(casco.transform, new Vector3(0f, altura - calado + 6f, -comp * 0.35f),
                             new Vector3(boca * 0.8f, 12f, comp * 0.12f), Lit(Color.white));
                    // cargo decks (blocks)
                    for (int i = -1; i <= 2; i++)
                        AddCaixa(casco.transform, new Vector3(0f, altura - calado + 2f, comp * 0.10f * i),
                                 new Vector3(boca * 0.7f, 4f, comp * 0.08f), matConves);
                    // funnel
                    AddCilindro(casco.transform, new Vector3(0f, altura - calado + 13f, -comp * 0.35f),
                                new Vector3(2.5f, 3f, 2.5f), Lit(new Color(0.2f, 0.2f, 0.2f)));
                    break;
                case HullStyle.Medium:
                    AddCaixa(casco.transform, new Vector3(0f, altura - calado + 2.2f, -comp * 0.1f),
                             new Vector3(boca * 0.7f, 4.4f, comp * 0.28f), Lit(Color.white));
                    AddMastro(casco.transform, new Vector3(0f, altura - calado + 4.4f, -comp * 0.1f), 5f);
                    break;
                default: // Launch
                    AddCaixa(casco.transform, new Vector3(0f, altura - calado + 0.9f, -comp * 0.05f),
                             new Vector3(boca * 0.6f, 1.6f, comp * 0.35f), Lit(new Color(0.15f, 0.3f, 0.55f)));
                    break;
            }

            var din = raiz.AddComponent<DynamicVessel>();
            din.Length = comp;
            din.Beam = boca;
            din.Kind = tipo.DisplayName;
            din.Speed = tipo.RollSpeedMs();

            // ---- adds and configures the AisBroadcaster ----
            var ais = raiz.AddComponent<AisBroadcaster>();
            ais.mmsi = GenerateUniqueMmsi();
            ais.vesselType = MapStyleToAisType(tipo.Style);

            return raiz;
        }

        private static uint GenerateUniqueMmsi()
        {
            uint mmsi;
            int attempts = 0;
            do
            {
                mmsi = (uint)Random.Range(200000000, 799999999);
                attempts++;
                if (attempts > 100) break;
            }
            while (usedMmsis.Contains(mmsi));

            usedMmsis.Add(mmsi);
            return mmsi;
        }

        private static byte MapStyleToAisType(HullStyle style)
        {
            switch (style)
            {
                case HullStyle.Cargo: return 70;   // Cargo
                case HullStyle.Medium: return 60;  // Passenger / Other
                case HullStyle.Launch: return 37;  // Pleasure Craft
                default: return 99;
            }
        }

        // -------- part helpers --------

        static void AddCaixa(Transform pai, Vector3 pos, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Bloco";
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void AddCilindro(Transform pai, Vector3 pos, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Chamine";
            go.transform.SetParent(pai, false);
            go.transform.localPosition = pos;
            go.transform.localScale = escala;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void AddMastro(Transform pai, Vector3 baseP, float altura)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Mastro";
            go.transform.SetParent(pai, false);
            go.transform.localPosition = baseP + Vector3.up * altura * 0.5f;
            go.transform.localScale = new Vector3(0.3f, altura * 0.5f, 0.3f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.1f, 0.1f, 0.1f));
        }

        // -------- hull (pointed bow, flat stern), origin at the waterline --------
        static Mesh GerarCasco(float comp, float boca, float altura, float calado)
        {
            float mb = boca * 0.5f, L = comp * 0.5f;
            // (z, half-beam, bottom, deck) — stern -> bow
            var est = new (float z, float larg, float fundo, float conves)[]
            {
                (-L,          mb * 0.92f, -calado,        altura - calado),
                (-L * 0.55f,  mb,         -calado,        altura - calado),
                ( L * 0.30f,  mb * 0.92f, -calado * 0.9f, altura - calado),
                ( L * 0.72f,  mb * 0.55f, -calado * 0.5f, altura - calado + 0.5f),
            };
            var proa = new Vector3(0f, altura - calado + 0.5f, L);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var an = new int[est.Length, 4];
            for (int i = 0; i < est.Length; i++)
            {
                var e = est[i];
                an[i, 0] = verts.Count; verts.Add(new Vector3(-e.larg, e.fundo, e.z));
                an[i, 1] = verts.Count; verts.Add(new Vector3(e.larg, e.fundo, e.z));
                an[i, 2] = verts.Count; verts.Add(new Vector3(e.larg, e.conves, e.z));
                an[i, 3] = verts.Count; verts.Add(new Vector3(-e.larg, e.conves, e.z));
            }
            int ip = verts.Count; verts.Add(proa);

            void Q(int a, int b, int c, int d) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }

            Q(an[0, 0], an[0, 3], an[0, 2], an[0, 1]); // stern
            for (int i = 0; i < est.Length - 1; i++)
            {
                Q(an[i, 0], an[i, 1], an[i + 1, 1], an[i + 1, 0]); // bottom
                Q(an[i, 1], an[i, 2], an[i + 1, 2], an[i + 1, 1]); // starboard
                Q(an[i, 2], an[i, 3], an[i + 1, 3], an[i + 1, 2]); // deck
                Q(an[i, 3], an[i, 0], an[i + 1, 0], an[i + 1, 3]); // port
            }
            int u = est.Length - 1;
            tris.Add(an[u, 0]); tris.Add(an[u, 1]); tris.Add(ip);
            tris.Add(an[u, 1]); tris.Add(an[u, 2]); tris.Add(ip);
            tris.Add(an[u, 2]); tris.Add(an[u, 3]); tris.Add(ip);
            tris.Add(an[u, 3]); tris.Add(an[u, 0]); tris.Add(ip);

            var mesh = new Mesh { name = "CascoObstaculo" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
