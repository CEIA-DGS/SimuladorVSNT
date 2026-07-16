using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CenarioMaritimo.Real;
using CenarioMaritimo.Water;
using CenarioMaritimo.Boat;
using CenarioMaritimo.Sensor;

namespace CenarioMaritimo.EditorTools
{
    /// <summary>
    /// Constrói, com um clique, um cenário marítimo a partir de dados REAIS de uma
    /// carta ENC S-57 (batimetria, linha de costa e obstáculos), previamente
    /// extraídos para Assets/CartaReal/Data/ (heightmap.bytes + metadata.json +
    /// pontos.json). Totalmente independente do cenário fictício "Cenário Marítimo".
    /// </summary>
    public static class CenarioRealBuilder
    {
        const string RAIZ_NOME = "CenarioRealGerado";
        const string PASTA_DADOS = "Assets/CartaReal/Data";

        // --- parâmetros de construção ---
        const int PASSO = 3;              // subamostragem do heightmap (>=1). Maior = mais leve.
        const float EXAGERO_VERTICAL = 4f; // realce do relevo (terra/rasos ficam mais visíveis)
        const float RUIDO_TERRA = 3.5f;   // variação de altura da terra (que é plana no dado)

        // onda (visual + flutuação do barco)
        const float ONDA_AMPLITUDE = 0.15f;
        const float ONDA_ESCALA = 0.05f;
        const float ONDA_VELOCIDADE = 0.6f;

        const string MATERIAL_AGUA = "Assets/ShaderGraphSamples/Water/WaterLake.mat";

        [MenuItem("Cenário Real/1. Construir a partir da Carta")]
        public static void Construir()
        {
            var meta = CarregarMetadata();
            if (meta == null) return;
            float[] elev = CarregarHeightmap(meta);
            if (elev == null) return;

            var cena = SceneManager.GetActiveScene();
            var antigo = GameObject.Find(RAIZ_NOME);
            if (antigo != null) Undo.DestroyObjectImmediate(antigo);

            // Os dois cenários não coexistem na mesma cena (terrenos e HUDs se
            // sobrepõem). Remove a INSTÂNCIA do cenário fictício se estiver presente
            // — não afeta o gerador dele; é só recriar pelo menu "Cenário Marítimo".
            var ficticio = GameObject.Find("CenarioMaritimoGerado");
            if (ficticio != null) Undo.DestroyObjectImmediate(ficticio);

            var raiz = new GameObject(RAIZ_NOME);
            Undo.RegisterCreatedObjectUndo(raiz, "Construir Cenário Real");

            var geo = raiz.AddComponent<GeoReferenceUTM>();
            geo.originE = meta.originUTM_E;
            geo.originN = meta.originUTM_N;
            geo.utmZone = meta.utmZone;
            geo.utmSouth = meta.utmSouth;
            geo.originLat = meta.originLat;
            geo.originLon = meta.originLon;
            geo.carta = meta.chart;

            float larguraM = meta.ncols * meta.cell;
            float alturaM = meta.nrows * meta.cell;

            // ---------- terreno ----------
            var malha = GerarMalhaTerreno(elev, meta);
            var terrenoGO = new GameObject("TerrenoCarta");
            terrenoGO.transform.SetParent(raiz.transform, false);
            terrenoGO.AddComponent<MeshFilter>().sharedMesh = malha;
            terrenoGO.AddComponent<MeshRenderer>().sharedMaterial = CriarMaterialTerreno(meta);
            var colisor = terrenoGO.AddComponent<MeshCollider>();
            colisor.sharedMesh = malha;

            // ---------- água ----------
            var aguaGO = new GameObject("Agua");
            aguaGO.transform.SetParent(raiz.transform, false);
            aguaGO.transform.position = new Vector3(larguraM * 0.5f, 0.05f, alturaM * 0.5f);
            aguaGO.AddComponent<MeshFilter>().sharedMesh = GerarMalhaAgua(larguraM, alturaM);
            bool usaShaderAgua;
            aguaGO.AddComponent<MeshRenderer>().sharedMaterial = CriarMaterialAgua(out usaShaderAgua);
            if (!usaShaderAgua)
            {
                var w = aguaGO.AddComponent<WaterAnimator>();
                w.amplitude = ONDA_AMPLITUDE; w.escala = ONDA_ESCALA; w.velocidade = ONDA_VELOCIDADE;
            }

            // ---------- obstáculos reais ----------
            CriarPontos(raiz.transform, meta, elev);

            // ---------- embarcação + câmera ----------
            Vector3 posBarco = AcharPontoNavegavel(elev, meta);
            var barco = EmbarcacaoFactory.Criar(raiz.transform, posBarco, colisor,
                                                ONDA_AMPLITUDE, ONDA_ESCALA, ONDA_VELOCIDADE);
            // sensor de objetos dinâmicos (camada de percepção) a bordo do USV
            var sensor = barco.AddComponent<SensorEmbarcacoes>();
            sensor.alcance = 3500f;

            ConfigurarCamera(barco.transform);
            ConfigurarCartaTatica(raiz.transform, barco.transform, meta, elev);

            // ---------- iluminação ----------
            ConfigurarIluminacao(Mathf.Max(larguraM, alturaM));

            EditorSceneManager.MarkSceneDirty(cena);
            Selection.activeGameObject = barco;

            EditorUtility.DisplayDialog("Cenário real construído",
                $"Carta: {meta.chart}\n" +
                $"Área: {larguraM/1000f:F1} x {alturaM/1000f:F1} km\n" +
                $"Malha: {malha.vertexCount:N0} vértices (passo {PASSO})\n\n" +
                "Dê Play e navegue com WASD. Salve a cena (Ctrl+S).",
                "OK");
        }

