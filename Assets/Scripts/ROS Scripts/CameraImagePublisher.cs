using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor; // Para ImageMsg
using RosMessageTypes.Std;    // Para HeaderMsg

public class CameraImagePublisher : MonoBehaviour
{
    [Header("ROS Configuration")]
    public string topicName = "/camera/image_raw";
    public string frameId = "camera_link";
    public float publishRate = 10f; // 10Hz é um bom padrão para não sobrecarregar a rede

    [Header("Capture Settings")]
    public Camera captureCamera;
    public int resolutionWidth = 640;
    public int resolutionHeight = 480;

    private ROSConnection ros;
    private float timeElapsed;
    private RenderTexture renderTexture;
    private Texture2D texture2D;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ImageMsg>(topicName);

        // Inicializa as texturas com a resolução desejada e formato RGB24 (3 bytes por pixel)
        renderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24);
        texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);

        if (captureCamera != null)
        {
            // Força a câmera a renderizar para a nossa textura
            captureCamera.targetTexture = renderTexture;
        }
        else
        {
            Debug.LogError("Capture Camera não atribuída no script!");
        }
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed >= (1f / publishRate))
        {
            PublishImage();
            timeElapsed = 0;
        }
    }

    private void PublishImage()
    {
        if (captureCamera == null) return;

        // 1. Renderiza a visão da câmera
        captureCamera.Render();
        RenderTexture.active = renderTexture;

        // 2. Lê os pixels da GPU para a CPU
        texture2D.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;

        // 3. Inverte a imagem no eixo Y (A Unity é Bottom-Up, o ROS é Top-Down)
        byte[] imageData = GetFlippedImageData(texture2D);

        // 4. Monta a mensagem Image do ROS
        // 4. Monta a mensagem Image do ROS
        ImageMsg imageMsg = new ImageMsg
        {
            header = new HeaderMsg
            {
                frame_id = frameId,
                stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg
                {
                    sec = (int)Mathf.Floor(Time.time), // <--- CORREÇÃO AQUI: mudou de (int) para (uint)
                    nanosec = (uint)((Time.time - Mathf.Floor(Time.time)) * 1e9f)
                }
            },
            height = (uint)resolutionHeight,
            width = (uint)resolutionWidth,
            encoding = "rgb8",     // Padrão ROS para imagens coloridas de 8 bits
            is_bigendian = 0,
            step = (uint)(resolutionWidth * 3), // Largura * Número de Canais (R, G, B)
            data = imageData
        };

        // 5. Publica no tópico
        ros.Publish(topicName, imageMsg);
    }

    // Função auxiliar para inverter as linhas da imagem
    private byte[] GetFlippedImageData(Texture2D tex)
    {
        byte[] rawData = tex.GetRawTextureData();
        byte[] flippedData = new byte[rawData.Length];
        int rowBytes = resolutionWidth * 3; // 3 bytes por pixel (RGB)

        for (int y = 0; y < resolutionHeight; y++)
        {
            int srcIndex = y * rowBytes;
            int destIndex = (resolutionHeight - 1 - y) * rowBytes;
            System.Array.Copy(rawData, srcIndex, flippedData, destIndex, rowBytes);
        }

        return flippedData;
    }
}