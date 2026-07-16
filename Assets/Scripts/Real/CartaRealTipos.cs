using System;
using System.Collections.Generic;

namespace CenarioMaritimo.Real
{
    /// <summary>Metadados da carta extraída (espelha metadata.json). Nomes de campo
    /// idênticos ao JSON para desserialização direta via JsonUtility.</summary>
    [Serializable]
    public class CartaMetadata
    {
        public int ncols;
        public int nrows;
        public float cell;
        public double originUTM_E;
        public double originUTM_N;
        public int utmZone;
        public bool utmSouth;
        public double centralMeridian;
        public double originLat;
        public double originLon;
        public float elevMin;
        public float elevMax;
        public string chart;
    }

    /// <summary>Um objeto pontual da carta (rochedo, boia, farol, naufrágio...),
    /// em coordenadas locais (metros): x = Leste, z = Norte.</summary>
    [Serializable]
    public class PontoCarta
    {
        public string tipo;
        public float x;
        public float z;
        public string cor;
    }

    /// <summary>Wrapper para o JsonUtility conseguir ler o array de pontos.json
    /// (o JsonUtility não desserializa um array no topo do JSON diretamente).</summary>
    [Serializable]
    public class ListaPontos
    {
        public List<PontoCarta> itens = new();
    }
}
