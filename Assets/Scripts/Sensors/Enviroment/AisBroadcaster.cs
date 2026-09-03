using UnityEngine;
using System.Collections.Generic;

public class AisBroadcaster : MonoBehaviour
{
    /// <summary>Every broadcaster currently in the scene. The AIS sensor reads this list instead of searching the scene on each scan.</summary>
    public static readonly List<AisBroadcaster> ActiveBroadcasters = new List<AisBroadcaster>();

    [Header("AIS Static Data")]
    /// <summary>Maritime Mobile Service Identity of this vessel. Must be unique within the scene.</summary>
    public uint mmsi = 123456789;
    /// <summary>Vessel type code from the ITU table, such as 70 for cargo, 30 for fishing and 99 for other.</summary>
    [Tooltip("Tabela ITU. Ex: 70=Carga, 30=Pesca, 99=Outros")]
    public byte vesselType = 70; 
    public Rigidbody Rb { get; private set; }

    void Awake()
    {
        Rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        if (!ActiveBroadcasters.Contains(this))
            ActiveBroadcasters.Add(this);
    }

    void OnDisable()
    {
        if (ActiveBroadcasters.Contains(this))
            ActiveBroadcasters.Remove(this);
    }
}