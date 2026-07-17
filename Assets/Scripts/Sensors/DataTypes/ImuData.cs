using UnityEngine;

public struct ImuData
{
    public Vector3 LinearAcceleration;
    public Vector3 AngularVelocity;
    public Quaternion Orientation;

    // Matrizes 3x3. Ordem (X, Y, Z)
    public double[] LinearAccelerationCovariance;
    public double[] AngularVelocityCovariance;
    public double[] OrientationCovariance;
}