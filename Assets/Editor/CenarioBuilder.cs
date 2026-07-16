using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CenarioMaritimo.Chart;
using CenarioMaritimo.Geo;
using CenarioMaritimo.Water;
using CenarioMaritimo.Boat;

namespace CenarioMaritimo.EditorTools
{
    /// <summary>
    /// Ferramenta de Editor que gera, com um clique, um cenário marítimo fictício
    /// e compacto: uma ilha principal com relevo (+ uma ilhota/recife secundário),
    /// várias faixas de profundidade (DEPARE, com buracos reais entre elas), água
    /// animada, vegetação, iluminação/céu, objetos notáveis (rochedos, boias,
    /// farol) e uma embarcação controlável com flutuação simples.
    ///
    /// Ao mesmo tempo, guarda a versão VETORIAL desses elementos (os anéis de
    /// polígono usados para moldar o relevo) em um ChartFeatureSource — é essa
    /// lista, e não a malha 3D, que representa a "carta náutica" do cenário e que
    /// deve ser exportada para uso pelos módulos de navegação/percepção.
    ///
    /// Uso:
    ///   1. Menu "Cenário Marítimo > 1. Gerar Cenário Completo".
    ///   2. Menu "Cenário Marítimo > 2. Exportar Carta Náutica (GeoJSON + PNG)".
    ///   3. Salve a cena (Ctrl+S) para persistir o resultado.
    /// </summary>
    public static class CenarioBuilder
    {
        const string RAIZ_NOME = "CenarioMaritimoGerado";

        // ---- ilha principal (metros, plano local X,Z) ----
        const float LAND_BASE_RADIUS = 30f;
        const float LAND_NOISE_AMP = 8f;
        const float LAND_PEAK_HEIGHT = 9f;

        // ---- faixas de profundidade (DEPARE), da mais rasa pra mais funda ----
        readonly struct FaixaProfundidade
        {
            public readonly float DRVAL1, DRVAL2, largura, ruido;
            public FaixaProfundidade(float drval1, float drval2, float largura, float ruido)
            {
                DRVAL1 = drval1; DRVAL2 = drval2; this.largura = largura; this.ruido = ruido;
            }
        }

        static readonly FaixaProfundidade[] FAIXAS =
        {
            new FaixaProfundidade(0f, 2f, 10f, 4f),
            new FaixaProfundidade(2f, 5f, 14f, 5f),
            new FaixaProfundidade(5f, 10f, 18f, 6f),
            new FaixaProfundidade(10f, 20f, 22f, 7f),
        };

        // ---- ilhota secundária (recife) ----
        const float ILHA2_CENTRO_X = 68f;
        const float ILHA2_CENTRO_Z = 50f;
        const float ILHA2_RAIO = 9f;
        const float ILHA2_RUIDO = 3.5f;
        const float ILHA2_PICO = 4f;

        // ---- malha e carta ----
        const int GRID_RES = 140;
        const float DOMAIN_MARGIN = 1.15f;
        const int ANGLE_STEPS_CHART = 56;
        const int ANGLE_STEPS_ILHA2 = 20;

        // ---- onda (única fonte de verdade — água e barco leem daqui) ----
        const float ONDA_AMPLITUDE = 0.15f;
        const float ONDA_ESCALA = 0.05f;
        const float ONDA_VELOCIDADE = 0.6f;

        // ---- embarcação (specs reais do VSNT/DGS-15, Projeto PRISMA) ----
        const float BARCO_COMPRIMENTO = 4.5f;   // comprimento do casco
        const float BARCO_LARGURA = 2.0f;       // boca
        const float BARCO_CALADO = 0.55f;       // calado (parte submersa)
        const float BARCO_ALTURA_MASTRO = 2.30f; // linha d'água até o topo do mastro

        [MenuItem("Cenário Marítimo/1. Gerar Cenário Completo")]
        public static void GerarCenarioCompleto()
        {
            var cena = SceneManager.GetActiveScene();

            var antigo = GameObject.Find(RAIZ_NOME);
            if (antigo != null) Undo.DestroyObjectImmediate(antigo);

            var raiz = new GameObject(RAIZ_NOME);
            Undo.RegisterCreatedObjectUndo(raiz, "Gerar Cenário Marítimo");

            raiz.AddComponent<GeoReferenceOrigin>();
            var fonte = raiz.AddComponent<ChartFeatureSource>();

            var rampa = GerarTexturaRampa();
            var malhaTerreno = GerarMalhaTerreno(out var poligonos);
            fonte.poligonos = poligonos;

            var terrenoGO = new GameObject("TerrenoOceano");
            terrenoGO.transform.SetParent(raiz.transform, false);
            terrenoGO.AddComponent<MeshFilter>().sharedMesh = malhaTerreno;
            terrenoGO.AddComponent<MeshRenderer>().sharedMaterial = CriarMaterialTerreno(rampa);
            var colisorTerreno = terrenoGO.AddComponent<MeshCollider>();
            colisorTerreno.sharedMesh = malhaTerreno;

            float raioAprox = RaioExternoAproximado();

            var aguaGO = new GameObject("Agua");
            aguaGO.transform.SetParent(raiz.transform, false);
            aguaGO.transform.position = new Vector3(0f, 0.05f, 0f);
            aguaGO.AddComponent<MeshFilter>().sharedMesh = GerarMalhaAgua(raioAprox * DOMAIN_MARGIN, 44);
            aguaGO.AddComponent<MeshRenderer>().sharedMaterial = CriarMaterialAgua(out bool usandoShaderGraph);
            if (!usandoShaderGraph)
            {
                // Sem o material do Shader Graph, a onda visual fica por conta do
                // WaterAnimator (CPU). Com o Shader Graph, a própria água já anima
                // sozinha (GPU) — a flutuação do barco continua igual nos dois casos,
                // porque ela usa a fórmula (OndaUtil), não a malha renderizada.
                var water = aguaGO.AddComponent<WaterAnimator>();
                water.amplitude = ONDA_AMPLITUDE;
                water.escala = ONDA_ESCALA;
                water.velocidade = ONDA_VELOCIDADE;
            }

            CriarPontosNotaveis(raiz.transform, fonte, raioAprox);
            CriarFarol(raiz.transform);
            CriarVegetacao(raiz.transform);
            var embarcacao = CriarEmbarcacaoPlaceholder(raiz.transform, raioAprox, colisorTerreno);
            ConfigurarIluminacaoECeu(raioAprox);
            ConfigurarPosProcessamento();
            PosicionarCamera(embarcacao.transform, raioAprox);

            EditorSceneManager.MarkSceneDirty(cena);
            Selection.activeGameObject = raiz;

            EditorUtility.DisplayDialog("Cenário gerado",
                $"Cenário marítimo criado em '{RAIZ_NOME}'.\n\n" +
                "Próximo passo: menu 'Cenário Marítimo > 2. Exportar Carta Náutica' " +
                "para gerar o GeoJSON e a imagem da carta.\n\n" +
                "Não esqueça de salvar a cena (Ctrl+S).",
                "OK");
        }

