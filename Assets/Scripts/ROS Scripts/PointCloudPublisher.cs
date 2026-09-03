using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using RosMessageTypes.Sensor; // PointCloud2Msg
using RosMessageTypes.Std;    // HeaderMsg

public class PointCloudPublisher : MonoBehaviour
{
    [Header("ROS Configuration")]
    /// <summary>ROS topic the point cloud is published on.</summary>
    public string topicName = "/camera/depth/points";
    /// <summary>Frame of reference stamped on the published cloud.</summary>
    public string frameId = "camera_link";
    /// <summary>Time between publications, in seconds. Point clouds are heavy, so the period is kept long.</summary>
    public float publishMessagePeriod = 1/10.0f; // PointClouds são pesadas, 10Hz é comum
    /// <summary>Whether the points are reported in world coordinates.</summary>
    public bool useGlobalPose = false;

    [Header("Capture Settings")]
    /// <summary>Camera used to render the depth image the cloud is built from.</summary>
    public Camera captureCamera;
    /// <summary>Render texture that receives the depth image.</summary>
    public RenderTexture depthRT;
    /// <summary>Longest distance represented in the depth image, in meters. Matches the far plane or the shader scale.</summary>
    public float maxDepth = 1000.0f; // Seu Far Plane ou escala do Shader

    private ROSConnection ros;
    private float timeElapsed;
    private Matrix4x4 invProjectionMatrix;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PointCloud2Msg>(topicName);
        invProjectionMatrix = captureCamera.projectionMatrix.inverse;
    }

    void FixedUpdate()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed >= publishMessagePeriod)
        {
            PublishPointCloud();
            timeElapsed = 0;
        }
    }

    private void PublishPointCloud()
    {
        // 1. Ler os dados da GPU para a CPU
        int w = depthRT.width;
        int h = depthRT.height;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RFloat, false);
        RenderTexture.active = depthRT;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        Color[] pixels = tex.GetPixels();

        // 2. Criar a mensagem PointCloud2
        byte[] rawData = new byte[pixels.Length * 12];
        int pointsCount = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            float rawDepth = pixels[i].r;
            if (rawDepth <= 0.0001f) continue; // Ignora valores pequenos

            // Reconstrução de posição em View Space (Câmera)
            int x = i % w;
            int y = i / w;
            float x_ndc = (x / (float)w) * 2f - 1f;
            float y_ndc = (y / (float)h) * 2f - 1f;

            Vector4 clipPos = new Vector4(x_ndc, y_ndc, 1.0f, 1.0f);
            Vector4 viewPos = invProjectionMatrix * clipPos;
            viewPos /= viewPos.w;

            // Ponto relativo à câmera (View Space)
            float scale = rawDepth / Mathf.Abs(viewPos.z);
            Vector3 point = new Vector3(viewPos.x * scale, viewPos.y * scale, rawDepth);

            // Se Global, converte para World Space
            if (useGlobalPose)
            {
                point = captureCamera.transform.TransformPoint(point);
            }

            // Conversão de Coordenadas (Unity -> ROS FLU)
            Vector3 rosPoint = new Vector3(point.z, -point.x, point.y);

            // Escreve os bytes no buffer (X, Y, Z)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes((float)rosPoint.x), 0, rawData, pointsCount * 12 + 0, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes((float)rosPoint.y), 0, rawData, pointsCount * 12 + 4, 4);
            System.Buffer.BlockCopy(System.BitConverter.GetBytes((float)rosPoint.z), 0, rawData, pointsCount * 12 + 8, 4);
            
            pointsCount++;
        }

        // 3. Montar cabeçalho e estrutura da mensagem
        PointCloud2Msg pc2 = new PointCloud2Msg();
        pc2.header = new HeaderMsg
        {
            frame_id = useGlobalPose ? "map" : frameId,
            stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg
            {
                sec = (int)Mathf.Floor(Time.time),
                nanosec = (uint)((Time.time - Mathf.Floor(Time.time)) * 1e9f)
            }
        };

        pc2.height = 1;
        pc2.width = (uint)pointsCount;
        pc2.is_bigendian = false;
        pc2.is_dense = true;
        pc2.point_step = 12;
        pc2.row_step = (uint)(12 * pointsCount);
        pc2.data = new byte[pc2.row_step];
        System.Buffer.BlockCopy(rawData, 0, pc2.data, 0, (int)pc2.row_step);

        // Definir os campos (X, Y, Z)
        pc2.fields = new PointFieldMsg[3];
        string[] names = { "x", "y", "z" };
        for (int i = 0; i < 3; i++)
        {
            pc2.fields[i] = new PointFieldMsg {
                name = names[i],
                offset = (uint)(i * 4),
                datatype = PointFieldMsg.FLOAT32,
                count = 1
            };
        }

        ros.Publish(topicName, pc2);
        Destroy(tex);
    }
}