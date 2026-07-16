using UnityEngine;

namespace CenarioMaritimo.Sensor
{
    /// <summary>
    /// Um CONTATO do sensor — o que o USV "enxerga", separado do objeto real
    /// (ground truth). Guarda a última pose/velocidade percebida e quando foi
    /// visto pela última vez. É a base do que a percepção do PRISMA publicaria
    /// como alvo rastreado (tracked_geo_target).
    /// </summary>
    public class Contato
    {
        public int id;
        public Vector3 posicao;
        public Vector3 velocidade;
        public float rumo;
        public float comprimento;
        public float primeiroVisto;
        public float ultimoVisto;

        public bool Novo => Time.time - primeiroVisto < 1.5f;
        public float Idade => Time.time - ultimoVisto;
    }
}
