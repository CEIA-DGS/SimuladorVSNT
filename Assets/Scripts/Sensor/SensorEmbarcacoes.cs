using System.Collections.Generic;
using UnityEngine;
using CenarioMaritimo.Boat;

namespace CenarioMaritimo.Sensor
{
    /// <summary>
    /// Sensor de objetos dinâmicos a bordo do USV (camada 2 — percepção). NÃO cria
    /// embarcações: DETECTA as que já existem (ground truth) dentro de um alcance e
    /// mantém uma lista de CONTATOS (cria/atualiza/remove), como um radar/AIS real.
    ///
    /// • Varredura periódica (não todo frame).
    /// • Oclusão opcional: se há terra (ilha) na linha de visada, não detecta.
    /// • Tracking por dicionário: contato existente é atualizado; novo é criado;
    ///   contato não visto há 'tempoEsquecimento' é removido.
    ///
    /// Nesta fase a chave do contato é o InstanceID da embarcação (simples). Um radar
    /// real associaria por proximidade à última posição — troca futura, se quiser.
    /// </summary>
    public class SensorEmbarcacoes : MonoBehaviour
    {
        [Header("Sensor")]
        public float alcance = 3500f;             // m
        public float intervaloVarredura = 0.25f;  // s (4x/s)
        public float tempoEsquecimento = 3f;      // s sem ver -> remove
        public bool usarOclusao = true;           // terra bloqueia a detecção

        // chaveado pela própria embarcação (evita GetInstanceID, obsoleto no Unity 6.5)
        readonly Dictionary<EmbarcacaoDinamica, Contato> contatos = new();
        readonly List<EmbarcacaoDinamica> _remover = new();
        float proxVarredura;

        public IReadOnlyCollection<Contato> Contatos => contatos.Values;
        public float Alcance => alcance;

        void Update()
        {
            if (Time.time < proxVarredura) return;
            proxVarredura = Time.time + intervaloVarredura;
            Varrer();
        }

        void Varrer()
        {
            Vector3 origem = transform.position;
            var frota = Object.FindObjectsByType<EmbarcacaoDinamica>();

            foreach (var v in frota)
            {
                Vector3 pos = v.transform.position;
                if (Vector3.Distance(origem, pos) > alcance) continue;
                if (usarOclusao && Ocluido(origem, pos)) continue;

                if (!contatos.TryGetValue(v, out var c))
                {
                    c = new Contato { id = v.GetHashCode(), primeiroVisto = Time.time };
                    contatos[v] = c;
                }
                c.posicao = pos;
                c.velocidade = v.VelocidadeAtual;
                c.rumo = v.RumoGraus;
                c.comprimento = v.comprimento;
                c.ultimoVisto = Time.time;
            }

            // remove contatos "perdidos" (não vistos há um tempo) ou destruídos
            _remover.Clear();
            foreach (var kv in contatos)
                if (kv.Key == null || Time.time - kv.Value.ultimoVisto > tempoEsquecimento) _remover.Add(kv.Key);
            foreach (var v in _remover) contatos.Remove(v);
        }

        // linha de visada ligeiramente acima da água; se bater no terreno (ilha),
        // o alvo está oculto. O único colisor na cena é o terreno, então basta o Linecast.
        bool Ocluido(Vector3 a, Vector3 b)
        {
            var a1 = new Vector3(a.x, 1.5f, a.z);
            var b1 = new Vector3(b.x, 1.5f, b.z);
            return Physics.Linecast(a1, b1);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, alcance);
        }
    }
}
