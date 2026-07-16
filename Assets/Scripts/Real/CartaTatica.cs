using UnityEngine;
using UnityEngine.InputSystem;
using CenarioMaritimo.Boat;
using CenarioMaritimo.Sensor;

namespace CenarioMaritimo.Real
{
    /// <summary>
    /// Visão tática 2D ao vivo. Dois modos (tecla M):
    ///   • Seguir  — câmera top-down segue o barco de perto (entorno imediato);
    ///   • Visão geral — mostra a BAÍA INTEIRA como uma CARTA chapada (imagem
    ///     colorida por profundidade: água azul, terra verde) com marcadores de
    ///     todas as embarcações + o barco. A imagem da carta (cartaMapa) é gerada
    ///     pelo builder, então tem alto contraste e não depende da iluminação/água 3D.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CartaTatica : MonoBehaviour
    {
        public Transform alvo;
        public float alcance = 180f;   // meia-altura da vista ao seguir (m)
        public float altitude = 600f;

        [Header("Carta (visão geral)")]
        public Texture2D cartaMapa;    // imagem colorida por profundidade (do builder)
        public Vector2 mundoTam = new Vector2(20000f, 15000f); // dimensão do mundo coberto (m)

        Camera cam;
        bool visaoGeral;
        const int LAYER_AGUA = 4; // "Water"

        EmbarcacaoDinamica[] frota = new EmbarcacaoDinamica[0];
        SensorEmbarcacoes sensor;
        float proxRefresh;

        readonly Rect vpSeguir = new Rect(0.70f, 0.70f, 0.29f, 0.29f);
        readonly Rect vpGeral = new Rect(0.28f, 0.14f, 0.70f, 0.82f);

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = altitude + 400f;
            cam.depth = 10;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.08f, 0.16f);
            cam.rect = vpSeguir;
            transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // Norte para cima

