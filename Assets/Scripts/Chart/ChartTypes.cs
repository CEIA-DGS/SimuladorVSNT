using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaritimeScenario.Chart
{
    /// <summary>
    /// Chart area object classes, named after the IHO S-57 standard (see the
    /// "requirements survey" report, section 2.3): LNDARE = land area,
    /// DEPARE = depth area.
    /// </summary>
    public enum ObjClass { LNDARE, DEPARE }

    /// <summary>Chart point object classes: BOYSHP = buoy, UWTROC = underwater rock.</summary>
    public enum PointObjClass { BOYSHP, UWTROC }

    /// <summary>A polygonal chart feature (land or depth area) in the local X,Z plane.</summary>
    [Serializable]
    public class ChartFeature
    {
        public ObjClass ObjectClass;
        public List<Vector2> RingXZ = new(); // outer polygon ring, local X,Z plane (meters)
        public List<Vector2> HoleXZ; // inner ring (hole), optional — e.g. the shallower band / the island
        public float DRVAL1; // minimum depth (m below datum) — DEPARE only
        public float DRVAL2; // maximum depth (m below datum) — DEPARE only
    }

    /// <summary>A point chart feature (buoy or rock) in the local X,Z plane.</summary>
    [Serializable]
    public class ChartPointFeature
    {
        public PointObjClass ObjectClass;
        public Vector2 PositionXZ; // local X,Z plane (meters) — .x = X, .y = Z
        public string Name;
    }
}
