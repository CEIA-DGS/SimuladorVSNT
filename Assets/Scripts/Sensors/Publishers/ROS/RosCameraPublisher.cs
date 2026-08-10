using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

/// <summary>
/// Publishes camera frame data (image raw) to a ROS topic.
/// </summary>
public class RosCameraPublisher : BasePublisher<CameraData>
{
    [Header("ROS Setup")]
    public string topicName = "/camera/image_raw";
    public string frameId = "camera_link";

    private ROSConnection ros;

    protected override void SetupPublisher()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImageMsg>(topicName);
    }

    protected override void PublishMessage(CameraData data)
    {
        var msg = new ImageMsg
        {
            header = new HeaderMsg
            {
                frame_id = frameId,
                stamp = GetTimeStamp()
            },
            height = (uint)data.Height,
            width = (uint)data.Width,
            encoding = "rgb8", 
            is_bigendian = 0,
            step = (uint)(data.Width * 3),
            data = data.ImageData
        };

        ros.Publish(topicName, msg);
    }

    /// <summary>
    /// Generates a ROS-compatible timestamp based on Unity's elapsed time.
    /// </summary>
    /// <returns>A TimeMsg structure containing seconds and nanoseconds.</returns>
    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetTimeStamp()
    {
        float timeNow = Time.time;
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (int)Mathf.Floor(timeNow),
            nanosec = (uint)Mathf.Floor((timeNow - Mathf.Floor(timeNow)) * 1e9f)
        };
    }
}