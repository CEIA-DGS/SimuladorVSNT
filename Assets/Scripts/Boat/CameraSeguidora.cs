using UnityEngine;

namespace CenarioMaritimo.Boat
{
    /// <summary>
    /// Câmera "chase cam" simples: segue um alvo (a embarcação) mantendo um
    /// deslocamento relativo suavizado. Só age em Play (LateUpdate não roda em
    /// modo de edição), então não atrapalha o enquadramento estático da cena.
    /// </summary>
    public class CameraSeguidora : MonoBehaviour
    {
        public Transform alvo;
        public Vector3 deslocamento = new Vector3(0f, 2.2f, -6f);
        public Vector3 alturaAlvoOlhar = new Vector3(0f, 1f, 0f);
        public float suavizacao = 4f;

        void LateUpdate()
        {
            if (alvo == null) return;

            Vector3 posDesejada = alvo.TransformPoint(deslocamento);
            transform.position = Vector3.Lerp(transform.position, posDesejada, suavizacao * Time.deltaTime);
            transform.LookAt(alvo.position + alturaAlvoOlhar);
        }
    }
}
