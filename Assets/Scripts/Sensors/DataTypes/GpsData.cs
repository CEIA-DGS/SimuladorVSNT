using UnityEngine;

public struct GpsData
{
    public double Latitude;
    public double Longitude;
    public double Altitude;
    
    public Vector3 GlobalVelocity; 
    
    // Matriz 3x3. Ordem: East, North, Up (E, N, U)
    public double[] PositionCovariance; 
}