        [MenuItem("Cenário Marítimo/2. Exportar Carta Náutica (GeoJSON + PNG)")]
        public static void ExportarCartaNautica()
        {
            var fonte = Object.FindAnyObjectByType<ChartFeatureSource>();
            var geo = Object.FindAnyObjectByType<GeoReferenceOrigin>();
            if (fonte == null || geo == null)
            {
                EditorUtility.DisplayDialog("Nada para exportar",
                    "Gere o cenário primeiro (menu 'Cenário Marítimo > 1. Gerar Cenário Completo').",
                    "OK");
                return;
            }

            string pasta = ChartExporter.Exportar(fonte, geo, RaioExternoAproximado() * DOMAIN_MARGIN);
            EditorUtility.DisplayDialog("Carta exportada", $"Arquivos gerados em:\n{pasta}", "OK");
        }

        // -------- forma do terreno (função de verdade única: usada pela malha, pela carta e pelos props) --------

        static float RaioExternoAproximado()
        {
            float r = LAND_BASE_RADIUS;
            foreach (var f in FAIXAS) r += f.largura;
            return r;
        }

        static float ProfundidadeMaxima => FAIXAS[FAIXAS.Length - 1].DRVAL2;

        static float LandRadius(float ang)
        {
            float nx = Mathf.Cos(ang) * 0.85f + 4.31f;
            float nz = Mathf.Sin(ang) * 0.85f + 7.92f;
            float n = Mathf.PerlinNoise(nx, nz); // 0..1, contínuo e periódico em ang (círculo no espaço do ruído)
            return LAND_BASE_RADIUS + (n - 0.5f) * 2f * LAND_NOISE_AMP;
        }

        static float RaioExternoFaixa(float ang, int indice, float raioInterno)
        {
            var f = FAIXAS[indice];
            float nx = Mathf.Cos(ang) * 0.85f + (indice + 1) * 17.3f + 3f;
            float nz = Mathf.Sin(ang) * 0.85f + (indice + 1) * 29.1f + 6f;
            float n = Mathf.PerlinNoise(nx, nz);
            return raioInterno + f.largura + (n - 0.5f) * 2f * f.ruido;
        }

        static float AlturaIlhaPrincipal(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float ang = Mathf.Atan2(z, x);
            float landR = LandRadius(ang);

            if (r <= landR)
            {
                float t = landR > 0.001f ? r / landR : 0f;
                return LAND_PEAK_HEIGHT * (1f - t * t);
            }

            float rAnterior = landR;
            for (int i = 0; i < FAIXAS.Length; i++)
            {
                float rFaixa = RaioExternoFaixa(ang, i, rAnterior);
                if (r <= rFaixa || i == FAIXAS.Length - 1)
                {
                    float t = Mathf.Clamp01(Mathf.InverseLerp(rAnterior, rFaixa, r));
                    return -Mathf.Lerp(FAIXAS[i].DRVAL1, FAIXAS[i].DRVAL2, t);
                }
                rAnterior = rFaixa;
            }
            return -ProfundidadeMaxima;
        }

        static float RaioIlha2(float ang)
        {
            float nx = Mathf.Cos(ang) * 0.9f + 61f;
            float nz = Mathf.Sin(ang) * 0.9f + 44f;
            float n = Mathf.PerlinNoise(nx, nz);
            return ILHA2_RAIO + (n - 0.5f) * 2f * ILHA2_RUIDO;
        }

        static float AlturaIlhaSecundaria(float x, float z)
        {
            float dx = x - ILHA2_CENTRO_X, dz = z - ILHA2_CENTRO_Z;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            float ang = Mathf.Atan2(dz, dx);
            float raio = RaioIlha2(ang);
            if (r > raio) return -ProfundidadeMaxima - 1f; // bem abaixo — não interfere no Max() abaixo

            float t = r / Mathf.Max(raio, 0.001f);
            return ILHA2_PICO * (1f - t * t);
        }

