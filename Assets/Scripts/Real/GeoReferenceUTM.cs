using System;
using UnityEngine;

namespace MaritimeScenario.Real
{
    /// <summary>
    /// Georeferencing of the REAL scenario (built from an ENC S-57 chart).
    ///
    /// Here the local Unity plane (X = East, Z = North, in meters) maps directly to the
    /// chart's UTM system, offset by the origin (SW corner):
    ///     UTM_E = originE + x ;  UTM_N = originN + z
    ///
    /// The UTM &lt;-&gt; latitude/longitude (WGS84) conversion uses the Transverse
    /// Mercator formulas (Snyder), both inverse and forward. The inverse direction was
    /// validated point by point against GDAL/PROJ, with error &lt; 1 mm — so it reflects
    /// the real geographic position of the chart, not a tangent-plane approximation.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeoReferenceUTM : MonoBehaviour, IGeoReference
    {
        [Header("Origem UTM (canto SW da carta)")]
        /// <summary>UTM easting of the scene origin, in meters.</summary>
        public double OriginE;
        /// <summary>UTM northing of the scene origin, in meters.</summary>
        public double OriginN;
        /// <summary>UTM zone number of the chart.</summary>
        public int UtmZone = 23;
        /// <summary>Whether the chart lies in the southern hemisphere.</summary>
        public bool UtmSouth = true;

        [Header("Referência (só informativo)")]
        /// <summary>Latitude of the scene origin, in decimal degrees.</summary>
        public double OriginLat;
        /// <summary>Longitude of the scene origin, in decimal degrees.</summary>
        public double OriginLon;
        /// <summary>Name of the nautical chart this reference was taken from.</summary>
        public string ChartName = "";

        /// <summary>Most recently enabled instance, for convenient global access.</summary>
        public static GeoReferenceUTM Instance { get; private set; }

        void Awake() => Instance = this;
        void OnEnable() { if (Instance == null) Instance = this; }

        const double A = 6378137.0;              // WGS84 semi-major axis
        const double F = 1.0 / 298.257223563;    // flattening
        const double K0 = 0.9996;                // UTM scale factor

        double CentralMeridianRad => (UtmZone * 6 - 183) * Math.PI / 180.0;

        /// <summary>Converts a local Unity position (X, Z in meters) to (latitude, longitude) in degrees.</summary>
        public (double lat, double lon) LocalToGeographic(float x, float z)
        {
            double e = OriginE + x;
            double n = OriginN + z;
            return UtmToLatLon(e, n);
        }

        /// <summary>Converts a local Unity position (uses X and Z) to (latitude, longitude) in degrees.</summary>
        public (double lat, double lon) LocalToGeographic(Vector3 localPosition)
            => LocalToGeographic(localPosition.x, localPosition.z);

        /// <summary>Converts (latitude, longitude) in degrees to a local Unity position (X, Z in meters).</summary>
        public Vector2 GeographicToLocal(double lat, double lon)
        {
            var (e, n) = LatLonToUtm(lat, lon);
            double x = e - OriginE;
            double z = n - OriginN;
            return new Vector2((float)x, (float)z);
        }

        /// <summary>
        /// Inverse Transverse Mercator (Snyder): converts absolute UTM easting/northing
        /// to latitude/longitude in radians-derived degrees. Variable names follow the
        /// formula's notation on purpose, to keep it verifiable against the reference.
        /// </summary>
        /// <param name="e">UTM easting (meters, including the 500 km false easting).</param>
        /// <param name="n">UTM northing (meters, including the southern false northing).</param>
        /// <returns>Latitude and longitude, in degrees.</returns>
        (double lat, double lon) UtmToLatLon(double e, double n)
        {
            double e2 = F * (2 - F);
            double ep2 = e2 / (1 - e2);

            e -= 500000.0;
            if (UtmSouth) n -= 10000000.0;

            double m = n / K0;
            double mu = m / (A * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));

            double phi1 = mu
                + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu)
                + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu)
                + (1097 * e1 * e1 * e1 * e1 / 512) * Math.Sin(8 * mu);

            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);
            double c1 = ep2 * cosPhi1 * cosPhi1;
            double t1 = tanPhi1 * tanPhi1;
            double sinPhi1 = Math.Sin(phi1);
            double n1 = A / Math.Sqrt(1 - e2 * sinPhi1 * sinPhi1);
            double r1 = A * (1 - e2) / Math.Pow(1 - e2 * sinPhi1 * sinPhi1, 1.5);
            double d = e / (n1 * K0);

            double d2 = d * d, d3 = d2 * d, d4 = d3 * d, d5 = d4 * d, d6 = d5 * d;

            double lat = phi1 - (n1 * tanPhi1 / r1) * (d2 / 2
                - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ep2) * d4 / 24
                + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ep2 - 3 * c1 * c1) * d6 / 720);

            double lon = CentralMeridianRad + (d
                - (1 + 2 * t1 + c1) * d3 / 6
                + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * ep2 + 24 * t1 * t1) * d5 / 120) / cosPhi1;

            return (lat * 180.0 / Math.PI, lon * 180.0 / Math.PI);
        }

        /// <summary>
        /// Forward Transverse Mercator (Snyder): converts latitude/longitude (degrees) to
        /// absolute UTM easting/northing (meters). Variable names follow the formula's
        /// notation on purpose, to keep it verifiable against the reference.
        /// </summary>
        /// <param name="lat">Latitude, in degrees.</param>
        /// <param name="lon">Longitude, in degrees.</param>
        /// <returns>UTM easting and northing, in meters.</returns>
        (double e, double n) LatLonToUtm(double lat, double lon)
        {
            double latRad = lat * Math.PI / 180.0;
            double lonRad = lon * Math.PI / 180.0;

            double e2 = F * (2 - F);
            double ep2 = e2 / (1 - e2);

            double sinLat = Math.Sin(latRad);
            double cosLat = Math.Cos(latRad);
            double tanLat = Math.Tan(latRad);

            double nRad = A / Math.Sqrt(1 - e2 * sinLat * sinLat);
            double t = tanLat * tanLat;
            double c = ep2 * cosLat * cosLat;
            double aVar = (lonRad - CentralMeridianRad) * cosLat;

            double m = A * (
                (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * latRad
                - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * latRad)
                + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * latRad)
                - (35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * latRad)
            );

            double a2 = aVar * aVar;
            double a3 = a2 * aVar;
            double a4 = a3 * aVar;
            double a5 = a4 * aVar;
            double a6 = a5 * aVar;

            double e = 500000.0 + (K0 * nRad * (aVar
                + (1 - t + c) * a3 / 6
                + (5 - 18 * t + t * t + 72 * c - 58 * ep2) * a5 / 120));

            double n = K0 * (m + nRad * tanLat * (a2 / 2
                + (5 - t + 9 * c + 4 * c * c) * a4 / 24
                + (61 - 58 * t + t * t + 600 * c - 330 * ep2) * a6 / 720));

            if (UtmSouth) n += 10000000.0; // false northing for the southern hemisphere

            return (e, n);
        }
    }
}
