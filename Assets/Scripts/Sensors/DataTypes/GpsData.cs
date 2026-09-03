using UnityEngine;

public struct GpsData
{
    /// <summary>Latitude, in decimal degrees.</summary>
    public double Latitude;
    /// <summary>Longitude, in decimal degrees.</summary>
    public double Longitude;
    /// <summary>Altitude above the datum, in meters.</summary>
    public double Altitude;
    
    /// <summary>Velocity in the world frame, in meters per second.</summary>
    public Vector3 GlobalVelocity; 
    
    // Matriz 3x3. Ordem: East, North, Up (E, N, U)
    /// <summary>Covariance of the position estimate, as the row-major 3x3 matrix ROS expects.</summary>
    public double[] PositionCovariance; 
}