        static float AlturaBase(float x, float z)
        {
            return Mathf.Max(AlturaIlhaPrincipal(x, z), AlturaIlhaSecundaria(x, z));
        }

        static List<ChartFeature> GerarPoligonosCarta()
        {
            var lista = new List<ChartFeature>();
            var aneisPorFaixa = new List<Vector2>[FAIXAS.Length];
            for (int i = 0; i < FAIXAS.Length; i++) aneisPorFaixa[i] = new List<Vector2>();
            var anelIlha = new List<Vector2>();

            for (int step = 0; step < ANGLE_STEPS_CHART; step++)
            {
                float ang = step * Mathf.PI * 2f / ANGLE_STEPS_CHART;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                float landR = LandRadius(ang);
                anelIlha.Add(new Vector2(cos * landR, sin * landR));

                float rAnterior = landR;
                for (int i = 0; i < FAIXAS.Length; i++)
                {
                    float rFaixa = RaioExternoFaixa(ang, i, rAnterior);
                    aneisPorFaixa[i].Add(new Vector2(cos * rFaixa, sin * rFaixa));
                    rAnterior = rFaixa;
                }
            }

            // Cada faixa tem um buraco de verdade: o anel da faixa mais interna
            // (ou a ilha, no caso da faixa mais rasa) — sem sobreposição.
            for (int i = 0; i < FAIXAS.Length; i++)
            {
                lista.Add(new ChartFeature
                {
                    objectClass = ObjClass.DEPARE,
                    DRVAL1 = FAIXAS[i].DRVAL1,
                    DRVAL2 = FAIXAS[i].DRVAL2,
                    ringXZ = aneisPorFaixa[i],
                    holeXZ = i == 0 ? anelIlha : aneisPorFaixa[i - 1]
                });
            }
            lista.Add(new ChartFeature { objectClass = ObjClass.LNDARE, ringXZ = anelIlha });

            var anelIlha2 = new List<Vector2>();
            for (int step = 0; step < ANGLE_STEPS_ILHA2; step++)
            {
                float ang = step * Mathf.PI * 2f / ANGLE_STEPS_ILHA2;
                float raio = RaioIlha2(ang);
                anelIlha2.Add(new Vector2(ILHA2_CENTRO_X + Mathf.Cos(ang) * raio, ILHA2_CENTRO_Z + Mathf.Sin(ang) * raio));
            }
            lista.Add(new ChartFeature { objectClass = ObjClass.LNDARE, ringXZ = anelIlha2 });

            return lista;
        }

        // -------- malhas 3D --------

        static Mesh GerarMalhaTerreno(out List<ChartFeature> poligonos)
        {
            float domain = RaioExternoAproximado() * DOMAIN_MARGIN;
            int res = GRID_RES;
            int ladoVerts = res + 1;
            var vertices = new Vector3[ladoVerts * ladoVerts];
            var uvs = new Vector2[vertices.Length];
            var tris = new int[res * res * 6];
            float passo = (2f * domain) / res;
            float profMax = ProfundidadeMaxima;

            for (int zi = 0; zi <= res; zi++)
            {
                for (int xi = 0; xi <= res; xi++)
                {
                    float x = -domain + xi * passo;
                    float z = -domain + zi * passo;

                    float baseHeight = AlturaBase(x, z);
                    float elevT = Mathf.InverseLerp(-profMax, LAND_PEAK_HEIGHT, baseHeight);
                    float ampDetalhe = Mathf.Lerp(0.25f, 1.4f, elevT);
                    float ruido = (Mathf.PerlinNoise(x * 0.11f + 91.7f, z * 0.11f + 13.3f) - 0.5f) * 2f;
                    float height = baseHeight + ruido * ampDetalhe;

                    int idx = zi * ladoVerts + xi;
                    vertices[idx] = new Vector3(x, height, z);

                    float elevFinal = Mathf.InverseLerp(-profMax, LAND_PEAK_HEIGHT, height);
                    float vLadrilhado = (x + z) * 0.18f; // sem limite — a textura repete sozinha (wrapModeV = Repeat)
                    uvs[idx] = new Vector2(Mathf.Clamp01(elevFinal), vLadrilhado);
                }
            }

            int ti = 0;
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    int a = zi * ladoVerts + xi;
                    int b = a + 1;
                    int c = a + ladoVerts;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var mesh = new Mesh { name = "TerrenoOceano" };
            mesh.indexFormat = vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            poligonos = GerarPoligonosCarta();
            return mesh;
        }

