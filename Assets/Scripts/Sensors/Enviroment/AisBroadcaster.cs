using UnityEngine;
using System.Collections.Generic;

public class AisBroadcaster : MonoBehaviour
{
    public static readonly List<AisBroadcaster> ActiveBroadcasters = new List<AisBroadcaster>();

    [Header("AIS Static Data")]
    public uint mmsi = 123456789;
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