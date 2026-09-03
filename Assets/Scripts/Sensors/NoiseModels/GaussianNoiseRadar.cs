using UnityEngine;

public class GaussianNoiseRadar : BaseNoiseModel<RadarData>
{
    [Header("Radar Noise")]
    /// <summary>Standard deviation of the range error, in meters.</summary>
    [Tooltip("Desvio padrão da precisão da distância (m)")]
    public float distanceStdDev = 0.03f; // Ex: precisão de 3cm

    public override RadarData ApplyNoise(RadarData data)
    {
        RadarData noisyData = data;
        
        noisyData.Ranges = new float[data.Ranges.Length];

        for (int i = 0; i < data.Ranges.Length; i++)
        {
            float range = data.Ranges[i];
            
            if (range >= data.RangeMin && range <= data.RangeMax)
            {
                range += GenerateGaussianNoise(0, distanceStdDev);
                
                // Garante que o ruído não empurre a leitura para fora dos limites válidos
                noisyData.Ranges[i] = Mathf.Clamp(range, data.RangeMin, data.RangeMax);
            }
            else
            {
                noisyData.Ranges[i] = range;
            }
        }

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