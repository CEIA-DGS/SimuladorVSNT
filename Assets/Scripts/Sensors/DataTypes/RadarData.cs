public struct RadarData
{
    /// <summary>Angle of the first sample, in radians.</summary>
    public float AngleMin;
    /// <summary>Angle of the last sample, in radians.</summary>
    public float AngleMax;
    /// <summary>Angle between two consecutive samples, in radians.</summary>
    public float AngleIncrement;
    /// <summary>Time between two consecutive samples, in seconds.</summary>
    public float TimeIncrement;
    /// <summary>Time taken by one full scan, in seconds.</summary>
    public float ScanTime;
    /// <summary>Shortest distance the scan reports, in meters.</summary>
    public float RangeMin;
    /// <summary>Longest distance the scan reports, in meters.</summary>
    public float RangeMax;
    
    /// <summary>Measured distances, one per sample, in meters.</summary>
    public float[] Ranges;
}