using System;
using UnityEngine;

namespace MaritimeScenario.Geo
{
    /// <summary>
    /// Scenario georeferencing: converts between geographic coordinates
    /// (latitude/longitude, decimal degrees, WGS84) and the local Unity plane
    /// (meters, X = East, Z = North), from a fictional origin.
    ///
    /// Uses a local tangent-plane (equirectangular) approximation, valid for compact
    /// extents (up to a few kilometers), which suits this project's scenario. Larger
    /// areas would require a projection such as UTM.
    /// </summary>
    [DisallowMultipleComponent]
    public class GeoReferenceOrigin : MonoBehaviour, IGeoReference
    {
        [Header("Origem geográfica fictícia (ponto Unity (0,0,0))")]
        [Tooltip("Latitude do datum, em graus decimais (WGS84). Negativo = Sul.")]
        public double OriginLatitudeDeg = -23.083000;

        [Tooltip("Longitude do datum, em graus decimais (WGS84). Negativo = Oeste.")]
        public double OriginLongitudeDeg = -44.300000;

        public const double EarthRadiusMeters = 6378137.0; // WGS84 equatorial radius

        /// <summary>Most recently enabled instance, for convenient global access.</summary>
        public static GeoReferenceOrigin Instance { get; private set; }

        void Awake() => Instance = this;
        void OnEnable() { if (Instance == null) Instance = this; }

        double MetersPerDegreeLatitude => (Math.PI / 180.0) * EarthRadiusMeters;
        double MetersPerDegreeLongitude => (Math.PI / 180.0) * EarthRadiusMeters * Math.Cos(OriginLatitudeDeg * Math.PI / 180.0);

        /// <summary>Converts lat/lon (degrees) to a local Unity position (X=East, Z=North), in meters.</summary>
        public Vector2 GeographicToLocal(double latDeg, double lonDeg)
        {
            double x = (lonDeg - OriginLongitudeDeg) * MetersPerDegreeLongitude;
            double z = (latDeg - OriginLatitudeDeg) * MetersPerDegreeLatitude;
            return new Vector2((float)x, (float)z);
        }

        /// <summary>Converts a local Unity position (X, Z in meters) to lat/lon (degrees).</summary>
        public (double lat, double lon) LocalToGeographic(float x, float z)
        {
            double lat = OriginLatitudeDeg + z / MetersPerDegreeLatitude;
            double lon = OriginLongitudeDeg + x / MetersPerDegreeLongitude;
            return (lat, lon);
        }

        /// <summary>Converts a local Unity position (uses X and Z) to lat/lon (degrees).</summary>
        public (double lat, double lon) LocalToGeographic(Vector3 localPosition)
            => LocalToGeographic(localPosition.x, localPosition.z);
    }
}
