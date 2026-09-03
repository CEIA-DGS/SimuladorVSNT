using UnityEngine;

public class GaussianNoiseGps : BaseNoiseModel<GpsData>
{
    [Header("GPS Noise (Metros)")]
    /// <summary>Standard deviation of the horizontal position error, in meters.</summary>
    public float horizontalStdDev = 1.5f; 
    /// <summary>Standard deviation of the vertical position error, in meters.</summary>
    public float verticalStdDev = 3.0f;

    public override GpsData ApplyNoise(GpsData data)
    {
        GpsData noisyData = data;

        // 1. Gera o ruído em metros
        float latNoiseMetros = GenerateGaussianNoise(0, horizontalStdDev);
        float lonNoiseMetros = GenerateGaussianNoise(0, horizontalStdDev);
        float altNoiseMetros = GenerateGaussianNoise(0, verticalStdDev);

        // 2. Converte o ruído de metros para graus
        noisyData.Latitude += latNoiseMetros / 111320.0;
        noisyData.Longitude += lonNoiseMetros / (111320.0 * Mathf.Cos((float)(data.Latitude * Mathf.Deg2Rad)));
        noisyData.Altitude += altNoiseMetros;

        // 3. Preenche a matriz de covariância (E, N, U) com as variâncias
        double varH = horizontalStdDev * horizontalStdDev;
        double varV = verticalStdDev * verticalStdDev;

        noisyData.PositionCovariance = new double[9] 
        {
            varH, 0,    0,
            0,    varH, 0,
            0,    0,    varV
        };

        return noisyData;
    }

    private float GenerateGaussianNoise(float mean, float stdDev)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}