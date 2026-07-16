using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CenarioMaritimo.Chart;
using CenarioMaritimo.Geo;

namespace CenarioMaritimo.EditorTools
{
    /// <summary>
    /// Exporta a carta náutica do cenário em dois formatos:
    ///   - carta_nautica.geojson: dado vetorial (LNDARE/DEPARE/boias/rochedos já
    ///     convertidos para lat/lon através do GeoReferenceOrigin), pronto para ser
    ///     consumido pelos módulos de navegação/percepção.
    ///   - carta_nautica_preview.png: uma captura de câmera ortográfica de cima,
    ///     como uma versão simplificada em imagem para conferência visual.
    /// </summary>
    public static class ChartExporter
    {
        const string PASTA_SAIDA = "Assets/CartaNautica";

        public static string Exportar(ChartFeatureSource fonte, GeoReferenceOrigin geo, float raioCena)
        {
            if (!Directory.Exists(PASTA_SAIDA))
                Directory.CreateDirectory(PASTA_SAIDA);

            string geojsonPath = Path.Combine(PASTA_SAIDA, "carta_nautica.geojson");
            File.WriteAllText(geojsonPath, ConstruirGeoJson(fonte, geo));

            string pngPath = Path.Combine(PASTA_SAIDA, "carta_nautica_preview.png");
            CapturarImagemTopo(pngPath, raioCena);

            AssetDatabase.Refresh();
            return Path.GetFullPath(PASTA_SAIDA);
        }

        static string F(double d) => d.ToString("F7", CultureInfo.InvariantCulture);

        static string ConstruirGeoJson(ChartFeatureSource fonte, GeoReferenceOrigin geo)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");
            bool primeira = true;

            foreach (var poly in fonte.poligonos)
            {
                if (!primeira) sb.Append(",");
                primeira = false;

                sb.Append("{\"type\":\"Feature\",\"properties\":{");
                sb.Append($"\"OBJL\":\"{poly.objectClass}\"");
                if (poly.objectClass == ObjClass.DEPARE)
                {
                    sb.Append($",\"DRVAL1\":{poly.DRVAL1.ToString(CultureInfo.InvariantCulture)}");
                    sb.Append($",\"DRVAL2\":{poly.DRVAL2.ToString(CultureInfo.InvariantCulture)}");
                }
                sb.Append("},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[");
                sb.Append(AnelParaJson(poly.ringXZ, geo, inverter: false));
                if (poly.holeXZ != null && poly.holeXZ.Count >= 3)
                {
                    // Anel interno (buraco) precisa de orientação oposta ao externo (RFC 7946).
                    sb.Append(",");
                    sb.Append(AnelParaJson(poly.holeXZ, geo, inverter: true));
                }
                sb.Append("]}}");
            }

            foreach (var pt in fonte.pontos)
            {
                sb.Append(",{\"type\":\"Feature\",\"properties\":{");
                sb.Append($"\"OBJL\":\"{pt.objectClass}\",\"nome\":\"{pt.nome}\"");
                sb.Append("},\"geometry\":{\"type\":\"Point\",\"coordinates\":[");
                var (lat, lon) = geo.LocalParaGeografica(pt.posicaoXZ.x, pt.posicaoXZ.y);
                sb.Append($"{F(lon)},{F(lat)}");
                sb.Append("]}}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        static string AnelParaJson(List<Vector2> anel, GeoReferenceOrigin geo, bool inverter)
        {
            var pontos = inverter ? Enumerable.Reverse(anel).ToList() : anel;
            var sb = new StringBuilder("[");
            for (int i = 0; i <= pontos.Count; i++)
            {
                var v = pontos[i % pontos.Count]; // fecha o anel repetindo o 1º ponto
                var (lat, lon) = geo.LocalParaGeografica(v.x, v.y);
                sb.Append($"[{F(lon)},{F(lat)}]");
                if (i < pontos.Count) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        static void CapturarImagemTopo(string caminhoPng, float raioCena)
        {
            var camGO = new GameObject("CameraCartaTemp");
            var cam = camGO.AddComponent<Camera>();
            cam.enabled = false; // renderizamos manualmente via cam.Render(), não pelo loop normal
            cam.orthographic = true;
            cam.orthographicSize = raioCena;
            cam.transform.position = new Vector3(0f, 200f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.20f);

            const int RES = 1024;
            var rt = new RenderTexture(RES, RES, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(RES, RES, TextureFormat.RGB24, false);

            cam.Render();
            var ativa = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, RES, RES), 0, 0);
            tex.Apply();
            RenderTexture.active = ativa;

            File.WriteAllBytes(caminhoPng, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
