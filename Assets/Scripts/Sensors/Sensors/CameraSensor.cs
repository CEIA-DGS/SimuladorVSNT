using UnityEngine;

// Obriga o Unity a ter um componente Camera neste mesmo GameObject
[RequireComponent(typeof(Camera))]
public class CameraSensor : BaseSensor<CameraData>
{
    [Header("Resolution Settings")]
    /// <summary>Width of the captured image, in pixels.</summary>
    public int width = 640;
    /// <summary>Height of the captured image, in pixels.</summary>
    public int height = 480;

    private Camera cam;
    private RenderTexture targetRT;
    private RenderTexture flippedRT;
    private Texture2D outputTexture;

    protected override void Awake()
    {
        base.Awake();
        cam = GetComponent<Camera>();

        // 1. Pré-aloca a memória de vídeo e RAM
        targetRT = new RenderTexture(width, height, 24);
        flippedRT = new RenderTexture(width, height, 24);
        outputTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

        // 2. Aponta a câmera para a RT
        cam.targetTexture = targetRT;
        
        cam.enabled = false; 
    }

    protected override CameraData GenerateRawData()
    {
        // 1. Renderiza a cena atual para o targetRT
        cam.Render();

        Graphics.Blit(targetRT, flippedRT, new Vector2(1, -1), new Vector2(0, 1));

        // 3. Lê os pixels da GPU para a CPU
        RenderTexture.active = flippedRT;
        outputTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        outputTexture.Apply();
        RenderTexture.active = null;

        // 4. Empacota os dados
        CameraData data = new CameraData
        {
            ImageData = outputTexture.GetRawTextureData(),
            Width = width,
            Height = height,
            FieldOfView = cam.fieldOfView
        };

        return data;
    }

    // Libera a memória de vídeo
    private void OnDestroy()
    {
        if (targetRT != null) targetRT.Release();
        if (flippedRT != null) flippedRT.Release();
        if (outputTexture != null) Destroy(outputTexture);
    }
}