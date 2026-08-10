namespace MaritimeScenario
{
    /// <summary>
    /// Common georeferencing contract: converts a local Unity position
    /// (X = East, Z = North, in meters) to latitude/longitude (degrees).
    /// Implemented both by the fictional scenario (tangent plane) and by the
    /// real scenario (UTM), so the vessel can display lat/lon in both cases.
    /// </summary>
    public interface IGeoReference
    {
        /// <summary>
        /// Converts a local position to geographic coordinates.
        /// </summary>
        /// <param name="x">Local X coordinate (East), in meters.</param>
        /// <param name="z">Local Z coordinate (North), in meters.</param>
        /// <returns>Latitude and longitude, in degrees.</returns>
        (double lat, double lon) LocalToGeographic(float x, float z);
    }
}
