using UnityEngine;

public struct ImuData
{
    /// <summary>Linear acceleration, in meters per second squared.</summary>
    public Vector3 LinearAcceleration;
    /// <summary>Angular velocity, in radians per second.</summary>
    public Vector3 AngularVelocity;
    /// <summary>Orientation of the sensor.</summary>
    public Quaternion Orientation;

    // Matrizes 3x3. Ordem (X, Y, Z)
    /// <summary>Covariance of the linear acceleration, as a row-major 3x3 matrix.</summary>
    public double[] LinearAccelerationCovariance;
    /// <summary>Covariance of the angular velocity, as a row-major 3x3 matrix.</summary>
    public double[] AngularVelocityCovariance;
    /// <summary>Covariance of the orientation, as a row-major 3x3 matrix.</summary>
    public double[] OrientationCovariance;
}