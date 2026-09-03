using UnityEngine;

public class RadarSensor : BaseSensor<RadarData>
{
    [Header("Radar/LiDAR Settings")]
    /// <summary>Number of rays in one full scan.</summary>
    [Range(360, 2000)]
    public int numSamples = 1440;
    /// <summary>Longest distance the scan reports, in meters.</summary>
    public float maxRange = 30f;
    /// <summary>Shortest distance the scan reports, in meters.</summary>
    public float minRange = 0.1f;
    
    [Header("Visual Debug")]
    /// <summary>Whether the rays are drawn in the Scene view.</summary>
    public bool showDebugRays = false;

    private float lastScanTime;

    protected override void Awake()
    {
        base.Awake();
        lastScanTime = Time.time;
    }

    protected override RadarData GenerateRawData()
    {
        RadarData data = new RadarData();
        
        float currentTime = Time.time;
        data.ScanTime = currentTime - lastScanTime;
        lastScanTime = currentTime;

        // Configurações Angulares (em Radianos)
        data.AngleMin = 0f;
        data.AngleMax = 2f * Mathf.PI;
        data.AngleIncrement = (2f * Mathf.PI) / numSamples;
        data.TimeIncrement = 0f; // Simulação instantânea
        
        data.RangeMin = minRange;
        data.RangeMax = maxRange;
        
        data.Ranges = new float[numSamples];

        // Execução do Raycasting
        for (int i = 0; i < numSamples; i++)
        {
            // Calcula o ângulo
            float angle = i * data.AngleIncrement;
            
            // angle = 0 -> Z (Frente). angle = 90 -> -X (Esquerda)
            Vector3 directionLocal = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Vector3 directionWorld = transform.rotation * directionLocal;

            if (Physics.Raycast(transform.position, directionWorld, out RaycastHit hit, maxRange))
            {
                data.Ranges[i] = hit.distance > minRange ? hit.distance : 0;
                
                if (showDebugRays && i % 10 == 0) // Desenha apenas 10% para não pesar
                    Debug.DrawLine(transform.position, hit.point, Color.green, 0.1f);
            }
            else
            {
                // Padrão para "nada detectado" é um valor fora do range
                data.Ranges[i] = maxRange + 1f; 
            }
        }

        return data;
    }
}