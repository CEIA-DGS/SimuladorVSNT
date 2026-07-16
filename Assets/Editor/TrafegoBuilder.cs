using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CenarioMaritimo.Boat;
using CenarioMaritimo.Real;

namespace CenarioMaritimo.EditorTools
{
    /// <summary>
    /// Adiciona TRÁFEGO DINÂMICO ao cenário real: embarcações de portes variados,
    /// sorteadas a partir dos VesselType (tipagem AIS), percorrendo rotas por dentro
    /// da baía. As rotas são snap-adas para água navegável usando o heightmap real
    /// (nenhum barco nasce em terra), e cada embarcação segue uma spline Catmull-Rom.
    /// </summary>
    public static class TrafegoBuilder
    {
        const string PASTA_DADOS = "Assets/CartaReal/Data";

        struct Rota
        {
            public EstiloCasco estilo;      // filtra quais VesselType podem usar esta rota
            public float minProf;          // profundidade mínima exigida (m)
            public int quantidade;         // barcos nesta rota
            public (float fx, float fz)[] wp; // waypoints em fração do domínio (0..1)
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

            float W = meta.ncols * meta.cell, H = meta.nrows * meta.cell;

            var antigo = FindChild(raizCena.transform, "TrafegoDinamico");
            if (antigo != null) Undo.DestroyObjectImmediate(antigo.gameObject);
            var grupo = new GameObject("TrafegoDinamico");
            grupo.transform.SetParent(raizCena.transform, false);
            Undo.RegisterCreatedObjectUndo(grupo, "Adicionar Tráfego");

            var rotas = new Rota[]
            {
                new Rota { estilo = EstiloCasco.Cargueiro, minProf = 8f, quantidade = 2, wp = new (float, float)[]
                    { (0.45f,0.14f),(0.42f,0.40f),(0.46f,0.64f),(0.50f,0.82f),(0.55f,0.58f),(0.51f,0.30f) } },
                new Rota { estilo = EstiloCasco.Media, minProf = 3f, quantidade = 3, wp = new (float, float)[]
                    { (0.30f,0.28f),(0.45f,0.34f),(0.60f,0.30f),(0.56f,0.14f),(0.36f,0.16f) } },
                new Rota { estilo = EstiloCasco.Lancha, minProf = 1.5f, quantidade = 4, wp = new (float, float)[]
                    { (0.46f,0.22f),(0.55f,0.26f),(0.53f,0.40f),(0.44f,0.34f) } },
            };

            int total = 0;
            foreach (var rota in rotas)
            {
                // tipos elegíveis para esta rota (pelo estilo de casco); fallback: todos
                var elegiveis = tipos.FindAll(t => t.estilo == rota.estilo);
                if (elegiveis.Count == 0) elegiveis = tipos;

                var wps = new List<Vector3>();
                foreach (var (fx, fz) in rota.wp)
                {
                    var (x, z) = AcharAgua(fx * W, fz * H, meta, elev, rota.minProf);
                    wps.Add(new Vector3(x, 0f, z));
                }

                // comprimento aproximado da rota, para espaçar os barcos
                float perimetro = 0f;
                for (int i = 0; i < wps.Count; i++)
                    perimetro += Vector3.Distance(wps[i], wps[(i + 1) % wps.Count]);

                for (int k = 0; k < rota.quantidade; k++)
                {
                    var tipo = elegiveis[Random.Range(0, elegiveis.Count)]; // sorteia o VesselType
                    var v = EmbarcacaoObstaculoFactory.Criar(grupo.transform, tipo, wps[0]);
                    var din = v.GetComponent<EmbarcacaoDinamica>();
                    din.waypoints = new List<Vector3>(wps);
                    din.loop = true;
                    din.distanciaInicial = perimetro * k / rota.quantidade; // espalha na rota
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

        // -------- acha a célula de água navegável mais próxima (busca em anel) --------
        static (float x, float z) AcharAgua(float x, float z, CartaMetadata meta, float[] elev, float minProf)
        {
            int col0 = Mathf.Clamp(Mathf.RoundToInt(x / meta.cell), 0, meta.ncols - 1);
            int row0 = Mathf.Clamp(Mathf.RoundToInt(z / meta.cell), 0, meta.nrows - 1);
            float limiar = -minProf;

            if (elev[row0 * meta.ncols + col0] <= limiar)
                return (x, z);

            int maxR = Mathf.Max(meta.ncols, meta.nrows);
            for (int r = 1; r < maxR; r++)
            {
                for (int dc = -r; dc <= r; dc++)
                    for (int dr = -r; dr <= r; dr++)
                    {
                        if (Mathf.Abs(dc) != r && Mathf.Abs(dr) != r) continue; // só o anel externo
                        int c = col0 + dc, rr = row0 + dr;
                        if (c < 0 || rr < 0 || c >= meta.ncols || rr >= meta.nrows) continue;
                        if (elev[rr * meta.ncols + c] <= limiar)
                            return (c * meta.cell, rr * meta.cell);
                    }
            }
            return (x, z);
        }

        // -------- carregamento dos dados (mesmos arquivos do builder) --------
        static CartaMetadata CarregarMeta()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/metadata.json");
            return ta == null ? null : JsonUtility.FromJson<CartaMetadata>(ta.text);
        }

        static float[] CarregarHeightmap(CartaMetadata meta)
        {
            if (meta == null) return null;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/heightmap.bytes");
            if (ta == null) return null;
            int total = meta.ncols * meta.nrows;
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
