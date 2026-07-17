using UnityEngine;

public class GaussianNoiseImu : BaseNoiseModel<ImuData>
{
    [Header("IMU Noise (Desvio Padrão)")]
    [Tooltip("Ruído da Aceleração Linear (m/s²)")]
    public float accelStdDev = 0.05f;
    
    [Tooltip("Ruído da Velocidade Angular (rad/s)")]
    public float gyroStdDev = 0.005f;
    
    [Tooltip("Ruído da Orientação (radianos)")]
    public float orientationStdDev = 0.01f;

    public override ImuData ApplyNoise(ImuData data)
    {
        ImuData noisyData = data;

        // Aplica ruído na Aceleração e Giroscópio
        noisyData.LinearAcceleration.x += GenerateGaussian(0, accelStdDev);
        noisyData.LinearAcceleration.y += GenerateGaussian(0, accelStdDev);
        noisyData.LinearAcceleration.z += GenerateGaussian(0, accelStdDev);

        noisyData.AngularVelocity.x += GenerateGaussian(0, gyroStdDev);
        noisyData.AngularVelocity.y += GenerateGaussian(0, gyroStdDev);
        noisyData.AngularVelocity.z += GenerateGaussian(0, gyroStdDev);

        // Aplica ruído na Orientação 
        Vector3 rotNoise = new Vector3(
            GenerateGaussian(0, orientationStdDev),
            GenerateGaussian(0, orientationStdDev),
            GenerateGaussian(0, orientationStdDev)
        ) * Mathf.Rad2Deg;

        noisyData.Orientation *= Quaternion.Euler(rotNoise);

        // Preenche as matrizes de Covariância
        double varAccel = accelStdDev * accelStdDev;
        double varGyro = gyroStdDev * gyroStdDev;
        double varOri = orientationStdDev * orientationStdDev;

        noisyData.LinearAccelerationCovariance = new double[9] { varAccel, 0, 0, 0, varAccel, 0, 0, 0, varAccel };
        noisyData.AngularVelocityCovariance = new double[9] { varGyro, 0, 0, 0, varGyro, 0, 0, 0, varGyro };
        noisyData.OrientationCovariance = new double[9] { varOri, 0, 0, 0, varOri, 0, 0, 0, varOri };

        return noisyData;
    }

    private float GenerateGaussian(float mean, float stdDev)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}