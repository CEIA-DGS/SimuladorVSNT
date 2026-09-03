public struct CameraData
{
    /// <summary>Raw image bytes, as captured.</summary>
    public byte[] ImageData;
    /// <summary>Image width, in pixels.</summary>
    public int Width;
    /// <summary>Image height, in pixels.</summary>
    public int Height;
    /// <summary>Vertical field of view of the camera, in degrees.</summary>
    public float FieldOfView;
}