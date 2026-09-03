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
        /// <summary>S-57 object class of this area feature.</summary>
        public ObjClass ObjectClass;
        /// <summary>Outer polygon ring, in local scene coordinates (X, Z), in meters.</summary>
        public List<Vector2> RingXZ = new(); // outer polygon ring, local X,Z plane (meters)
        /// <summary>Inner ring of the polygon (a hole), optional.</summary>
        public List<Vector2> HoleXZ; // inner ring (hole), optional — e.g. the shallower band / the island
        /// <summary>Minimum depth in meters below datum. Depth areas (DEPARE) only.</summary>
        public float DRVAL1; // minimum depth (m below datum) — DEPARE only
        /// <summary>Maximum depth in meters below datum. Depth areas (DEPARE) only.</summary>
        public float DRVAL2; // maximum depth (m below datum) — DEPARE only
    }

    /// <summary>A point chart feature (buoy or rock) in the local X,Z plane.</summary>
    [Serializable]
    public class ChartPointFeature
    {
        /// <summary>S-57 object class of this point feature.</summary>
        public PointObjClass ObjectClass;
        /// <summary>Position in the local scene plane (X, Z), in meters.</summary>
        public Vector2 PositionXZ; // local X,Z plane (meters) — .x = X, .y = Z
        /// <summary>Name of the feature, when the chart provides one.</summary>
        public string Name;
    }
}
