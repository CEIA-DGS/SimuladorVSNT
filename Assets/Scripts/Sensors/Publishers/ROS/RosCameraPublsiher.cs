using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;

public class RosCameraPublisher : BasePublisher<CameraData>
{
    [Header("ROS Setup")]
    public string topicName = "/camera/image_raw";
    public string frameId = "camera_link";

    private ROSConnection ros;
    private uint sequenceCount = 0;

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
                seq = ++sequenceCount,
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

    private RosMessageTypes.BuiltinInterfaces.TimeMsg GetTimeStamp()
    {
        float timeNow = Time.time;
        return new RosMessageTypes.BuiltinInterfaces.TimeMsg
        {
            sec = (uint)Mathf.Floor(timeNow),
            nanosec = (uint)Mathf.Floor((timeNow - Mathf.Floor(timeNow)) * 1e9f)
        };
    }
}