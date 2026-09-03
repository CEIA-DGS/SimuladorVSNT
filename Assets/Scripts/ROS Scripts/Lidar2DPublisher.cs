using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System;

public class Lidar2DPublisher : MonoBehaviour
{
    [Header("ROS Configuration")]
    /// <summary>ROS topic the laser scan is published on.</summary>
    public string topicName = "/scan";
    /// <summary>Frame of reference stamped on the published scan.</summary>
    public string frameId = "lidar_link";
    /// <summary>Publication rate, in hertz.</summary>
    public float publishFrequency = 10f; // Hz

    [Header("LiDAR Settings")]
    /// <summary>Number of rays in one full scan.</summary>
    [Range(360, 2000)]
    public int numSamples = 1440;
    /// <summary>Longest distance the scan reports, in meters.</summary>
    public float maxRange = 30f;
    /// <summary>Shortest distance the scan reports, in meters. Closer returns are discarded.</summary>
    public float minRange = 0.1f;
    
    [Header("Visual Debug")]
    /// <summary>Whether the rays are drawn in the Scene view.</summary>
    public bool showDebugRays = false;

    private ROSConnection ros;
    private float timeElapsed;
    private float lastPublishTime;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<LaserScanMsg>(topicName);
    }

    void FixedUpdate()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed >= (1f / publishFrequency))
        {
            PublishLaserScan();
            timeElapsed = 0;
        }
    }

    private void PublishLaserScan()
    {
        LaserScanMsg scan = new LaserScanMsg();

        // 1. Cabeçalho (Header)
        scan.header = new HeaderMsg
        {
            frame_id = frameId,
            stamp = GetRosTime()
        };

        // 2. Configurações Angulares (em Radianos)
        // No ROS, 0 rad é para frente (X), positivo é sentido anti-horário
        scan.angle_min = 0;
        scan.angle_max = 2f * Mathf.PI;
        scan.angle_increment = (2f * Mathf.PI) / numSamples;
        
        scan.time_increment = 0; // Simulação instantânea
        scan.scan_time = 1f / publishFrequency;
        scan.range_min = minRange;
        scan.range_max = maxRange;

        // 3. Execução do Raycasting
        float[] ranges = new float[numSamples];
        
        for (int i = 0; i < numSamples; i++)
        {
            // Calcula o ângulo para este sample (Sentido Anti-horário no Plano XZ do Unity)
            float angle = i * scan.angle_increment;
            
            Vector3 directionLocal = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            Vector3 directionWorld = transform.rotation * directionLocal;

            if (Physics.Raycast(transform.position, directionWorld, out RaycastHit hit, maxRange))
            {
                ranges[i] = hit.distance > minRange ? hit.distance : 0;
                
                if (showDebugRays && i % 10 == 0) // Desenha apenas 10% dos raios para não pesar
                    Debug.DrawLine(transform.position, hit.point, Color.green, 0.1f);
            }
            else
            {
                // Se não bater em nada, o padrão ROS é colocar um valor acima do range_max ou Infinito
                ranges[i] = maxRange + 1f; 
            }
        }

        scan.ranges = ranges;

        // 4. Publicar
        ros.Publish(topicName, scan);
    }

    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetRosTime()
    {
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (int)Mathf.Floor(Time.time),
            nanosec = (uint)((Time.time - Mathf.Floor(Time.time)) * 1e9f)
        };
    }
}