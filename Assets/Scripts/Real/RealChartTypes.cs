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
        public int Columns;
        public int Rows;
        public float CellSize;
        public double OriginUtmE;
        public double OriginUtmN;
        public int UtmZone;
        public bool UtmSouth;
        public double CentralMeridian;
        public double OriginLat;
        public double OriginLon;
        public float ElevationMin;
        public float ElevationMax;
        public string ChartName;
    }

    /// <summary>
    /// A point object of the chart (rock, buoy, lighthouse, wreck...), in local
    /// coordinates (meters): x = East, z = North.
    /// </summary>
    [Serializable]
    public class ChartPoint
    {
        public string Type;
        public float X;
        public float Z;
        public string ColorHex;
    }

    /// <summary>
    /// Wrapper so JsonUtility can read the array in pontos.json (JsonUtility does not
    /// deserialize a top-level JSON array directly).
    /// </summary>
    [Serializable]
    public class PointList
    {
        public List<ChartPoint> Items = new();
    }
}
