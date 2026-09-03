using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class GpsSensor : BaseSensor<GpsData>
{
    [Header("World Anchor (0,0,0 do Unity)")]
    /// <summary>Latitude of the scene origin, in decimal degrees.</summary>
    public double originLatitude = -15.8021; 
    /// <summary>Longitude of the scene origin, in decimal degrees.</summary>
    public double originLongitude = -47.8569;
    /// <summary>Altitude of the scene origin, in meters.</summary>
    public double originAltitude = 1000.0;

    private Rigidbody parentRb;

    protected override void Awake()
    {
        base.Awake();
        parentRb = GetComponentInParent<Rigidbody>();

        if (parentRb == null)
        {
            Debug.LogWarning($"[GpsSensor] Nenhum Rigidbody em {gameObject.name}. Assumindo base estática.");
        }
    }

    protected override GpsData GenerateRawData()
    {
        GpsData data = new GpsData();

        // 1. Coordenadas locais do Unity para FLU (Right-Handed)
        var fluPosition = transform.position.To<FLU>();

        // 2. Conversão Cartesiana -> Geográfica
        double latOffset = fluPosition.x / 111320.0;
        double lonOffset = fluPosition.y / (111320.0 * Mathf.Cos((float)(originLatitude * Mathf.Deg2Rad)));

        data.Latitude = originLatitude + latOffset;
        data.Longitude = originLongitude + lonOffset;
        data.Altitude = originAltitude + transform.position.y;

        // 3. Velocidade
        if (parentRb != null)
            data.GlobalVelocity = parentRb.GetPointVelocity(transform.position);
        else
            data.GlobalVelocity = Vector3.zero;

        // 4. Inicializa covariância zerada
        data.PositionCovariance = new double[9];

        return data;
    }
}