        static Mesh GerarMalhaAgua(float tamanho, int res)
        {
            int ladoVerts = res + 1;
            var vertices = new Vector3[ladoVerts * ladoVerts];
            var uvs = new Vector2[vertices.Length];
            var tris = new int[res * res * 6];
            float passo = tamanho * 2f / res;

            for (int zi = 0; zi <= res; zi++)
            {
                for (int xi = 0; xi <= res; xi++)
                {
                    float x = -tamanho + xi * passo;
                    float z = -tamanho + zi * passo;
                    int idx = zi * ladoVerts + xi;
                    vertices[idx] = new Vector3(x, 0f, z);
                    uvs[idx] = new Vector2((float)xi / res * 8f, (float)zi / res * 8f);
                }
            }

            int ti = 0;
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    int a = zi * ladoVerts + xi;
                    int b = a + 1;
                    int c = a + ladoVerts;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var mesh = new Mesh { name = "Agua" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // -------- materiais --------

        static Texture2D GerarTexturaRampa()
        {
            const int LARGURA = 256;
            const int ALTURA = 64;
            var tex = new Texture2D(LARGURA, ALTURA, TextureFormat.RGBA32, false)
            {
                wrapModeU = TextureWrapMode.Clamp, // eixo da elevação: não deve dar a volta
                wrapModeV = TextureWrapMode.Repeat, // eixo "ladrilhado" pela superfície: repete
                filterMode = FilterMode.Bilinear,
                name = "RampaTerrenoOceano"
            };

            var paradas = new (float t, Color cor)[]
            {
                (0.00f, new Color(0.02f, 0.09f, 0.28f)), // azul profundo
                (0.45f, new Color(0.08f, 0.32f, 0.60f)), // azul médio
                (0.55f, new Color(0.22f, 0.70f, 0.75f)), // água rasa / turquesa
                (0.665f, new Color(0.86f, 0.80f, 0.58f)), // areia (linha d'água)
                (0.72f, new Color(0.80f, 0.72f, 0.46f)), // areia
                (0.85f, new Color(0.32f, 0.55f, 0.22f)), // vegetação
                (1.00f, new Color(0.22f, 0.33f, 0.17f)), // topo / mata densa
            };

            var pixels = new Color[LARGURA * ALTURA];
            for (int y = 0; y < ALTURA; y++)
            {
                for (int x = 0; x < LARGURA; x++)
                {
                    Color baseCor = AmostrarRampa(paradas, x / (float)(LARGURA - 1));

                    // Ruído sutil de brilho, pra quebrar o efeito "degradê liso demais"
                    // quando visto de perto, sem precisar de uma textura tileável de verdade.
                    float ruido = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f;
                    float jitter = 1f + ruido * 0.22f;

                    pixels[y * LARGURA + x] = new Color(
                        Mathf.Clamp01(baseCor.r * jitter),
                        Mathf.Clamp01(baseCor.g * jitter),
                        Mathf.Clamp01(baseCor.b * jitter),
                        1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Color AmostrarRampa((float t, Color cor)[] paradas, float t)
        {
            for (int i = 0; i < paradas.Length - 1; i++)
            {
                if (t >= paradas[i].t && t <= paradas[i + 1].t)
                {
                    float local = Mathf.InverseLerp(paradas[i].t, paradas[i + 1].t, t);
                    return Color.Lerp(paradas[i].cor, paradas[i + 1].cor, local);
                }
            }
            return paradas[paradas.Length - 1].cor;
        }

        const string TEXTURA_DETALHE_NORMAL = "Assets/ShaderGraphSamples/Common/Textures/Moss_N.png";

        static Material CriarMaterialTerreno(Texture2D rampa)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", rampa);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);

            // Detail Normal Map: dá relevo real de superfície (bumps) sem mudar a
            // cor — a textura de musgo entra só pelo canal de normal, a cor continua
            // vindo 100% da rampa acima. _DetailAlbedoMapScale = 0 neutraliza
            // completamente a contribuição de cor do detalhe (ver LitInput.hlsl:
            // ScaleDetailAlbedo(a, 0) = 1, ou seja, sem efeito).
            var normalDetalhe = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURA_DETALHE_NORMAL);
            if (normalDetalhe != null)
            {
                mat.SetTexture("_DetailNormalMap", normalDetalhe);
                mat.SetFloat("_DetailNormalMapScale", 0.7f);
                mat.SetFloat("_DetailAlbedoMapScale", 0f);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(25f, 25f));
                mat.EnableKeyword("_DETAIL_SCALED");
                mat.DisableKeyword("_DETAIL_MULX2");
            }

            return mat;
        }

        const string MATERIAL_AGUA_SHADER_GRAPH = "Assets/ShaderGraphSamples/Water/WaterLake.mat";

        static Material CriarMaterialAgua(out bool usandoShaderGraph)
        {
            var referencia = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_AGUA_SHADER_GRAPH);
            if (referencia != null)
            {
                usandoShaderGraph = true;
                return new Material(referencia) { name = "Agua (Shader Graph)" };
            }

            usandoShaderGraph = false;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.10f, 0.45f, 0.55f, 0.72f));
            mat.SetFloat("_Smoothness", 0.85f);
            mat.SetFloat("_Metallic", 0.05f);

            // Torna o material transparente (equivalente a marcar "Surface Type: Transparent" no Inspector)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // -------- objetos notáveis --------

        static void CriarPontosNotaveis(Transform pai, ChartFeatureSource fonte, float raioAprox)
        {
            float[] angulosRochedoGraus = { 35f, 110f, 200f, 260f };
            foreach (var angGraus in angulosRochedoGraus)
            {
                float ang = angGraus * Mathf.Deg2Rad;
                float landR = LandRadius(ang);
                float rFaixaRasa = RaioExternoFaixa(ang, 0, landR);
                float r = landR + (rFaixaRasa - landR) * 0.5f; // no meio da faixa mais rasa (0-2m)
                var pos = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                CriarRochedo(pai, pos);
                fonte.pontos.Add(new ChartPointFeature
                {
                    objectClass = PointObjClass.UWTROC,
                    posicaoXZ = pos,
                    nome = $"Rochedo_{angGraus:0}"
                });
            }

            // pequeno recife de rochedos ao redor da ilhota secundária
            for (int i = 0; i < 3; i++)
            {
                float ang = (i * 120f + 15f) * Mathf.Deg2Rad;
                float r = RaioIlha2(ang) * 1.15f;
                var pos = new Vector2(ILHA2_CENTRO_X + Mathf.Cos(ang) * r, ILHA2_CENTRO_Z + Mathf.Sin(ang) * r);
                CriarRochedo(pai, pos);
                fonte.pontos.Add(new ChartPointFeature
                {
                    objectClass = PointObjClass.UWTROC,
                    posicaoXZ = pos,
                    nome = $"Rochedo_Recife_{i}"
                });
            }

            var boias = new (float angGraus, float raio, Color cor, string nome)[]
            {
                (170f, raioAprox * 0.55f, new Color(0.9f, 0.1f, 0.1f), "Boia_Bombordo"),
                (190f, raioAprox * 0.55f, new Color(0.1f, 0.8f, 0.2f), "Boia_Boreste"),
            };
            foreach (var b in boias)
            {
                float ang = b.angGraus * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Cos(ang) * b.raio, Mathf.Sin(ang) * b.raio);
                CriarBoia(pai, pos, b.cor, b.nome);
                fonte.pontos.Add(new ChartPointFeature
                {
                    objectClass = PointObjClass.BOYSHP,
                    posicaoXZ = pos,
                    nome = b.nome
                });
            }
        }