        // ---------------- carregamento dos dados ----------------

        static CartaMetadata CarregarMetadata()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/metadata.json");
            if (ta == null)
            {
                EditorUtility.DisplayDialog("Dados não encontrados",
                    $"Não achei {PASTA_DADOS}/metadata.json.\n\n" +
                    "Rode o script de extração da carta primeiro (extrair_carta.py).", "OK");
                return null;
            }
            return JsonUtility.FromJson<CartaMetadata>(ta.text);
        }

        static float[] CarregarHeightmap(CartaMetadata meta)
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/heightmap.bytes");
            if (ta == null)
            {
                EditorUtility.DisplayDialog("Dados não encontrados",
                    $"Não achei {PASTA_DADOS}/heightmap.bytes.", "OK");
                return null;
            }
            byte[] bytes = ta.bytes;
            int total = meta.ncols * meta.nrows;
            if (bytes.Length < total * 4)
            {
                EditorUtility.DisplayDialog("Heightmap inválido",
                    $"Esperava {total * 4} bytes, achei {bytes.Length}.", "OK");
                return null;
            }
            var elev = new float[total];
            System.Buffer.BlockCopy(bytes, 0, elev, 0, total * 4); // float32 little-endian (Windows)
            return elev;
        }

        static float ElevacaoEm(float[] elev, CartaMetadata meta, int col, int row)
        {
            col = Mathf.Clamp(col, 0, meta.ncols - 1);
            row = Mathf.Clamp(row, 0, meta.nrows - 1);
            return elev[row * meta.ncols + col];
        }

        // ---------------- malhas ----------------

        static Mesh GerarMalhaTerreno(float[] elev, CartaMetadata meta)
        {
            int cols = (meta.ncols + PASSO - 1) / PASSO;
            int rows = (meta.nrows + PASSO - 1) / PASSO;
            float cell = meta.cell;

            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[verts.Length];
            float faixa = Mathf.Max(0.01f, meta.elevMax - meta.elevMin);

            for (int r = 0; r < rows; r++)
            {
                int srcRow = Mathf.Min(r * PASSO, meta.nrows - 1);
                for (int c = 0; c < cols; c++)
                {
                    int srcCol = Mathf.Min(c * PASSO, meta.ncols - 1);
                    float e = elev[srcRow * meta.ncols + srcCol];

                    float y = e;
                    if (e > 0.5f) // terra: leve ruído para não ficar um platô liso
                        y += (Mathf.PerlinNoise(srcCol * 0.08f, srcRow * 0.08f) - 0.5f) * 2f * RUIDO_TERRA;
                    y *= EXAGERO_VERTICAL;

                    int idx = r * cols + c;
                    verts[idx] = new Vector3(srcCol * cell, y, srcRow * cell);
                    uvs[idx] = new Vector2(Mathf.Clamp01((e - meta.elevMin) / faixa), 0.5f);
                }
            }

            var tris = new int[(cols - 1) * (rows - 1) * 6];
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int a = r * cols + c, bb = a + 1, cc = a + cols, d = cc + 1;
                    tris[t++] = a; tris[t++] = cc; tris[t++] = bb;
                    tris[t++] = bb; tris[t++] = cc; tris[t++] = d;
                }
            }

            var mesh = new Mesh { name = "TerrenoCarta", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh GerarMalhaAgua(float largura, float altura)
        {
            const int RES = 60;
            int lado = RES + 1;
            var verts = new Vector3[lado * lado];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= RES; j++)
                for (int i = 0; i <= RES; i++)
                {
                    int idx = j * lado + i;
                    verts[idx] = new Vector3((i / (float)RES - 0.5f) * largura, 0f, (j / (float)RES - 0.5f) * altura);
                    uvs[idx] = new Vector2(i / (float)RES * 40f, j / (float)RES * 40f);
                }
            var tris = new int[RES * RES * 6];
            int t = 0;
            for (int j = 0; j < RES; j++)
                for (int i = 0; i < RES; i++)
                {
                    int a = j * lado + i, b = a + 1, c = a + lado, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "Agua" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- materiais ----------------

        static Color CorPorElevacao(float e)
        {
            // cores mais saturadas/contrastadas para as regiões ficarem nítidas na
            // carta tática (terra verde forte, praia clara, faixas de profundidade
            // bem distintas do turquesa ao azul-marinho).
            if (e >= 6f) return new Color(0.16f, 0.45f, 0.14f);   // mata (verde forte)
            if (e >= 2f) return Color.Lerp(new Color(0.90f, 0.82f, 0.50f), new Color(0.22f, 0.60f, 0.18f), Mathf.InverseLerp(2f, 6f, e));
            if (e >= 0f) return new Color(0.95f, 0.88f, 0.60f);   // areia / entre-marés (clara)
            if (e >= -5f) return Color.Lerp(new Color(0.20f, 0.85f, 0.80f), new Color(0.10f, 0.55f, 0.80f), Mathf.InverseLerp(0f, -5f, e));  // turquesa -> azul claro
            if (e >= -20f) return Color.Lerp(new Color(0.10f, 0.55f, 0.80f), new Color(0.05f, 0.28f, 0.62f), Mathf.InverseLerp(-5f, -20f, e)); // azul médio
            return Color.Lerp(new Color(0.05f, 0.28f, 0.62f), new Color(0.02f, 0.08f, 0.30f), Mathf.InverseLerp(-20f, -75f, e));               // azul-marinho profundo
        }

        static Material CriarMaterialTerreno(CartaMetadata meta)
        {
            const int LARG = 256;
            var tex = new Texture2D(LARG, 1, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "RampaCarta" };
            var px = new Color[LARG];
            for (int i = 0; i < LARG; i++)
            {
                float e = Mathf.Lerp(meta.elevMin, meta.elevMax, i / (float)(LARG - 1));
                px[i] = CorPorElevacao(e);
            }
            tex.SetPixels(px); tex.Apply();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Smoothness", 0.15f);
            return mat;
        }

        static Material CriarMaterialAgua(out bool usaShader)
        {
            var referencia = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_AGUA);
            if (referencia != null) { usaShader = true; return new Material(referencia) { name = "Agua (Shader Graph)" }; }

            usaShader = false;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.10f, 0.42f, 0.55f, 0.72f));
            mat.SetFloat("_Smoothness", 0.85f);
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // ---------------- obstáculos ----------------

        static void CriarPontos(Transform pai, CartaMetadata meta, float[] elev)
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>($"{PASTA_DADOS}/pontos.json");
            if (ta == null) return;
            var lista = JsonUtility.FromJson<ListaPontos>("{\"itens\":" + ta.text + "}");
            if (lista == null || lista.itens == null) return;

            var grupo = new GameObject("Obstaculos");
            grupo.transform.SetParent(pai, false);

            foreach (var p in lista.itens)
            {
                int col = Mathf.RoundToInt(p.x / meta.cell);
                int row = Mathf.RoundToInt(p.z / meta.cell);
                float eTerreno = ElevacaoEm(elev, meta, col, row) * EXAGERO_VERTICAL;

                bool flutua = p.tipo.StartsWith("boia") || p.tipo == "baliza" || p.tipo == "farol";
                float y = flutua ? 0f : Mathf.Min(eTerreno, 0f);

                CriarMarcador(grupo.transform, p, new Vector3(p.x, y, p.z), flutua);
            }
        }

        static void CriarMarcador(Transform pai, PontoCarta p, Vector3 pos, bool flutua)
        {
            var cor = CorDoPonto(p);
            if (flutua)
            {
                var raiz = new GameObject(p.tipo);
                raiz.transform.SetParent(pai, false);
                raiz.transform.position = pos;

                var corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                corpo.transform.SetParent(raiz.transform, false);
                corpo.transform.localScale = new Vector3(3f, 2.5f, 3f);
                corpo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                Object.DestroyImmediate(corpo.GetComponent<Collider>());
                corpo.GetComponent<Renderer>().sharedMaterial = Mat(cor);

                if (p.tipo == "farol")
                {
                    var luz = new GameObject("Luz").AddComponent<Light>();
                    luz.transform.SetParent(raiz.transform, false);
                    luz.transform.localPosition = new Vector3(0f, 6f, 0f);
                    luz.type = LightType.Point; luz.color = new Color(1f, 0.9f, 0.5f);
                    luz.range = 120f; luz.intensity = 4f;
                }
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = p.tipo;
                go.transform.SetParent(pai, false);
                float s = p.tipo == "naufragio" ? 8f : 5f;
                go.transform.localScale = new Vector3(s, s * 0.5f, s);
                go.transform.position = pos + Vector3.up * (s * 0.15f);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = Mat(cor);
            }
        }

        static Material Mat(Color cor)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Smoothness", 0.4f);
            return m;
        }

        static Color CorDoPonto(PontoCarta p)
        {
            // cor S-57: 1 branco, 2 preto, 3 vermelho, 4 verde, 6 amarelo
            string primeira = string.IsNullOrEmpty(p.cor) ? "" : p.cor.Split(',')[0].Trim();
            switch (primeira)
            {
                case "3": return new Color(0.85f, 0.12f, 0.12f);
                case "4": return new Color(0.15f, 0.7f, 0.2f);
                case "6": return new Color(0.95f, 0.8f, 0.1f);
                case "1": return Color.white;
                case "2": return new Color(0.1f, 0.1f, 0.1f);
            }
            switch (p.tipo)
            {
                case "rochedo": return new Color(0.4f, 0.38f, 0.36f);
                case "naufragio": return new Color(0.45f, 0.2f, 0.15f);
                case "obstaculo": return new Color(0.9f, 0.45f, 0.1f);
                case "farol": return new Color(0.95f, 0.85f, 0.3f);
                case "baliza": return Color.white;
                default: return new Color(0.9f, 0.8f, 0.1f);
            }
        }

        // ---------------- posição inicial navegável ----------------

        static Vector3 AcharPontoNavegavel(float[] elev, CartaMetadata meta)
        {
            // procura, a partir do centro, uma célula com boa profundidade (água aberta)
            int cc = meta.ncols / 2, cr = meta.nrows / 2;
            for (int raio = 0; raio < Mathf.Max(meta.ncols, meta.nrows); raio += 4)
            {
                for (int dr = -raio; dr <= raio; dr += 4)
                    for (int dc = -raio; dc <= raio; dc += 4)
                    {
                        int c = cc + dc, r = cr + dr;
                        if (c < 0 || r < 0 || c >= meta.ncols || r >= meta.nrows) continue;
                        if (elev[r * meta.ncols + c] < -6f) // pelo menos ~6 m de profundidade
                            return new Vector3(c * meta.cell, 0.05f, r * meta.cell);
                    }
            }
            return new Vector3(meta.ncols * meta.cell * 0.5f, 0.05f, meta.nrows * meta.cell * 0.5f);
        }

        // ---------------- câmera e luz ----------------

        static void ConfigurarCamera(Transform alvo)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var todas = Object.FindObjectsByType<Camera>();
                cam = todas.Length > 0 ? todas[0] : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 8000f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = alvo.position + new Vector3(0f, 3f, -8f);
            cam.transform.LookAt(alvo.position);

            var seg = cam.GetComponent<CameraSeguidora>() ?? cam.gameObject.AddComponent<CameraSeguidora>();
            seg.alvo = alvo;
            seg.deslocamento = new Vector3(0f, 2.2f, -6f);
            seg.alturaAlvoOlhar = new Vector3(0f, 1f, 0f);
            seg.suavizacao = 4f;
        }

        static void ConfigurarCartaTatica(Transform pai, Transform barco, CartaMetadata meta, float[] elev)
        {
            var go = new GameObject("CameraCartaTatica");
            go.transform.SetParent(pai, false);
            go.AddComponent<Camera>();
            var ct = go.AddComponent<CartaTatica>();
            ct.alvo = barco;
            ct.alcance = 180f;
            ct.cartaMapa = GerarTexturaCartaMapa(meta, elev);
            ct.mundoTam = new Vector2(meta.ncols * meta.cell, meta.nrows * meta.cell);
        }

        // Imagem de carta (top-down, colorida por profundidade) para a visão geral
        // da carta tática — chapada e de alto contraste (terra verde, água azul),
        // sem iluminação nem água 3D. Linha 0 da textura = Sul (Norte fica em cima).
        static Texture2D GerarTexturaCartaMapa(CartaMetadata meta, float[] elev)
        {
            int step = Mathf.Max(1, meta.ncols / 800);
            int tw = Mathf.Max(1, meta.ncols / step), th = Mathf.Max(1, meta.nrows / step);
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CartaMapa" };

            var px = new Color[tw * th];
            for (int ty = 0; ty < th; ty++)
                for (int tx = 0; tx < tw; tx++)
                {
                    int c = Mathf.Min(tx * step, meta.ncols - 1);
                    int r = Mathf.Min(ty * step, meta.nrows - 1);
                    px[tx + ty * tw] = CorPorElevacao(elev[r * meta.ncols + c]);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void ConfigurarIluminacao(float escala)
        {
            Light sol = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sol = l; break; }
            if (sol == null) sol = new GameObject("Sol").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sol.color = new Color(1f, 0.96f, 0.86f);
            sol.intensity = 1.25f;
            sol.shadows = LightShadows.Soft;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.86f);
            RenderSettings.fogStartDistance = 400f;
            RenderSettings.fogEndDistance = 4500f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.75f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.25f);
        }
    }
}
