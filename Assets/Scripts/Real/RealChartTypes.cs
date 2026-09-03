using System;
using System.Collections.Generic;

namespace MaritimeScenario.Real
{
    /// <summary>
    /// Metadata of the extracted chart (mirrors metadata.json). Field names are
    /// identical to the JSON for direct deserialization via JsonUtility.
    /// </summary>
    [Serializable]
    public class ChartMetadata
    {
        /// <summary>Number of columns in the height grid.</summary>
        public int Columns;
        /// <summary>Number of rows in the height grid.</summary>
        public int Rows;
        /// <summary>Ground distance between two neighbouring grid cells, in meters.</summary>
        public float CellSize;
        /// <summary>UTM easting of the grid origin, in meters.</summary>
        public double OriginUtmE;
        /// <summary>UTM northing of the grid origin, in meters.</summary>
        public double OriginUtmN;
        /// <summary>UTM zone number of the chart.</summary>
        public int UtmZone;
        /// <summary>Whether the chart lies in the southern hemisphere.</summary>
        public bool UtmSouth;
        /// <summary>Central meridian of the UTM zone, in degrees.</summary>
        public double CentralMeridian;
        /// <summary>Latitude of the grid origin, in decimal degrees.</summary>
        public double OriginLat;
        /// <summary>Longitude of the grid origin, in decimal degrees.</summary>
        public double OriginLon;
        /// <summary>Lowest elevation in the grid, in meters. Negative values are depths.</summary>
        public float ElevationMin;
        /// <summary>Highest elevation in the grid, in meters.</summary>
        public float ElevationMax;
        /// <summary>Name of the nautical chart the grid was extracted from.</summary>
        public string ChartName;
    }

    /// <summary>
    /// A point object of the chart (rock, buoy, lighthouse, wreck...), in local
    /// coordinates (meters): x = East, z = North.
    /// </summary>
    [Serializable]
    public class ChartPoint
    {
        /// <summary>Feature type, using the S-57 object class code.</summary>
        public string Type;
        /// <summary>Position along the East axis of the scene, in meters.</summary>
        public float X;
        /// <summary>Position along the North axis of the scene, in meters.</summary>
        public float Z;
        /// <summary>Colour used to draw the feature, as an HTML hex string.</summary>
        public string ColorHex;
    }

    /// <summary>
    /// Wrapper so JsonUtility can read the array in pontos.json (JsonUtility does not
    /// deserialize a top-level JSON array directly).
    /// </summary>
    [Serializable]
    public class PointList
    {
        /// <summary>The point features read from the chart.</summary>
        public List<ChartPoint> Items = new();
    }
}
