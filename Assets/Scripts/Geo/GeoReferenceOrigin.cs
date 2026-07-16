using System;
using UnityEngine;

namespace CenarioMaritimo.Geo
{
    /// <summary>
    /// Georreferenciamento do cenário: converte entre coordenadas geográficas
    /// (latitude/longitude, graus decimais, WGS84) e o plano local do Unity
    /// (metros, X = Leste, Z = Norte), a partir de uma origem fictícia.
    ///
    /// Usa aproximação de plano tangente local (equirretangular), válida para
    /// extensões compactas (até poucos quilômetros) — adequada ao cenário deste
    /// projeto. Para áreas maiores seria necessária uma projeção como UTM.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeoReferenceOrigin : MonoBehaviour, IGeoReference
    {
        [Header("Origem geográfica fictícia (ponto Unity (0,0,0))")]
        [Tooltip("Latitude do datum, em graus decimais (WGS84). Negativo = Sul.")]
        public double latitudeOrigemGraus = -23.083000;

        [Tooltip("Longitude do datum, em graus decimais (WGS84). Negativo = Oeste.")]
        public double longitudeOrigemGraus = -44.300000;

        public const double RaioTerraMetros = 6378137.0; // raio equatorial WGS84

        public static GeoReferenceOrigin Instance { get; private set; }

        void Awake() => Instance = this;
        void OnEnable() { if (Instance == null) Instance = this; }

        double MetrosPorGrauLatitude => (Math.PI / 180.0) * RaioTerraMetros;
        double MetrosPorGrauLongitude => (Math.PI / 180.0) * RaioTerraMetros * Math.Cos(latitudeOrigemGraus * Math.PI / 180.0);

        /// <summary>Converte lat/lon (graus) para posição local do Unity (X=Leste, Z=Norte), em metros.</summary>
        public Vector2 GeograficaParaLocal(double latGraus, double lonGraus)
        {
            double x = (lonGraus - longitudeOrigemGraus) * MetrosPorGrauLongitude;
            double z = (latGraus - latitudeOrigemGraus) * MetrosPorGrauLatitude;
            return new Vector2((float)x, (float)z);
        }

        /// <summary>Converte posição local do Unity (X,Z em metros) para lat/lon (graus).</summary>
        public (double lat, double lon) LocalParaGeografica(float x, float z)
        {
            double lat = latitudeOrigemGraus + z / MetrosPorGrauLatitude;
            double lon = longitudeOrigemGraus + x / MetrosPorGrauLongitude;
            return (lat, lon);
        }

        public (double lat, double lon) LocalParaGeografica(Vector3 posicaoLocal)
            => LocalParaGeografica(posicaoLocal.x, posicaoLocal.z);
    }
}
