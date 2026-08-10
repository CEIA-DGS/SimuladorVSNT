using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Rosgraph;

public class ClockPublisher : MonoBehaviour
{   ROSConnection ros;
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ClockMsg>("/clock");
    }

    void FixedUpdate()
    {
        ClockMsg clock = new ClockMsg();
        clock.clock.sec = (int)Mathf.Floor(Time.time);
        clock.clock.nanosec = (uint)Mathf.Floor((Time.time - Mathf.Floor(Time.time)) * 1e9f);
        ros.Publish("/clock", clock);
    }
}
