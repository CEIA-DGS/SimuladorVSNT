using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MaritimeScenario.Boat;
using MaritimeScenario.Real;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Adds DYNAMIC TRAFFIC to the real scenario: vessels of varied sizes, rolled
    /// from the VesselType assets (AIS typing), traveling routes inside the bay.
    /// The routes are snapped to navigable water using the real heightmap
    /// (no vessel spawns on land), and each vessel follows a Catmull-Rom spline.
    /// </summary>
    public static class TrafegoBuilder
    {
        const string PASTA_DADOS = "Assets/CartaReal/Data";

        struct Rota
        {
            public HullStyle estilo;      // filters which VesselType assets can use this route
            public float minProf;          // minimum required depth (m)
            public int quantidade;         // vessels on this route
            public (float fx, float fz)[] wp; // waypoints as a fraction of the domain (0..1)
        }

        [MenuItem("Cenário Real/3. Adicionar Tráfego (Embarcações)")]
        public static void Adicionar()
        {
            var raizCena = GameObject.Find("CenarioRealGerado");
            if (raizCena == null)
            {
                EditorUtility.DisplayDialog("Cenário ausente",
                    "Construa o cenário real primeiro (Cenário Real > 1).", "OK");
                return;
            }

            var meta = CarregarMeta();
            var elev = CarregarHeightmap(meta);
            if (meta == null || elev == null) return;

            var tipos = VesselTypeSetup.CarregarOuCriar();
            if (tipos.Count == 0) { EditorUtility.DisplayDialog("Sem tipos", "Nenhum VesselType disponível.", "OK"); return; }

            float W = meta.Columns * meta.CellSize, H = meta.Rows * meta.CellSize;

            var antigo = FindChild(raizCena.transform, "TrafegoDinamico");
            if (antigo != null) Undo.DestroyObjectImmediate(antigo.gameObject);
            var grupo = new GameObject("TrafegoDinamico");
            grupo.transform.SetParent(raizCena.transform, false);
            Undo.RegisterCreatedObjectUndo(grupo, "Adicionar Tráfego");

            var rotas = new Rota[]
            {
                new Rota { estilo = HullStyle.Cargo, minProf = 8f, quantidade = 2, wp = new (float, float)[]
                    { (0.45f,0.14f),(0.42f,0.40f),(0.46f,0.64f),(0.50f,0.82f),(0.55f,0.58f),(0.51f,0.30f) } },
                new Rota { estilo = HullStyle.Medium, minProf = 3f, quantidade = 3, wp = new (float, float)[]
                    { (0.30f,0.28f),(0.45f,0.34f),(0.60f,0.30f),(0.56f,0.14f),(0.36f,0.16f) } },
                new Rota { estilo = HullStyle.Launch, minProf = 1.5f, quantidade = 4, wp = new (float, float)[]
                    { (0.46f,0.22f),(0.55f,0.26f),(0.53f,0.40f),(0.44f,0.34f) } },
            };

            int total = 0;
            foreach (var rota in rotas)
            {
                // types eligible for this route (by hull style); fallback: all of them
                var elegiveis = tipos.FindAll(t => t.Style == rota.estilo);
                if (elegiveis.Count == 0) elegiveis = tipos;

                var wps = new List<Vector3>();
                foreach (var (fx, fz) in rota.wp)
                {
                    var (x, z) = AcharAgua(fx * W, fz * H, meta, elev, rota.minProf);
                    wps.Add(new Vector3(x, 0f, z));
                }

                // approximate route length, used to space out the vessels
                float perimetro = 0f;
                for (int i = 0; i < wps.Count; i++)
                    perimetro += Vector3.Distance(wps[i], wps[(i + 1) % wps.Count]);

                for (int k = 0; k < rota.quantidade; k++)
                {
                    var tipo = elegiveis[Random.Range(0, elegiveis.Count)]; // rolls the VesselType
                    var v = EmbarcacaoObstaculoFactory.Criar(grupo.transform, tipo, wps[0]);
                    var din = v.GetComponent<DynamicVessel>();
                    din.Waypoints = new List<Vector3>(wps);
                    din.Loop = true;
                    din.InitialDistance = perimetro * k / rota.quantidade; // spreads them along the route
                    total++;
                }
            }

            EditorSceneManager.MarkSceneDirty(raizCena.scene);
            Selection.activeGameObject = grupo;
            EditorUtility.DisplayDialog("Tráfego adicionado",
                $"{total} embarcações dinâmicas em {rotas.Length} rotas.\n\n" +
                "Dê Play para vê-las navegando. Para teste de desempenho, aumente as " +
                "quantidades em TrafegoBuilder e observe o FPS (Game > Stats).", "OK");
        }

        // -------- finds the nearest navigable water cell (ring search) --------
        static (float x, float z) AcharAgua(float x, float z, ChartMetadata meta, float[] elev, float minProf)
        {
            int col0 = Mathf.Clamp(Mathf.RoundToInt(x / meta.CellSize), 0, meta.Columns - 1);
            int row0 = Mathf.Clamp(Mathf.RoundToInt(z / meta.CellSize), 0, meta.Rows - 1);
            float limiar = -minProf;

            if (elev[row0 * meta.Columns + col0] <= limiar)
                return (x, z);

            int maxR = Mathf.Max(meta.Columns, meta.Rows);
            for (int r = 1; r < maxR; r++)
            {
                for (int dc = -r; dc <= r; dc++)
                    for (int dr = -r; dr <= r; dr++)
                    {
                        if (Mathf.Abs(dc) != r && Mathf.Abs(dr) != r) continue; // outer ring only
                        int c = col0 + dc, rr = row0 + dr;
                        if (c < 0 || rr < 0 || c >= meta.Columns || rr >= meta.Rows) continue;
                        if (elev[rr * meta.Columns + c] <= limiar)
                            return (c * meta.CellSize, rr * meta.CellSize);
                    }
            }
            return (x, z);
        }

        // -------- data loading (same files as the builder) --------
        static ChartMetadata CarregarMeta()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/metadata.json");
            return ta == null ? null : JsonUtility.FromJson<ChartMetadata>(ta.text);
        }

        static float[] CarregarHeightmap(ChartMetadata meta)
        {
            if (meta == null) return null;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/heightmap.bytes");
            if (ta == null) return null;
            int total = meta.Columns * meta.Rows;
            var elev = new float[total];
            System.Buffer.BlockCopy(ta.bytes, 0, elev, 0, total * 4);
            return elev;
        }

        static Transform FindChild(Transform pai, string nome)
        {
            foreach (Transform c in pai) if (c.name == nome) return c;
            return null;
        }
    }
}
