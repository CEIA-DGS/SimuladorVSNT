using UnityEngine;
using UnityEngine.InputSystem;
using CenarioMaritimo.Water;

namespace CenarioMaritimo.Boat
{
    /// <summary>
    /// Controle simples da embarcação para testar o cenário durante o Play:
    /// W/A/S/D ou setas movem e giram o USV. A flutuação amostra a mesma onda
    /// da água (OndaUtil) em 4 pontos do casco (proa/popa/bombordo/boreste) para
    /// gerar heave (sobe/desce) e inclinação (arfagem/balanço) condizentes com o
    /// que se vê — não é um modelo hidrodinâmico de verdade, só uma aproximação
    /// visual pra "sentir" a escala do cenário. Também mostra, em tempo real, a
    /// posição local (metros) e a posição geográfica (lat/lon), como demonstração
    /// ao vivo do georreferenciamento.
    /// </summary>
    public class BoatController : MonoBehaviour
    {
        public float velocidade = 10f;
        public float velocidadeGiro = 70f;
        public float alturaFlutuacao = 0.5f;
        public bool mostrarHUD = true;

        [Header("Limite de terra")]
        [Tooltip("Acima desta altura (Y do terreno) é considerado terra firme — o barco não entra.")]
        public float alturaLimiteTerra = 0.3f;
        [Tooltip("Colisor do terreno usado para checar a altura embaixo do barco. Se vazio, o limite de terra fica desativado.")]
        public Collider colisorTerreno;

        [Header("Casco (para a flutuação)")]
        public float comprimento = 4.5f;
        public float largura = 2.2f;

        [Header("Onda (deve bater com a Água)")]
        public float amplitudeOnda = 0.15f;
        public float escalaOnda = 0.05f;
        public float velocidadeOnda = 0.6f;

        IGeoReference geo;

        void Start()
        {
            // Aceita qualquer georreferenciamento (fictício = plano tangente,
            // real = UTM) através da interface comum IGeoReference.
            foreach (var mb in FindObjectsByType<MonoBehaviour>())
                if (mb is IGeoReference g) { geo = g; break; }
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado != null)
            {
                float avanco = 0f;
                if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) avanco += 1f;
                if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) avanco -= 1f;

                float giro = 0f;
                if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) giro += 1f;
                if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) giro -= 1f;

                Vector3 posAnterior = transform.position;
                transform.Rotate(Vector3.up, giro * velocidadeGiro * Time.deltaTime);
                transform.position += transform.forward * avanco * velocidade * Time.deltaTime;

                if (SobreTerra(transform.position))
                    transform.position = posAnterior; // barra o barco de subir na ilha
            }

            AplicarFlutuacao();
        }

        bool SobreTerra(Vector3 pos)
        {
            if (colisorTerreno == null) return false;

            // Testa só contra o colisor do terreno (não "qualquer coisa na cena"),
            // pra nunca correr o risco de bater no próprio barco ou em outro objeto.
            var raio = new Ray(pos + Vector3.up * 300f, Vector3.down);
            if (colisorTerreno.Raycast(raio, out var hit, 1000f))
                return hit.point.y > alturaLimiteTerra;
            return false;
        }

        void AplicarFlutuacao()
        {
            Vector3 p = transform.position;
            Vector3 frente = transform.forward * (comprimento * 0.5f);
            Vector3 lado = transform.right * (largura * 0.5f);

            float hProa = OndaUtil.Altura(p.x + frente.x, p.z + frente.z, Time.time, amplitudeOnda, escalaOnda, velocidadeOnda);
            float hPopa = OndaUtil.Altura(p.x - frente.x, p.z - frente.z, Time.time, amplitudeOnda, escalaOnda, velocidadeOnda);
            float hBoreste = OndaUtil.Altura(p.x + lado.x, p.z + lado.z, Time.time, amplitudeOnda, escalaOnda, velocidadeOnda);
            float hBombordo = OndaUtil.Altura(p.x - lado.x, p.z - lado.z, Time.time, amplitudeOnda, escalaOnda, velocidadeOnda);

            float alturaMedia = (hProa + hPopa + hBoreste + hBombordo) * 0.25f;
            p.y = Mathf.Lerp(p.y, alturaFlutuacao + alturaMedia, Time.deltaTime * 6f);
            transform.position = p;

            float pitch = Mathf.Atan2(hPopa - hProa, comprimento) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(hBombordo - hBoreste, largura) * Mathf.Rad2Deg;
            Quaternion rotAlvo = Quaternion.Euler(pitch, transform.eulerAngles.y, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * 4f);
        }

        void OnGUI()
        {
            if (!mostrarHUD) return;

            var pos = transform.position;
            string texto = $"Local (X, Z): {pos.x:F1} m, {pos.z:F1} m";
            if (geo != null)
            {
                var (lat, lon) = geo.LocalParaGeografica(pos.x, pos.z);
                texto += $"\nGeográfica (lat, lon): {lat:F6}, {lon:F6}";
            }
            texto += "\nWASD / setas para mover";

            GUI.Label(new Rect(10, 10, 460, 70), texto, EstiloHUD());
        }

        static GUIStyle _estilo;
        static GUIStyle EstiloHUD()
        {
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _estilo.normal.textColor = Color.white;
            }
            return _estilo;
        }
    }
}
