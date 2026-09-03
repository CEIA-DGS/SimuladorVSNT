using System.Collections.Generic;

public struct AisTargetData
{
    /// <summary>Maritime Mobile Service Identity of the transmitting vessel.</summary>
    public uint MMSI;
    /// <summary>Vessel type code, from the ITU table.</summary>
    public byte VesselType;
    /// <summary>Reported latitude, in decimal degrees.</summary>
    public double Latitude;
    /// <summary>Reported longitude, in decimal degrees.</summary>
    public double Longitude;
    /// <summary>Course over ground, in degrees.</summary>
    public float COG; // Course Over Ground (Graus)
    /// <summary>Speed over ground, in knots.</summary>
    public float SOG; // Speed Over Ground (Nós)
    /// <summary>True heading, in degrees.</summary>
    public float Heading; // Proa verdadeira (Graus)
}

public struct AisData
{
    /// <summary>The AIS contacts carried in this report.</summary>
    public List<AisTargetData> Targets;
}