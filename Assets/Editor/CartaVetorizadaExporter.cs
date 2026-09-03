using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MaritimeScenario.Chart;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Generates the VECTORIZED and GEOREFERENCED nautical chart from the scene's
    /// 3D ENVIRONMENT — works for both the REAL and the FICTIONAL scenario.
    ///
    /// Reads the scene's terrain mesh, reconstructs the depth grid and extracts
    /// CONTOURS via marching squares — coastline (0 m) and isobaths — which are
    /// true vectors. Converts each vertex to lat/lon (WGS84) through the
    /// georeferencing present in the scene (IGeoReference: tangent plane in the
    /// fictional one, UTM in the real one). Produces, per scenario:
    ///   - *.svg     : vector drawing (local meters, for viewing)
    ///   - *.geojson : GEOREFERENCED data in lat/lon (product for navigation)
    /// </summary>
    public static class CartaVetorizadaExporter
    {
        class Config
        {
            /// <summary>Name of the GameObject holding the terrain mesh to vectorize.</summary>
            public string nomeMalha;      // name of the terrain mesh's GameObject
            /// <summary>Vertical exaggeration applied when the terrain was built, undone here to recover real depths.</summary>
            public float exagero;         // vertical exaggeration applied to the mesh (to recover the real depth)
            /// <summary>Path of the SVG file to write, relative to the project root.</summary>
            public string saidaSvg;
            /// <summary>Path of the georeferenced GeoJSON file to write, relative to the project root.</summary>
            public string saidaGeojson;
            public List<(Vector3 pos, string tipo)> obstaculos;
        }

        // -------------------- menus --------------------

        [MenuItem("Cenário Real/2. Vetorizar Carta do Ambiente 3D")]
        public static void ExportarReal()
        {
            var obst = new List<(Vector3, string)>();
            var grupo = GameObject.Find("Obstaculos");
            if (grupo != null)
                foreach (Transform o in grupo.transform)
                    obst.Add((o.position, o.name));

            Gerar(new Config
            {
                nomeMalha = "TerrenoCarta",
                exagero = 4f, // = EXAGERO_VERTICAL from CenarioRealBuilder
                saidaSvg = "Assets/CartaReal/carta_vetorizada_unity.svg",
                saidaGeojson = "Assets/CartaReal/carta_vetorizada_unity.geojson",
                obstaculos = obst,
            });
        }

        [MenuItem("Cenário Marítimo/3. Vetorizar Carta do Ambiente 3D")]
        public static void ExportarFicticio()
        {
            var obst = new List<(Vector3, string)>();
            var fonte = Object.FindAnyObjectByType<ChartFeatureSource>();
            if (fonte != null)
                foreach (var p in fonte.Points)
                {
                    string tipo = p.ObjectClass == PointObjClass.BOYSHP ? "boia_lateral" : "rochedo";
                    obst.Add((new Vector3(p.PositionXZ.x, 0f, p.PositionXZ.y), tipo));
                }

            Gerar(new Config
            {
                nomeMalha = "TerrenoOceano",
                exagero = 1f, // the fictional scenario does not use exaggeration
                saidaSvg = "Assets/CartaNautica/carta_vetorizada_unity.svg",
                saidaGeojson = "Assets/CartaNautica/carta_vetorizada_unity.geojson",
                obstaculos = obst,
            });
        }

        // -------------------- core --------------------

        static void Gerar(Config cfg)
        {
            var terreno = AcharMeshFilter(cfg.nomeMalha);
            if (terreno == null)
            {
                EditorUtility.DisplayDialog("Ambiente não encontrado",
                    $"Não achei a malha '{cfg.nomeMalha}' na cena.\nConstrua o cenário primeiro.", "OK");
                return;
            }

            // ---- reconstructs the grid from the mesh vertices ----
            var verts = terreno.sharedMesh.vertices;
            int cols = 1;
            while (cols < verts.Length && verts[cols].x > verts[cols - 1].x) cols++;
            int rows = verts.Length / cols;
            float step = cols > 1 ? verts[1].x - verts[0].x : 1f;
            float originX = verts[0].x;   // local X coord of the corner (can be negative in the fictional one)
            float originZ = verts[0].z;

            var elev = new float[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    elev[r, c] = verts[r * cols + c].y / cfg.exagero; // real depth

            float W = (cols - 1) * step;
            float H = (rows - 1) * step;

            // ---- contours (marching squares) in grid coords (0-based) ----
            var costa = MarchingSquares(elev, step, 0f);
            var isobatas = new (float nivel, List<Vector4> segs)[]
            {
                (-2f, MarchingSquares(elev, step, -2f)),
                (-5f, MarchingSquares(elev, step, -5f)),
                (-10f, MarchingSquares(elev, step, -10f)),
                (-20f, MarchingSquares(elev, step, -20f)),
            };

            // ---- SVG (grid coords 0..W, 0..H) ----
            var sb = new StringBuilder();
            float scale = Mathf.Clamp(2000f / Mathf.Max(W, 1f), 0.06f, 6f); // ~2000 px on the longer side
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W * scale:F0}\" height=\"{H * scale:F0}\" viewBox=\"0 0 {F(W)} {F(H)}\">\n");
            sb.Append($"<rect x=\"0\" y=\"0\" width=\"{F(W)}\" height=\"{F(H)}\" fill=\"#ffffff\"/>\n");
            sb.Append($"<g transform=\"translate(0,{F(H)}) scale(1,-1)\">\n");
            DesenharPreenchimento(sb, elev, step, cols, rows);
            foreach (var (nivel, segs) in isobatas)
                DesenharSegmentos(sb, segs, "#2b6fa0", Mathf.Max(0.6f, 6f * scale));
            DesenharSegmentos(sb, costa, "#5a4a30", Mathf.Max(1.2f, 14f * scale));
            DesenharObstaculos(sb, cfg.obstaculos, originX, originZ);
            sb.Append("</g>\n</svg>\n");

            Directory.CreateDirectory(Path.GetDirectoryName(cfg.saidaSvg));
            File.WriteAllText(cfg.saidaSvg, sb.ToString());

            // ---- georeferenced GeoJSON (lat/lon) ----
            var geo = AcharGeoReference();
            string msgGeo;
            if (geo != null)
            {
                var iso = new List<(float, List<Vector4>)>();
                foreach (var it in isobatas) iso.Add((it.nivel, it.segs));
                File.WriteAllText(cfg.saidaGeojson,
                    ConstruirGeoJson(geo, originX, originZ, costa, iso, cfg.obstaculos));
                msgGeo = $"\n• {cfg.saidaGeojson}\n  (GEORREFERENCIADO em lat/lon)";
            }
            else msgGeo = "\n(Nenhum georreferenciamento na cena — GeoJSON não gerado)";

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Carta vetorizada do ambiente 3D",
                $"Malha '{cfg.nomeMalha}': {cols}x{rows} células (grade {step:F1} m).\n\n" +
                $"• {cfg.saidaSvg}\n  (desenho vetorial){msgGeo}", "OK");
        }

        // -------------------- marching squares --------------------

        static List<Vector4> MarchingSquares(float[,] g, float step, float lvl)
        {
            int rows = g.GetLength(0), cols = g.GetLength(1);
            var segs = new List<Vector4>();
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    float bl = g[r, c], br = g[r, c + 1], tl = g[r + 1, c], tr = g[r + 1, c + 1];
                    float x0 = c * step, x1 = (c + 1) * step, z0 = r * step, z1 = (r + 1) * step;
                    var cross = new List<Vector2>();
                    if ((bl < lvl) != (br < lvl)) cross.Add(new Vector2(Interp(x0, x1, bl, br, lvl), z0));
                    if ((tl < lvl) != (tr < lvl)) cross.Add(new Vector2(Interp(x0, x1, tl, tr, lvl), z1));
                    if ((bl < lvl) != (tl < lvl)) cross.Add(new Vector2(x0, Interp(z0, z1, bl, tl, lvl)));
                    if ((br < lvl) != (tr < lvl)) cross.Add(new Vector2(x1, Interp(z0, z1, br, tr, lvl)));
                    if (cross.Count == 2)
                        segs.Add(new Vector4(cross[0].x, cross[0].y, cross[1].x, cross[1].y));
                    else if (cross.Count == 4)
                    {
                        segs.Add(new Vector4(cross[0].x, cross[0].y, cross[1].x, cross[1].y));
                        segs.Add(new Vector4(cross[2].x, cross[2].y, cross[3].x, cross[3].y));
                    }
                }
            return segs;
        }

        static float Interp(float a, float b, float va, float vb, float lvl)
        {
            float t = Mathf.Approximately(vb, va) ? 0.5f : (lvl - va) / (vb - va);
            return Mathf.Lerp(a, b, Mathf.Clamp01(t));
        }

        // -------------------- SVG drawing --------------------

        static void DesenharSegmentos(StringBuilder sb, List<Vector4> segs, string cor, float largura)
        {
            if (segs.Count == 0) return;
            sb.Append($"<g stroke=\"{cor}\" stroke-width=\"{F(largura)}\" fill=\"none\" stroke-linecap=\"round\">\n");
            foreach (var s in segs)
                sb.Append($"<line x1=\"{F(s.x)}\" y1=\"{F(s.y)}\" x2=\"{F(s.z)}\" y2=\"{F(s.w)}\"/>\n");
            sb.Append("</g>\n");
        }

        static void DesenharPreenchimento(StringBuilder sb, float[,] g, float step, int cols, int rows)
        {
            int passo = Mathf.Max(1, cols / 200);
            float lado = step * passo;
            sb.Append("<g stroke=\"none\">\n");
            for (int r = 0; r < rows - 1; r += passo)
                for (int c = 0; c < cols - 1; c += passo)
                    sb.Append($"<rect x=\"{F(c * step)}\" y=\"{F(r * step)}\" width=\"{F(lado)}\" height=\"{F(lado)}\" fill=\"{CorFaixa(g[r, c])}\"/>\n");
            sb.Append("</g>\n");
        }

        static string CorFaixa(float e)
        {
            if (e > 0f) return "#e9dcc0";
            if (e >= -2f) return "#5fa0c8";
            if (e >= -5f) return "#8fbcdc";
            if (e >= -10f) return "#b9d8ec";
            if (e >= -20f) return "#dcecf7";
            return "#ffffff";
        }

        static void DesenharObstaculos(StringBuilder sb, List<(Vector3 pos, string tipo)> obst, float originX, float originZ)
        {
            if (obst == null) return;
            sb.Append("<g>\n");
            foreach (var (pos, tipo) in obst)
            {
                float x = pos.x - originX, z = pos.z - originZ; // -> SVG grid coords (0-based)
                (string cor, string forma) = SimboloDe(tipo);
                float raio = (tipo == "farol" || tipo == "naufragio") ? 45f : 35f;
                if (forma == "circulo")
                    sb.Append($"<circle cx=\"{F(x)}\" cy=\"{F(z)}\" r=\"{F(raio)}\" fill=\"{cor}\" stroke=\"#111\" stroke-width=\"6\"/>\n");
                else if (forma == "x")
                {
                    sb.Append($"<line x1=\"{F(x - raio)}\" y1=\"{F(z - raio)}\" x2=\"{F(x + raio)}\" y2=\"{F(z + raio)}\" stroke=\"{cor}\" stroke-width=\"10\"/>\n");
                    sb.Append($"<line x1=\"{F(x - raio)}\" y1=\"{F(z + raio)}\" x2=\"{F(x + raio)}\" y2=\"{F(z - raio)}\" stroke=\"{cor}\" stroke-width=\"10\"/>\n");
                }
                else
                {
                    sb.Append($"<line x1=\"{F(x - raio)}\" y1=\"{F(z)}\" x2=\"{F(x + raio)}\" y2=\"{F(z)}\" stroke=\"{cor}\" stroke-width=\"10\"/>\n");
                    sb.Append($"<line x1=\"{F(x)}\" y1=\"{F(z - raio)}\" x2=\"{F(x)}\" y2=\"{F(z + raio)}\" stroke=\"{cor}\" stroke-width=\"10\"/>\n");
                }
            }
            sb.Append("</g>\n");
        }

        static (string cor, string forma) SimboloDe(string tipo)
        {
            switch (tipo)
            {
                case "rochedo": return ("#404040", "cruz");
                case "obstaculo": return ("#e07010", "cruz");
                case "naufragio": return ("#703020", "x");
                case "farol": return ("#d020a0", "circulo");
                case "baliza": return ("#202020", "circulo");
                default: return ("#f0c020", "circulo"); // buoys
            }
        }

        // -------------------- georeferenced GeoJSON --------------------

        static string ConstruirGeoJson(IGeoReference geo, float originX, float originZ,
            List<Vector4> costa, List<(float nivel, List<Vector4> segs)> isobatas,
            List<(Vector3 pos, string tipo)> obst)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"FeatureCollection\",\"features\":[\n");
            bool primeira = true;

            EscreverLinhas(sb, geo, originX, originZ, costa, "COALNE", 0f, ref primeira);
            foreach (var (nivel, segs) in isobatas)
                EscreverLinhas(sb, geo, originX, originZ, segs, "DEPCNT", -nivel, ref primeira);

            if (obst != null)
                foreach (var (pos, tipo) in obst)
                {
                    var (lat, lon) = geo.LocalToGeographic(pos.x, pos.z);
                    if (!primeira) sb.Append(",\n");
                    primeira = false;
                    sb.Append($"{{\"type\":\"Feature\",\"properties\":{{\"OBJL\":\"{tipo}\"}},"
                              + $"\"geometry\":{{\"type\":\"Point\",\"coordinates\":[{F7(lon)},{F7(lat)}]}}}}");
                }

            sb.Append("\n]}");
            return sb.ToString();
        }

        static void EscreverLinhas(StringBuilder sb, IGeoReference geo, float originX, float originZ,
            List<Vector4> segs, string objl, float profundidade, ref bool primeira)
        {
            if (segs == null || segs.Count == 0) return;
            if (!primeira) sb.Append(",\n");
            primeira = false;

            sb.Append($"{{\"type\":\"Feature\",\"properties\":{{\"OBJL\":\"{objl}\"");
            if (objl == "DEPCNT") sb.Append($",\"VALDCO\":{F1(profundidade)}");
            sb.Append("},\"geometry\":{\"type\":\"MultiLineString\",\"coordinates\":[");
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                var (lat1, lon1) = geo.LocalToGeographic(originX + s.x, originZ + s.y);
                var (lat2, lon2) = geo.LocalToGeographic(originX + s.z, originZ + s.w);
                if (i > 0) sb.Append(",");
                sb.Append($"[[{F7(lon1)},{F7(lat1)}],[{F7(lon2)},{F7(lat2)}]]");
            }
            sb.Append("]}}");
        }

        // -------------------- utilities --------------------

        static MeshFilter AcharMeshFilter(string nome)
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
                if (mf.gameObject.name == nome && mf.sharedMesh != null)
                    return mf;
            return null;
        }

        static IGeoReference AcharGeoReference()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>())
                if (mb is IGeoReference g) return g;
            return null;
        }

        static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string F7(double d) => d.ToString("F7", CultureInfo.InvariantCulture);
        static string F1(float f) => f.ToString("F1", CultureInfo.InvariantCulture);
    }
}
