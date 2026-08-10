using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MaritimeScenario.Boat;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Creates/loads the default set of vessel types (VesselType),
    /// inspired by AIS codes. The assets live in Assets/Simulador/VesselTypes/.
    /// </summary>
    public static class VesselTypeSetup
    {
        const string PASTA = "Assets/Simulador/VesselTypes";

        [MenuItem("Cenário Real/Ferramentas/Criar Tipos de Embarcação (AIS)")]
        public static void CriarMenu()
        {
            var tipos = CarregarOuCriar();
            EditorUtility.DisplayDialog("Tipos de embarcação",
                $"{tipos.Count} tipos disponíveis em {PASTA}.\n\n" +
                "Edite-os no Inspector (tamanho, velocidade, cor) — o tráfego usa esses valores.", "OK");
        }

        /// <summary>Returns all VesselType assets in the project; creates the defaults if none exist.</summary>
        public static List<VesselType> CarregarOuCriar()
        {
            var lista = new List<VesselType>();
            var guids = AssetDatabase.FindAssets("t:VesselType");
            foreach (var g in guids)
            {
                var vt = AssetDatabase.LoadAssetAtPath<VesselType>(AssetDatabase.GUIDToAssetPath(g));
                if (vt != null) lista.Add(vt);
            }
            if (lista.Count > 0) return lista;

            // none yet: create the default set
            GarantirPasta();
            lista.Add(Criar("Cargueiro", 70, new Vector2(120, 200), 0.16f, new Vector2(8, 14), new Color(0.35f, 0.30f, 0.28f), HullStyle.Cargo));
            lista.Add(Criar("Tanque", 80, new Vector2(100, 180), 0.17f, new Vector2(7, 13), new Color(0.30f, 0.32f, 0.30f), HullStyle.Cargo));
            lista.Add(Criar("Passageiros", 60, new Vector2(40, 90), 0.18f, new Vector2(10, 18), new Color(0.85f, 0.85f, 0.88f), HullStyle.Medium));
            lista.Add(Criar("Rebocador", 52, new Vector2(20, 35), 0.30f, new Vector2(6, 12), new Color(0.6f, 0.35f, 0.15f), HullStyle.Medium));
            lista.Add(Criar("Pesqueiro", 30, new Vector2(15, 30), 0.28f, new Vector2(5, 10), new Color(0.2f, 0.4f, 0.55f), HullStyle.Medium));
            lista.Add(Criar("Lancha", 37, new Vector2(8, 14), 0.32f, new Vector2(12, 25), new Color(0.9f, 0.9f, 0.92f), HullStyle.Launch));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return lista;
        }

        static VesselType Criar(string nome, int ais, Vector2 comp, float razaoBoca,
                                Vector2 vel, Color cor, HullStyle estilo)
        {
            var vt = ScriptableObject.CreateInstance<VesselType>();
            vt.DisplayName = nome;
            vt.AisCode = ais;
            vt.LengthRangeM = comp;
            vt.BeamRatio = razaoBoca;
            vt.SpeedRangeKn = vel;
            vt.HullColor = cor;
            vt.Style = estilo;
            AssetDatabase.CreateAsset(vt, $"{PASTA}/{nome}.asset");
            return vt;
        }

        static void GarantirPasta()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Simulador"))
                AssetDatabase.CreateFolder("Assets", "Simulador");
            if (!AssetDatabase.IsValidFolder(PASTA))
                AssetDatabase.CreateFolder("Assets/Simulador", "VesselTypes");
        }
    }
}
