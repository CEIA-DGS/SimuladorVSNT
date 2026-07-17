using System.Collections.Generic;

public struct AisTargetData
{
    public uint MMSI;
    public byte VesselType;
    public double Latitude;
    public double Longitude;
    public float COG; // Course Over Ground (Graus)
    public float SOG; // Speed Over Ground (Nós)
    public float Heading; // Proa verdadeira (Graus)
}

public struct AisData
{
    public List<AisTargetData> Targets;
}