            // câmera tática não renderiza a água (só o terreno)
            var agua = GameObject.Find("Agua");
            if (agua != null) agua.layer = LAYER_AGUA;
            cam.cullingMask = ~(1 << LAYER_AGUA);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) visaoGeral = !visaoGeral;

            if (Time.time >= proxRefresh)
            {
                frota = Object.FindObjectsByType<EmbarcacaoDinamica>();
                if (sensor == null && alvo != null) sensor = alvo.GetComponent<SensorEmbarcacoes>();
                proxRefresh = Time.time + 1f;
            }
        }

        void LateUpdate()
        {
            // Na visão geral com a imagem de carta, a câmera 3D não precisa renderizar
            // (o painel é desenhado em OnGUI). Sem a imagem, cai para a câmera 3D.
            bool usaImagem = visaoGeral && cartaMapa != null;
            cam.enabled = !usaImagem;

            if (!visaoGeral && alvo != null)
            {
                cam.rect = vpSeguir;
                cam.orthographicSize = alcance;
                transform.position = new Vector3(alvo.position.x, altitude, alvo.position.z);
            }
        }

        // ---------------- painel (OnGUI) ----------------

        static GUIStyle _stTexto, _stTri;
        static Texture2D _branco;

        void OnGUI()
        {
            if (_stTexto == null)
            {
                _stTexto = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold };
                _stTexto.normal.textColor = Color.white;
                _stTri = new GUIStyle { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                _branco = Texture2D.whiteTexture;
            }

            Rect vp = visaoGeral ? vpGeral : vpSeguir;
            Rect painel = PainelPixels(vp);

            GUI.Label(new Rect(painel.x + 6, painel.y + 2, 340, 20),
                visaoGeral ? "CARTA TÁTICA — VISÃO GERAL   (M: seguir barco)"
                           : "CARTA TÁTICA  (N ↑)   (M: abrir mapa)", _stTexto);

            if (visaoGeral && cartaMapa != null)
                DesenharVisaoGeral(painel);
            else
                DesenharMarcadoresProjetados(); // segue (câmera 3D já renderizou o terreno)
        }

        // ---- visão geral: imagem de carta + marcadores mapeados linearmente ----
        void DesenharVisaoGeral(Rect painel)
        {
            // fundo escuro (bordas do letterbox)
            Cor(new Color(0.04f, 0.08f, 0.16f));
            GUI.DrawTexture(painel, _branco);
            Cor(Color.white);

            // imagem da carta, preservando o aspecto (ScaleToFit calcula o rect interno)
            Rect img = AjustarAspecto(painel, (float)cartaMapa.width / cartaMapa.height);
            GUI.DrawTexture(img, cartaMapa);

            // alcance do sensor (círculo em torno do USV)
            if (sensor != null && alvo != null && MapearLinear(img, alvo.position, out Vector2 pc))
            {
                float raioPx = sensor.Alcance / mundoTam.x * img.width;
                Anel(pc, raioPx, new Color(0.2f, 0.9f, 1f, 0.5f));
            }

            // ground truth: quadrados coloridos por porte
            foreach (var v in frota)
                if (v != null) QuadradoLinear(img, v.transform.position, CorPorPorte(v.comprimento), 7f);

            // contatos do sensor: anéis ciano (o que o USV "enxerga")
            if (sensor != null)
                foreach (var c in sensor.Contatos)
                    if (MapearLinear(img, c.posicao, out Vector2 pcont))
                        Anel(pcont, 8f, c.Novo ? new Color(1f, 1f, 0.3f, 1f) : new Color(0.2f, 0.95f, 1f, 1f));

            if (alvo != null) TrianguloLinear(img, alvo.position, alvo.eulerAngles.y);
        }

        void QuadradoLinear(Rect img, Vector3 mundo, Color cor, float tam)
        {
            if (!MapearLinear(img, mundo, out Vector2 p)) return;
            Cor(cor);
            GUI.DrawTexture(new Rect(p.x - tam * 0.5f, p.y - tam * 0.5f, tam, tam), _branco);
            Cor(Color.white);
        }

        void TrianguloLinear(Rect img, Vector3 mundo, float rumo)
        {
            if (!MapearLinear(img, mundo, out Vector2 p)) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(rumo, p);
            _stTri.normal.textColor = new Color(1f, 0.25f, 0.15f);
            GUI.Label(new Rect(p.x - 15, p.y - 18, 30, 36), "▲", _stTri);
            GUI.matrix = m;
        }

        // mundo (x,z) -> pixel dentro do rect da imagem (Norte em cima, Leste à direita)
        bool MapearLinear(Rect img, Vector3 mundo, out Vector2 tela)
        {
            tela = default;
            float u = mundo.x / mundoTam.x;
            float w = mundo.z / mundoTam.y;
            if (u < 0f || u > 1f || w < 0f || w > 1f) return false;
            tela = new Vector2(img.x + u * img.width, img.y + (1f - w) * img.height);
            return true;
        }

        // ---- modo seguir: marcadores projetados pela câmera 3D ----
        void DesenharMarcadoresProjetados()
        {
            if (cam == null) return;
            foreach (var v in frota)
                if (v != null) QuadradoProjetado(v.transform.position, CorPorPorte(v.comprimento), 6f);
            if (sensor != null)
                foreach (var c in sensor.Contatos)
                    if (Projetar(c.posicao, out Vector2 p))
                        Anel(p, 9f, c.Novo ? new Color(1f, 1f, 0.3f, 1f) : new Color(0.2f, 0.95f, 1f, 1f));
            if (alvo != null) TrianguloProjetado(alvo.position, alvo.eulerAngles.y);
        }

        bool Projetar(Vector3 mundo, out Vector2 tela)
        {
            tela = default;
            Vector3 v = cam.WorldToViewportPoint(mundo);
            if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            tela = new Vector2((cam.rect.x + v.x * cam.rect.width) * Screen.width,
                               Screen.height - (cam.rect.y + v.y * cam.rect.height) * Screen.height);
            return true;
        }

        void QuadradoProjetado(Vector3 mundo, Color cor, float tam)
        {
            if (!Projetar(mundo, out Vector2 p)) return;
            Cor(cor);
            GUI.DrawTexture(new Rect(p.x - tam * 0.5f, p.y - tam * 0.5f, tam, tam), _branco);
            Cor(Color.white);
        }

        void TrianguloProjetado(Vector3 mundo, float rumo)
        {
            if (!Projetar(mundo, out Vector2 p)) return;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(rumo, p);
            _stTri.normal.textColor = new Color(1f, 0.25f, 0.15f);
            GUI.Label(new Rect(p.x - 15, p.y - 18, 30, 36), "▲", _stTri);
            GUI.matrix = m;
        }

        // ---------------- utilidades ----------------

        static Rect PainelPixels(Rect vp) => new Rect(
            vp.x * Screen.width,
            (1f - vp.y - vp.height) * Screen.height,
            vp.width * Screen.width,
            vp.height * Screen.height);

        static Rect AjustarAspecto(Rect painel, float imgAspect)
        {
            float painelAspect = painel.width / painel.height;
            if (painelAspect > imgAspect)
            {
                float w = painel.height * imgAspect;
                return new Rect(painel.x + (painel.width - w) * 0.5f, painel.y, w, painel.height);
            }
            else
            {
                float h = painel.width / imgAspect;
                return new Rect(painel.x, painel.y + (painel.height - h) * 0.5f, painel.width, h);
            }
        }

        static void Cor(Color c) => GUI.color = c;

        static Texture2D _anelTex;
        static Texture2D AnelTex()
        {
            if (_anelTex != null) return _anelTex;
            const int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            float cx = s * 0.5f, cy = s * 0.5f, rOut = 62f, rIn = 55f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = (d <= rOut && d >= rIn) ? 1f : 0f;
                    px[x + y * s] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            _anelTex = t;
            return t;
        }

        static void Anel(Vector2 centro, float raioPx, Color cor)
        {
            Cor(cor);
            GUI.DrawTexture(new Rect(centro.x - raioPx, centro.y - raioPx, raioPx * 2f, raioPx * 2f), AnelTex());
            Cor(Color.white);
        }

        static Color CorPorPorte(float comprimento)
        {
            if (comprimento > 80f) return new Color(1f, 0.30f, 0.15f);   // cargueiro/tanque
            if (comprimento > 25f) return new Color(1f, 0.65f, 0.10f);   // média
            return new Color(1f, 0.95f, 0.35f);                          // lancha
        }
    }
}
