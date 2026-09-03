using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Dt;

public class RosAisPublisher : BasePublisher<AisData>
{
    [Header("ROS Setup")]
    /// <summary>ROS topic the AIS report is published on.</summary>
    public string topicName = "/ais/report";
    /// <summary>Frame of reference stamped on the published report.</summary>
    public string frameId = "ais_antenna_link";

    private ROSConnection ros;

    protected override void SetupPublisher()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<AisReportMsg>(topicName);
    }

    protected override void PublishMessage(AisData data)
    {
        // Cria o array de alvos baseado no tamanho da lista detectada
        AisTargetMsg[] targetMsgs = new AisTargetMsg[data.Targets.Count];

        for (int i = 0; i < data.Targets.Count; i++)
        {
            targetMsgs[i] = new AisTargetMsg
            {
                mmsi = data.Targets[i].MMSI,
                type = data.Targets[i].VesselType,
                latitude = data.Targets[i].Latitude,
                longitude = data.Targets[i].Longitude,
                cog = data.Targets[i].COG,
                sog = data.Targets[i].SOG,
                heading = data.Targets[i].Heading
            };
        }

        var msg = new AisReportMsg
        {
            header = new RosMessageTypes.Std.HeaderMsg
            {
                frame_id = frameId,
                stamp = GetTimeStamp()
            },
            targets = targetMsgs
        };

        ros.Publish(topicName, msg);
    }

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