        static readonly string[] ROCHEDO_PREFABS =
        {
            "Assets/ShaderGraphSamples/Common/Meshes/Rock_A_01.prefab",
            "Assets/ShaderGraphSamples/Common/Meshes/Rock_A_02.prefab",
        };

        static void CriarRochedo(Transform pai, Vector2 posXZ)
        {
            float escala = Random.Range(1.3f, 2.6f);
            float h = AlturaBase(posXZ.x, posXZ.y);

            var caminhoPrefab = ROCHEDO_PREFABS[Random.Range(0, ROCHEDO_PREFABS.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);

            if (prefab != null)
            {
                // Rocha real (malha + material do Shader Graph), importada do pacote oficial.
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "Rochedo";
                go.transform.SetParent(pai, false);
                go.transform.localScale = Vector3.one * escala * 0.6f;
                go.transform.position = new Vector3(posXZ.x, h - escala * 0.15f, posXZ.y);
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else
            {
                // Fallback (sem o prefab importado): esfera simples, como antes.
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Rochedo";
                go.transform.SetParent(pai, false);
                go.transform.localScale = new Vector3(escala, escala * 0.6f, escala);
                go.transform.position = new Vector3(posXZ.x, h - escala * 0.3f, posXZ.y);
                go.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0.35f, 0.34f, 0.33f));
                mat.SetFloat("_Smoothness", 0.1f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }

        static void CriarBoia(Transform pai, Vector2 posXZ, Color cor, string nome)
        {
            var raiz = new GameObject(nome);
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = new Vector3(posXZ.x, 0f, posXZ.y);

            var corpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            corpo.name = "Corpo";
            corpo.transform.SetParent(raiz.transform, false);
            corpo.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            corpo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Object.DestroyImmediate(corpo.GetComponent<Collider>());

            var topo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            topo.name = "Topo";
            topo.transform.SetParent(raiz.transform, false);
            topo.transform.localScale = Vector3.one * 0.5f;
            topo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            Object.DestroyImmediate(topo.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", cor);
            mat.SetFloat("_Smoothness", 0.4f);
            corpo.GetComponent<Renderer>().sharedMaterial = mat;
            topo.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void CriarFarol(Transform pai)
        {
            const float xF = 6f, zF = -4f; // sempre dentro da ilha (raio mínimo da ilha é bem maior que isso)
            float hF = AlturaBase(xF, zF);

            var raiz = new GameObject("Farol");
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = new Vector3(xF, hF, zF);

            var torre = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            torre.name = "Torre";
            torre.transform.SetParent(raiz.transform, false);
            torre.transform.localScale = new Vector3(1.2f, 4f, 1.2f);
            torre.transform.localPosition = new Vector3(0f, 4f, 0f);
            Object.DestroyImmediate(torre.GetComponent<Collider>());
            var matTorre = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matTorre.SetColor("_BaseColor", Color.white);
            torre.GetComponent<Renderer>().sharedMaterial = matTorre;

            var topo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            topo.name = "Lanterna";
            topo.transform.SetParent(raiz.transform, false);
            topo.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            topo.transform.localPosition = new Vector3(0f, 8.6f, 0f);
            Object.DestroyImmediate(topo.GetComponent<Collider>());
            var matTopo = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matTopo.SetColor("_BaseColor", Color.red);
            topo.GetComponent<Renderer>().sharedMaterial = matTopo;

            var luzGO = new GameObject("LuzFarol");
            luzGO.transform.SetParent(raiz.transform, false);
            luzGO.transform.localPosition = new Vector3(0f, 8.6f, 0f);
            var luz = luzGO.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.85f, 0.4f);
            luz.range = 40f;
            luz.intensity = 3f;
        }

        static void CriarVegetacao(Transform pai)
        {
            const int ALVO = 45;
            int criadas = 0, tentativas = 0;

            while (criadas < ALVO && tentativas < ALVO * 8)
            {
                tentativas++;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float landR = LandRadius(ang);
                float r = Random.Range(0f, landR * 0.85f); // evita a faixa de praia
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float h = AlturaBase(x, z);

                if (h < LAND_PEAK_HEIGHT * 0.15f) continue; // perto demais da praia
                if (h > LAND_PEAK_HEIGHT * 0.92f) continue; // topo rochoso, sem árvore

                CriarArvore(pai, x, h, z);
                criadas++;
            }
        }

        const string ARVORE_PREFAB = "Assets/ShaderGraphSamples/AiNav/Prefabs/BanyanTree.prefab";

        static void CriarArvore(Transform pai, float x, float y, float z)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ARVORE_PREFAB);
            if (prefab != null)
            {
                // Árvore real (malha + material), importada do pacote de AI Navigation.
                var arv = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                arv.name = "Arvore";
                arv.transform.SetParent(pai, false);
                arv.transform.position = new Vector3(x, y, z);
                arv.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                arv.transform.localScale = Vector3.one * Random.Range(0.35f, 0.55f); // ajustar depois de conferir na cena
                foreach (var col in arv.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(col);
                return;
            }

            // Fallback (sem o prefab importado): tronco + copa simples, como antes.
            var raiz = new GameObject("Arvore");
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = new Vector3(x, y, z);
            raiz.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            raiz.transform.localScale = Vector3.one * Random.Range(0.7f, 1.4f);

            var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tronco.name = "Tronco";
            tronco.transform.SetParent(raiz.transform, false);
            tronco.transform.localScale = new Vector3(0.25f, 1.1f, 0.25f);
            tronco.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Object.DestroyImmediate(tronco.GetComponent<Collider>());
            var matTronco = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matTronco.SetColor("_BaseColor", new Color(0.36f, 0.25f, 0.16f));
            tronco.GetComponent<Renderer>().sharedMaterial = matTronco;

            var copa = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            copa.name = "Copa";
            copa.transform.SetParent(raiz.transform, false);
            copa.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
            copa.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            Object.DestroyImmediate(copa.GetComponent<Collider>());
            var matCopa = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matCopa.SetColor("_BaseColor", new Color(Random.Range(0.15f, 0.25f), Random.Range(0.42f, 0.55f), Random.Range(0.15f, 0.22f)));
            copa.GetComponent<Renderer>().sharedMaterial = matCopa;
        }

        static GameObject CriarEmbarcacaoPlaceholder(Transform pai, float raioAprox, Collider colisorTerreno)
        {
            var raiz = new GameObject("USV_DGS15");
            raiz.transform.SetParent(pai, false);
            raiz.transform.position = new Vector3(0f, 0f, -raioAprox * 0.6f);

            // -------- casco (afunilado, popa reta / proa em ponta) --------
            var cascoGO = new GameObject("Casco");
            cascoGO.transform.SetParent(raiz.transform, false);
            var malhaCasco = GerarMalhaCasco(BARCO_COMPRIMENTO, BARCO_LARGURA, BARCO_CALADO);
            cascoGO.AddComponent<MeshFilter>().sharedMesh = malhaCasco;
            var matCasco = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matCasco.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f)); // preto fosco, como o DGS-15
            matCasco.SetFloat("_Smoothness", 0.35f);
            matCasco.SetFloat("_Cull", 0f); // dupla face — não corre risco de "buraco" por causa da ordem dos vértices
            cascoGO.AddComponent<MeshRenderer>().sharedMaterial = matCasco;

            // -------- tubos infláveis laterais (características de um RIB) --------
            var matTubo = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matTubo.SetColor("_BaseColor", new Color(0.65f, 0.08f, 0.08f));
            matTubo.SetFloat("_Smoothness", 0.5f);
            CriarTubo(cascoGO.transform, +1f, matTubo);
            CriarTubo(cascoGO.transform, -1f, matTubo);

            // -------- console --------
            var matConsole = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matConsole.SetColor("_BaseColor", new Color(0.75f, 0.76f, 0.78f));
            matConsole.SetFloat("_Smoothness", 0.4f);
            float zConsole = -0.5f;
            float alturaConvesMeio = BARCO_CALADO * 0.80f;
            var consoleGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            consoleGO.name = "Console";
            consoleGO.transform.SetParent(cascoGO.transform, false);
            consoleGO.transform.localPosition = new Vector3(0f, alturaConvesMeio + 0.28f, zConsole);
            consoleGO.transform.localScale = new Vector3(BARCO_LARGURA * 0.55f, 0.56f, 0.45f);
            Object.DestroyImmediate(consoleGO.GetComponent<Collider>());
            consoleGO.GetComponent<Renderer>().sharedMaterial = matConsole;

            // -------- mastro em estrutura de "A" (como nas fotos: duas hastes
            // diagonais partindo do convés + uma vertical até o sensor) --------
            var matMastro = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matMastro.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.08f));
            float alturaTopoMastro = BARCO_ALTURA_MASTRO;
            float alturaApice = alturaConvesMeio + (alturaTopoMastro - alturaConvesMeio) * 0.55f;
            var baseEsquerda = new Vector3(-0.4f, alturaConvesMeio + 0.4f, zConsole);
            var baseDireita = new Vector3(0.4f, alturaConvesMeio + 0.4f, zConsole);
            var apice = new Vector3(0f, alturaApice, zConsole - 0.1f);
            var topoMastro = new Vector3(0f, alturaTopoMastro, zConsole - 0.1f);
            CriarHaste(cascoGO.transform, baseEsquerda, apice, 0.045f, matMastro);
            CriarHaste(cascoGO.transform, baseDireita, apice, 0.045f, matMastro);
            CriarHaste(cascoGO.transform, apice, topoMastro, 0.045f, matMastro);

