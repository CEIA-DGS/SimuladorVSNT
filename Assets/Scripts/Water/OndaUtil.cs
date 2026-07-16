using UnityEngine;

namespace CenarioMaritimo.Water
{
    /// <summary>
    /// Fórmula única de onda (soma de senos), usada tanto pela malha visual da
    /// água (WaterAnimator) quanto pela flutuação da embarcação (BoatController) —
    /// assim o barco sobe e desce exatamente de acordo com o que se vê na água.
    /// </summary>
    public static class OndaUtil
    {
        public static float Altura(float x, float z, float tempo, float amplitude, float escala, float velocidade)
        {
            float t = tempo * velocidade;
            float onda = Mathf.Sin((x + z) * escala + t)
                        + Mathf.Sin((x - z) * escala * 1.7f + t * 1.3f)
                        + Mathf.Sin((x * 0.6f + z * 1.3f) * escala * 2.3f + t * 0.7f) * 0.5f;
            return onda * amplitude * 0.65f; // 0.65 compensa a amplitude extra do 3º termo
        }
    }
}
