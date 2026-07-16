using UnityEngine;

namespace CenarioMaritimo.Boat
{
    /// <summary>Estilo visual do casco (define a superestrutura procedural).</summary>
    public enum EstiloCasco { Cargueiro, Media, Lancha }

    /// <summary>
    /// Tipo de embarcação (data-driven). Inspirado nos códigos de tipo do padrão
    /// AIS para dar plausibilidade de porte/velocidade — mas nesta fase é apenas
    /// um rótulo para geração sintética, NÃO um dado AIS real. Cada asset define
    /// a faixa de comprimento, a razão de boca, a faixa de velocidade e a cor,
    /// usados pela fábrica procedural de embarcações.
    ///
    /// Referência AIS: 70–79 carga, 80–89 tanque, 60–69 passageiros, 52 rebocador,
    /// 37 lazer, 30 pesca. (No AIS real, comprimento = A+B e boca = C+D.)
    /// </summary>
    [CreateAssetMenu(menuName = "Simulador/Tipo de Embarcação", fileName = "VesselType")]
    public class VesselType : ScriptableObject
    {
        public string nomeExibicao = "Embarcação";

        [Tooltip("Código de tipo do padrão AIS (rótulo de plausibilidade; não é dado AIS real nesta fase).")]
        public int codigoAIS = 70;

        [Tooltip("Faixa de comprimento plausível (min, max) em metros.")]
        public Vector2 comprimentoM = new Vector2(120f, 200f);

        [Range(0.08f, 0.35f), Tooltip("Boca = comprimento × razão.")]
        public float razaoBoca = 0.16f;

        [Tooltip("Faixa de velocidade típica (min, max) em nós.")]
        public Vector2 velocidadeKn = new Vector2(8f, 14f);

        public Color cor = new Color(0.35f, 0.30f, 0.28f);
        public EstiloCasco estilo = EstiloCasco.Cargueiro;

        public float SortearComprimentoM() => Random.Range(comprimentoM.x, comprimentoM.y);
        public float SortearVelocidadeMS() => Random.Range(velocidadeKn.x, velocidadeKn.y) * 0.514444f; // nós -> m/s
    }
}