            // antenas finas (whip antennas)
            CriarHaste(cascoGO.transform, apice + new Vector3(0.15f, 0f, 0f), apice + new Vector3(0.15f, 0.9f, -0.05f), 0.012f, matMastro);
            CriarHaste(cascoGO.transform, apice + new Vector3(-0.15f, 0f, 0f), apice + new Vector3(-0.22f, 0.75f, 0.05f), 0.012f, matMastro);

            var sensorGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sensorGO.name = "Sensor";
            sensorGO.transform.SetParent(cascoGO.transform, false);
            sensorGO.transform.localPosition = topoMastro;
            sensorGO.transform.localScale = new Vector3(0.24f, 0.20f, 0.24f);
            Object.DestroyImmediate(sensorGO.GetComponent<Collider>());
            var matSensor = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matSensor.SetColor("_BaseColor", Color.white);
            matSensor.SetFloat("_Smoothness", 0.6f);
            sensorGO.GetComponent<Renderer>().sharedMaterial = matSensor;

            // -------- motor de popa (capuz + perna + hélice) --------
            var matMotor = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matMotor.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.08f));
            matMotor.SetFloat("_Smoothness", 0.45f);
            float zPopa = -BARCO_COMPRIMENTO * 0.5f - 0.25f;

            var capuzGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            capuzGO.name = "MotorCapuz";
            capuzGO.transform.SetParent(cascoGO.transform, false);
            capuzGO.transform.localPosition = new Vector3(0f, alturaConvesMeio + 0.15f, zPopa);
            capuzGO.transform.localScale = new Vector3(0.42f, 0.32f, 0.55f);
            Object.DestroyImmediate(capuzGO.GetComponent<Collider>());
            capuzGO.GetComponent<Renderer>().sharedMaterial = matMotor;

            var pernaGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pernaGO.name = "MotorPerna";
            pernaGO.transform.SetParent(cascoGO.transform, false);
            float alturaPerna = alturaConvesMeio + 0.15f - (-BARCO_CALADO * 1.3f);
            pernaGO.transform.localPosition = new Vector3(0f, (alturaConvesMeio + 0.15f - BARCO_CALADO * 1.3f) * 0.5f, zPopa + 0.08f);
            pernaGO.transform.localScale = new Vector3(0.18f, alturaPerna, 0.22f);
            Object.DestroyImmediate(pernaGO.GetComponent<Collider>());
            pernaGO.GetComponent<Renderer>().sharedMaterial = matMotor;

            var heliceGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            heliceGO.name = "Helice";
            heliceGO.transform.SetParent(cascoGO.transform, false);
            heliceGO.transform.localPosition = new Vector3(0f, -BARCO_CALADO * 1.3f, zPopa + 0.08f);
            heliceGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            heliceGO.transform.localScale = new Vector3(0.03f, 0.28f, 0.03f);
            Object.DestroyImmediate(heliceGO.GetComponent<Collider>());
            var matHelice = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matHelice.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.62f));
            matHelice.SetFloat("_Smoothness", 0.7f);
            matHelice.SetFloat("_Metallic", 0.6f);
            heliceGO.GetComponent<Renderer>().sharedMaterial = matHelice;

            var boat = raiz.AddComponent<BoatController>();
            boat.comprimento = BARCO_COMPRIMENTO;
            boat.largura = BARCO_LARGURA;
            boat.amplitudeOnda = ONDA_AMPLITUDE;
            boat.escalaOnda = ONDA_ESCALA;
            boat.velocidadeOnda = ONDA_VELOCIDADE;
            boat.colisorTerreno = colisorTerreno;
            // O casco já modela o calado (fica com Y=0 na própria linha d'água),
            // então a "altura de flutuação" do controlador é a altura real da água
            // (mesmo Y usado no GameObject "Agua"), não mais um valor arbitrário.
            boat.alturaFlutuacao = 0.05f;

            return raiz;
        }

        static void CriarTubo(Transform pai, float lado, Material mat)
        {
            var tubo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tubo.name = lado > 0 ? "TuboBoreste" : "TuboBombordo";
            tubo.transform.SetParent(pai, false);
            tubo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // deita o cilindro ao longo do comprimento (Z)
            tubo.transform.localScale = new Vector3(0.16f, BARCO_COMPRIMENTO * 0.46f, 0.16f);
            tubo.transform.localPosition = new Vector3(lado * BARCO_LARGURA * 0.52f, BARCO_CALADO * 0.55f, 0f);
            Object.DestroyImmediate(tubo.GetComponent<Collider>());
            tubo.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Cilindro fino esticado entre dois pontos (locais) — usado pra hastes,
        // estrutura do mastro e antenas, sem precisar calcular ângulos na mão.
        static void CriarHaste(Transform pai, Vector3 a, Vector3 b, float espessura, Material mat)
        {
            var haste = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            haste.name = "Haste";
            haste.transform.SetParent(pai, false);
            Vector3 meio = (a + b) * 0.5f;
            float comprimento = Vector3.Distance(a, b);
            haste.transform.localPosition = meio;
            haste.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            haste.transform.localScale = new Vector3(espessura, comprimento * 0.5f, espessura);
            Object.DestroyImmediate(haste.GetComponent<Collider>());
            haste.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Mesh GerarMalhaCasco(float comprimento, float boca, float calado)
        {
            float meiaBoca = boca * 0.5f;
            float L = comprimento * 0.5f;

            // (z, meia-largura, altura do fundo, altura do convés) — da popa pra proa.
            // Mais estações = afunilamento mais suave (silhueta menos "quadrada").
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
            var aneis = new int[estacoes.Length, 4]; // BL, BR, TR, TL por estação

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

            Quad(aneis[0, 0], aneis[0, 3], aneis[0, 2], aneis[0, 1]); // tampa de popa

            for (int i = 0; i < estacoes.Length - 1; i++)
            {
                int bl0 = aneis[i, 0], br0 = aneis[i, 1], tr0 = aneis[i, 2], tl0 = aneis[i, 3];
                int bl1 = aneis[i + 1, 0], br1 = aneis[i + 1, 1], tr1 = aneis[i + 1, 2], tl1 = aneis[i + 1, 3];

                Quad(bl0, br0, br1, bl1); // fundo
                Quad(br0, tr0, tr1, br1); // boreste
                Quad(tr0, tl0, tl1, tr1); // convés
                Quad(tl0, bl0, bl1, tl1); // bombordo
            }

            int ultimo = estacoes.Length - 1;
            tris.Add(aneis[ultimo, 0]); tris.Add(aneis[ultimo, 1]); tris.Add(idxPonta);
            tris.Add(aneis[ultimo, 1]); tris.Add(aneis[ultimo, 2]); tris.Add(idxPonta);
            tris.Add(aneis[ultimo, 2]); tris.Add(aneis[ultimo, 3]); tris.Add(idxPonta);
            tris.Add(aneis[ultimo, 3]); tris.Add(aneis[ultimo, 0]); tris.Add(idxPonta);

            var mesh = new Mesh { name = "CascoUSV" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // -------- iluminação, céu e câmera --------

        static void ConfigurarIluminacaoECeu(float raioAprox)
        {
            Light sol = null;
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional) { sol = l; break; }
            }
            if (sol == null)
            {
                var go = new GameObject("Sol (Luz Direcional)");
                sol = go.AddComponent<Light>();
                sol.type = LightType.Directional;
            }
            sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sol.color = new Color(1f, 0.95f, 0.85f);
            sol.intensity = 1.25f;
            sol.shadows = LightShadows.Soft;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.85f);
            RenderSettings.fogStartDistance = raioAprox * 1.3f;
            RenderSettings.fogEndDistance = raioAprox * 3.2f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.75f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.68f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.25f);
        }

        static void ConfigurarPosProcessamento()
        {
            // O "Global Volume" que vem no template tinha um perfil contaminado com
            // componentes de teste internos do Unity (CopyPasteTestComponent, TestVolume
            // etc.), todos com valor neutro — na prática, nenhum efeito aparecia. Criamos
            // um perfil limpo, só com os efeitos que interessam pro visual do cenário.
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            perfil.name = "PerfilCenarioMaritimo";

            var bloom = perfil.Add<Bloom>(true);
            bloom.threshold.value = 0.9f;
            bloom.intensity.value = 0.25f;
            bloom.scatter.value = 0.6f;

            var cor = perfil.Add<ColorAdjustments>(true);
            cor.postExposure.value = 0.1f;
            cor.contrast.value = 8f;
            cor.saturation.value = 12f;

            var branco = perfil.Add<WhiteBalance>(true);
            branco.temperature.value = 8f; // levemente quente, clima tropical

            var vinheta = perfil.Add<Vignette>(true);
            vinheta.intensity.value = 0.25f;
            vinheta.smoothness.value = 0.4f;

            var tom = perfil.Add<Tonemapping>(true);
            tom.mode.value = TonemappingMode.ACES;

            const string pasta = "Assets/Settings";
            const string caminho = pasta + "/PerfilPosProcessamentoCenario.asset";
            if (!AssetDatabase.IsValidFolder(pasta))
                AssetDatabase.CreateFolder("Assets", "Settings");
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(caminho) != null)
                AssetDatabase.DeleteAsset(caminho);
            AssetDatabase.CreateAsset(perfil, caminho);

            var volumeGO = GameObject.Find("Global Volume");
            if (volumeGO == null) volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.GetComponent<Volume>();
            if (volume == null) volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = perfil;
        }

        static void PosicionarCamera(Transform embarcacao, float raioAprox)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var todas = Object.FindObjectsByType<Camera>();
                if (todas.Length > 0)
                {
                    cam = todas[0];
                }
                else
                {
                    var go = new GameObject("Main Camera");
                    go.tag = "MainCamera";
                    cam = go.AddComponent<Camera>();
                }
            }

            // Enquadramento estático (usado no modo de edição e ao entrar em Play).
            cam.transform.position = new Vector3(0f, LAND_PEAK_HEIGHT * 6f, -raioAprox * 1.35f);
            cam.transform.LookAt(Vector3.zero);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, raioAprox * 6f);
            cam.clearFlags = CameraClearFlags.Skybox;

            // Em Play, passa a seguir a embarcação (LateUpdate só roda durante o Play).
            // Os valores são setados explicitamente (não só nos defaults do campo)
            // porque a Main Camera não é recriada a cada geração — um componente já
            // existente de uma geração anterior manteria valores antigos serializados.
            var seguidora = cam.GetComponent<CameraSeguidora>();
            if (seguidora == null) seguidora = cam.gameObject.AddComponent<CameraSeguidora>();
            seguidora.alvo = embarcacao;
            seguidora.deslocamento = new Vector3(0f, 2.2f, -6f);
            seguidora.alturaAlvoOlhar = new Vector3(0f, 1f, 0f);
            seguidora.suavizacao = 4f;
        }
    }
}
