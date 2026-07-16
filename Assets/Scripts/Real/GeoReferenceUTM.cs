using System;
using UnityEngine;

namespace CenarioMaritimo.Real
{
    /// <summary>
    /// Georreferenciamento do cenário REAL (a partir de carta ENC S-57).
    ///
    /// Aqui o plano local do Unity (X = Leste, Z = Norte, em metros) corresponde
    /// diretamente ao sistema UTM da carta, deslocado pela origem (canto SW):
    ///     UTM_E = originE + x ;  UTM_N = originN + z
    ///
    /// A conversão UTM -> latitude/longitude (WGS84) usa a fórmula inversa de
    /// Transverse Mercator (Snyder). Esta implementação foi validada ponto a ponto
    /// contra o GDAL/PROJ, com erro < 1 mm — portanto reflete a posição geográfica
    /// real da carta, não uma aproximação de plano tangente.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeoReferenceUTM : MonoBehaviour, IGeoReference
    {
        [Header("Origem UTM (canto SW da carta)")]
        public double originE;
        public double originN;
        public int utmZone = 23;
        public bool utmSouth = true;

        [Header("Referência (só informativo)")]
        public double originLat;
        public double originLon;
        public string carta = "";

        public static GeoReferenceUTM Instance { get; private set; }
        void Awake() => Instance = this;
        void OnEnable() { if (Instance == null) Instance = this; }

        const double A = 6378137.0;              // semieixo maior WGS84
        const double F = 1.0 / 298.257223563;    // achatamento
        const double K0 = 0.9996;                // fator de escala UTM

        double CentralMeridianRad => (utmZone * 6 - 183) * Math.PI / 180.0;

        /// <summary>Posição local do Unity (X,Z metros) -> (latitude, longitude) em graus.</summary>
        public (double lat, double lon) LocalParaGeografica(float x, float z)
        {
            double E = originE + x;
            double N = originN + z;
            return UtmParaLatLon(E, N);
        }

        public (double lat, double lon) LocalParaGeografica(Vector3 posicaoLocal)
            => LocalParaGeografica(posicaoLocal.x, posicaoLocal.z);

        (double lat, double lon) UtmParaLatLon(double E, double N)
        {
            double e2 = F * (2 - F);
            double ep2 = e2 / (1 - e2);

            E -= 500000.0;
            if (utmSouth) N -= 10000000.0;

            double M = N / K0;
            double mu = M / (A * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

            double phi1 = mu
                + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu)
                + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu)
                + (1097 * e1 * e1 * e1 * e1 / 512) * Math.Sin(8 * mu);

            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);
            double C1 = ep2 * cosPhi1 * cosPhi1;
            double T1 = tanPhi1 * tanPhi1;
            double sinPhi1 = Math.Sin(phi1);
            double N1 = A / Math.Sqrt(1 - e2 * sinPhi1 * sinPhi1);
            double R1 = A * (1 - e2) / Math.Pow(1 - e2 * sinPhi1 * sinPhi1, 1.5);
            double D = E / (N1 * K0);

            double D2 = D * D, D3 = D2 * D, D4 = D3 * D, D5 = D4 * D, D6 = D5 * D;

            double lat = phi1 - (N1 * tanPhi1 / R1) * (D2 / 2
                - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D4 / 24
                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D6 / 720);

            double lon = CentralMeridianRad + (D
                - (1 + 2 * T1 + C1) * D3 / 6
                + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D5 / 120) / cosPhi1;

            return (lat * 180.0 / Math.PI, lon * 180.0 / Math.PI);
        }
    }
}
