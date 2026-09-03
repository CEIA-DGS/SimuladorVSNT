using UnityEngine;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using System.Collections.Generic;

public class AisSensor : BaseSensor<AisData>
{
    [Header("AIS Receiver Settings")]
    /// <summary>Maximum VHF radio range, in meters. 37000 m is roughly 20 nautical miles.</summary>
    [Tooltip("Alcance máximo do rádio VHF em metros (Ex: 37000m = ~20 Milhas Náuticas)")]
    public float maxRangeMeters = 37000f;

    [Header("World Anchor (Compartilhado com GPS)")]
    /// <summary>Latitude of the scene origin, used to convert positions to geographic coordinates.</summary>
    public double originLatitude = -15.8021;
    /// <summary>Longitude of the scene origin, used to convert positions to geographic coordinates.</summary>
    public double originLongitude = -47.8569;

    protected override AisData GenerateRawData()
    {
        AisData data = new AisData
        {
            Targets = new List<AisTargetData>()
        };

        foreach (var target in AisBroadcaster.ActiveBroadcasters)
        {
            // Ignora a si mesmo 
            if (target.gameObject == this.gameObject || target.transform.IsChildOf(transform.root))
                continue;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            
            // Se o barco estiver fora do alcance do rádio não detecta
            if (distance > maxRangeMeters) continue;

            AisTargetData tData = new AisTargetData
            {
                MMSI = target.mmsi,
                VesselType = target.vesselType
            };

            // 1. Conversão de Posição
            var targetFlu = target.transform.position.To<FLU>();
            double latOffset = targetFlu.x / 111320.0;
            double lonOffset = targetFlu.y / (111320.0 * Mathf.Cos((float)(originLatitude * Mathf.Deg2Rad)));
            
            tData.Latitude = originLatitude + latOffset;
            tData.Longitude = originLongitude + lonOffset;

            // 2. Dinâmica de Movimento
            Vector3 velocity = target.Rb != null ? target.Rb.linearVelocity : Vector3.zero;

            // Conversão de m/s para Nós
            tData.SOG = velocity.magnitude * 1.94384f;

            // Heading. Norte = 0, Leste = 90.
            tData.Heading = Mathf.Atan2(target.transform.forward.x, target.transform.forward.z) * Mathf.Rad2Deg;
            if (tData.Heading < 0) tData.Heading += 360f;

            // COG
            if (velocity.sqrMagnitude > 0.01f) 
            {
                tData.COG = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                if (tData.COG < 0) tData.COG += 360f;
            }
            else
            {
                tData.COG = tData.Heading; // Se parado, COG = Heading
            }

            data.Targets.Add(tData);
        }

        return data;
    }
}