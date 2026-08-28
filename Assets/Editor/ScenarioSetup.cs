using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MaritimeScenario.Testing;

namespace MaritimeScenario.EditorTools
{
    /// <summary>
    /// Cria o conjunto padrão de cenários determinísticos de teste de navegação.
    /// Os quatro cenários reproduzem as situações de encontro clássicas do RIPEAM/COLREGS,
    /// que são a base de comparação usual dos algoritmos de desvio de colisão:
    ///   • obstáculo estático;
    ///   • cruzamento perpendicular (Regra 15);
    ///   • encontro roda-a-roda / head-on (Regra 14);
    ///   • ultrapassagem de alvo mais lento (Regra 13).
    ///
    /// Os assets ficam em Assets/Simulador/Cenarios/ e são totalmente editáveis no Inspector.
    /// </summary>
    public static class ScenarioSetup
    {
        const string PASTA = "Assets/Simulador/Cenarios";

        /// <summary>Arquivo da bateria padrão de testes, lido pelo ScenarioSuiteRunner.</summary>
        const string ARQUIVO_SUITE = PASTA + "/suite_padrao.yaml";

        /// <summary>Ponto de partida do USV, em água aberta da carta da Baía de Guanabara.</summary>
        static readonly Vector2 PARTIDA_USV = new Vector2(9900f, 7500f);

        /// <summary>
        /// Rota reta rumo ao Norte, comum a todos os cenários. O comprimento é escolhido
        /// para o USV conseguir concluí-la dentro do tempo limite: a 12 nós ele cobre
        /// 1000 m em cerca de 163 s. Uma rota que nunca termina faria toda execução
        /// encerrar por estouro de tempo, e o critério "rota concluída" nunca seria
        /// exercitado.
        /// </summary>
        static readonly List<Vector2> ROTA_NORTE = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1000f)
        };

        [MenuItem("Cenário Real/Ferramentas/Criar Cenários de Teste (determinísticos)")]
        public static void CriarMenu()
        {
            var cenarios = CarregarOuCriar();
            EditorUtility.DisplayDialog("Cenários de teste",
                $"{cenarios.Count} cenários disponíveis em {PASTA}.\n\n" +
                "Use 'Cenário Real > 4. Preparar Bancada de Testes' para montar o objeto " +
                "que executa os cenários.", "OK");
        }

        /// <summary>
        /// Monta na cena o objeto que executa os cenários, já com o primeiro cenário
        /// atribuído. Evita o passo manual de criar o GameObject e adicionar os
        /// componentes na mão — basta dar Play depois.
        /// </summary>
        [MenuItem("Cenário Real/4. Preparar Bancada de Testes")]
        public static void PrepararBancada()
        {
            var cenarios = CarregarOuCriar();
            if (cenarios.Count == 0)
            {
                EditorUtility.DisplayDialog("Sem cenários",
                    "Nenhum cenário de teste disponível.", "OK");
                return;
            }

            const string NOME = "BancadaDeTestes";
            var antigo = GameObject.Find(NOME);
            if (antigo != null) Undo.DestroyObjectImmediate(antigo);

            var bancada = new GameObject(NOME);
            Undo.RegisterCreatedObjectUndo(bancada, "Preparar Bancada de Testes");

            var gerador = bancada.AddComponent<RandomScenarioGenerator>();

            // A bancada começa nos cenários determinísticos, de leitura mais direta.
            // Para o teste de estresse basta marcar 'Use Random Generator' no runner.
            var runner = bancada.AddComponent<ScenarioRunner>();
            runner.Scenario = cenarios[0];
            runner.Generator = gerador;
            runner.UseRandomGenerator = false;
            runner.RunOnStart = true;

            EditorSceneManager.MarkSceneDirty(bancada.scene);
            Selection.activeGameObject = bancada;

            EditorUtility.DisplayDialog("Bancada pronta",
                $"Objeto '{NOME}' criado com o cenário '{cenarios[0].DisplayName}'.\n\n" +
                "• Dê Play para executar; o relatório e o mapa saem no Console.\n" +
                "• Para trocar de cenário, use o campo 'Scenario' no Inspector.\n" +
                "• Para o teste de estresse aleatório, marque 'Use Random Generator'.", "OK");
        }

        // ---------------- configuração por arquivo (YAML) ----------------

        /// <summary>
        /// Grava cada cenário do projeto como um arquivo .yaml e monta a suíte padrão que
        /// os executa em sequência. É o que permite versionar, revisar em diff e variar a
        /// bateria de testes sem abrir a Unity.
        /// </summary>
        [MenuItem("Cenário Real/Ferramentas/Exportar Cenários para YAML")]
        public static void ExportarParaYaml()
        {
            var cenarios = CarregarOuCriar();
            if (cenarios.Count == 0)
            {
                EditorUtility.DisplayDialog("Sem cenários", "Nenhum cenário de teste disponível.", "OK");
                return;
            }

            // Os arquivos versionados no repositório têm comentários explicando cada
            // número, e a exportação os reescreve a partir dos assets — perdendo os
            // comentários. Melhor perguntar do que apagar o trabalho de alguém.
            if (!ConfirmarSobrescrita(cenarios)) return;

            var arquivos = new List<string>();
            foreach (var cenario in cenarios)
            {
                string nome = NomeDeArquivo(cenario);
                ScenarioConfig.Save(cenario, $"{PASTA}/{nome}{ScenarioConfig.EXTENSION}");
                arquivos.Add(nome + ScenarioConfig.EXTENSION);
            }

            string suite = EscreverSuitePadrao(arquivos);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Cenários exportados",
                $"{arquivos.Count} cenários gravados em {PASTA}.\n\n" +
                $"Suíte criada em:\n{ARQUIVO_SUITE}\n\n" +
                "Use 'Cenário Real > 5. Preparar Bancada por Arquivo' para executá-la.", "OK");

            Debug.Log($"[ScenarioSetup] Suíte de testes escrita em {suite}");
        }

        /// <summary>
        /// Monta na cena o objeto que executa a bateria inteira a partir do arquivo YAML,
        /// em uma única sessão de Play, exportando os mapas e as análises ao final.
        /// </summary>
        [MenuItem("Cenário Real/5. Preparar Bancada por Arquivo (YAML)")]
        public static void PrepararBancadaPorArquivo()
        {
            if (!System.IO.File.Exists(ScenarioConfig.ResolveProjectPath(ARQUIVO_SUITE)))
            {
                ExportarParaYaml();

                // A exportação pode ter sido cancelada; sem o arquivo não há o que rodar.
                if (!System.IO.File.Exists(ScenarioConfig.ResolveProjectPath(ARQUIVO_SUITE)))
                {
                    EditorUtility.DisplayDialog("Sem suíte",
                        $"O arquivo {ARQUIVO_SUITE} não existe.\n\n" +
                        "Use 'Cenário Real > Ferramentas > Exportar Cenários para YAML' " +
                        "para criá-lo.", "OK");
                    return;
                }
            }

            const string NOME = "BancadaDeTestes";
            var antigo = GameObject.Find(NOME);
            if (antigo != null) Undo.DestroyObjectImmediate(antigo);

            var bancada = new GameObject(NOME);
            Undo.RegisterCreatedObjectUndo(bancada, "Preparar Bancada por Arquivo");

            var gerador = bancada.AddComponent<RandomScenarioGenerator>();

            // O runner executa um cenário de cada vez; quem decide a ordem e escreve as
            // análises é a suíte, então ele não deve disparar nada sozinho no Play.
            var runner = bancada.AddComponent<ScenarioRunner>();
            runner.Generator = gerador;
            runner.RunOnStart = false;

            var suite = bancada.AddComponent<ScenarioSuiteRunner>();
            suite.Runner = runner;
            suite.Generator = gerador;
            suite.SuiteFile = ARQUIVO_SUITE;
            suite.RunOnStart = true;

            EditorSceneManager.MarkSceneDirty(bancada.scene);
            Selection.activeGameObject = bancada;

            EditorUtility.DisplayDialog("Bancada por arquivo pronta",
                $"Objeto '{NOME}' criado, lendo a bateria de:\n{ARQUIVO_SUITE}\n\n" +
                "• Dê Play: os cenários rodam em sequência e o resumo sai no Console.\n" +
                "• Os CSV e o resumo em Markdown ficam na pasta 'output.folder' da suíte.\n" +
                "• Para mudar a bateria, edite o arquivo YAML — não precisa mexer na cena.", "OK");
        }

        /// <summary>
        /// Escreve o arquivo da suíte padrão: os cenários determinísticos exportados, mais
        /// uma varredura de sementes para o teste de estresse.
        /// </summary>
        /// <param name="arquivos">Nomes dos arquivos .yaml dos cenários, na ordem de execução.</param>
        /// <returns>Caminho absoluto do arquivo escrito.</returns>
        static string EscreverSuitePadrao(List<string> arquivos)
        {
            var raiz = YamlNode.NewMapping();
            raiz.Add("suite", "Bateria padrão RIPEAM");
            raiz.Add("description",
                "Os quatro encontros clássicos do RIPEAM mais uma varredura de sementes " +
                "de estresse. Rodar esta bateria antes e depois de mexer no algoritmo de " +
                "navegação dá os números para comparar as duas versões.");

            var ambiente = YamlNode.NewMapping();
            ambiente.Add("waterHeight", 0.05f);
            ambiente.Add("fixedTimeStep", 0.02f);
            ambiente.Add("randomSeed", 12345);
            ambiente.Add("timeScale", 8f);
            raiz.Add("environment", ambiente);

            var saida = YamlNode.NewMapping();
            saida.Add("folder", "Assets/CartaReal/Testes");
            saida.Add("exportMaps", true);
            saida.Add("exportResults", true);
            saida.Add("csvSeparator", ",");
            raiz.Add("output", saida);

            var lista = YamlNode.NewSequence();
            foreach (string arquivo in arquivos)
            {
                var entrada = YamlNode.NewMapping();
                entrada.Add("file", arquivo);
                lista.AddItem(entrada);
            }

            // A entrada aleatória vira uma execução por semente. Três sementes já mostram
            // se o comportamento é estável sem transformar a bateria em algo demorado.
            var aleatorio = YamlNode.NewMapping();
            var sementes = YamlNode.NewSequence(flow: true);
            sementes.AddItem(YamlNode.NewScalar("20260813"));
            sementes.AddItem(YamlNode.NewScalar("20260814"));
            sementes.AddItem(YamlNode.NewScalar("20260815"));
            aleatorio.Add("seeds", sementes);
            aleatorio.Add("usvStartXZ", PARTIDA_USV);
            aleatorio.Add("areaRadius", 2000f);
            aleatorio.Add("minDistanceFromUsv", 300f);
            aleatorio.Add("targetCount", new Vector2(6f, 14f));
            aleatorio.Add("speedRangeKnots", new Vector2(4f, 16f));
            aleatorio.Add("lengthRangeMeters", new Vector2(15f, 120f));
            aleatorio.Add("staticTargetRatio", 0.15f);
            aleatorio.Add("routePoints", new Vector2(2f, 4f));
            aleatorio.Add("legLengthMeters", new Vector2(400f, 1500f));
            aleatorio.Add("publishUsvWaypoints", true);
            aleatorio.Add("maxDurationSeconds", 300f);
            aleatorio.Add("minSafeDistanceMeters", 100f);

            var entradaAleatoria = YamlNode.NewMapping();
            entradaAleatoria.Add("random", aleatorio);
            lista.AddItem(entradaAleatoria);

            raiz.Add("scenarios", lista);

            string caminho = ScenarioConfig.ResolveProjectPath(ARQUIVO_SUITE);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(caminho));
            System.IO.File.WriteAllText(caminho, YamlLite.Write(raiz));
            return caminho;
        }

        /// <summary>
        /// Avisa quais arquivos .yaml já existem e serão reescritos, e deixa cancelar.
        /// </summary>
        /// <param name="cenarios">Cenários que serão exportados.</param>
        /// <returns>True quando a exportação pode prosseguir.</returns>
        static bool ConfirmarSobrescrita(List<ScenarioDefinition> cenarios)
        {
            var existentes = new List<string>();

            foreach (var cenario in cenarios)
            {
                string arquivo = $"{PASTA}/{NomeDeArquivo(cenario)}{ScenarioConfig.EXTENSION}";
                if (System.IO.File.Exists(ScenarioConfig.ResolveProjectPath(arquivo)))
                    existentes.Add(System.IO.Path.GetFileName(arquivo));
            }

            if (System.IO.File.Exists(ScenarioConfig.ResolveProjectPath(ARQUIVO_SUITE)))
                existentes.Add(System.IO.Path.GetFileName(ARQUIVO_SUITE));

            if (existentes.Count == 0) return true;

            return EditorUtility.DisplayDialog("Sobrescrever arquivos?",
                $"{existentes.Count} arquivo(s) já existem e serão reescritos a partir dos " +
                "assets, perdendo os comentários que tiverem:\n\n" +
                string.Join("\n", existentes) + "\n\n" +
                "Continuar?", "Sobrescrever", "Cancelar");
        }

        /// <summary>Nome de arquivo de um cenário: o do asset quando existe, senão o nome exibido.</summary>
        static string NomeDeArquivo(ScenarioDefinition cenario)
        {
            string caminho = AssetDatabase.GetAssetPath(cenario);
            return string.IsNullOrEmpty(caminho)
                ? ScenarioRunner.SanitizeFileName(cenario.DisplayName)
                : System.IO.Path.GetFileNameWithoutExtension(caminho);
        }

        /// <summary>Retorna todos os cenários do projeto; cria os padrões se não houver nenhum.</summary>
        public static List<ScenarioDefinition> CarregarOuCriar()
        {
            var lista = new List<ScenarioDefinition>();
            var guids = AssetDatabase.FindAssets("t:ScenarioDefinition");
            foreach (var g in guids)
            {
                var cenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetDatabase.GUIDToAssetPath(g));
                if (cenario != null) lista.Add(cenario);
            }
            if (lista.Count > 0) return lista;

            GarantirPasta();
            lista.Add(CriarAlvoEstatico());
            lista.Add(CriarCruzamento());
            lista.Add(CriarRodaARoda());
            lista.Add(CriarUltrapassagem());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return lista;
        }

        // ---------------- os quatro cenários ----------------

        /// <summary>
        /// Obstáculo parado bem em cima da rota, 600 m à frente. Testa a reação mais
        /// básica: perceber e desviar de algo que não se move.
        /// </summary>
        static ScenarioDefinition CriarAlvoEstatico()
        {
            var cenario = Base("01 - Alvo estático",
                "O USV segue rumo Norte e encontra um obstáculo parado sobre a própria rota, " +
                "600 m à frente. Espera-se que perceba o obstáculo e desvie, retomando a rota depois.");

            cenario.Targets.Add(new TargetSpec
            {
                Name = "ObstaculoParado",
                StartOffsetXZ = new Vector2(0f, 600f),
                HeadingDegrees = 0f,
                SpeedKnots = 0f,
                Behaviour = TargetBehaviour.Static,
                Length = 30f,
                Beam = 12f,
                HullColor = new Color(0.55f, 0.55f, 0.58f)
            });

            return Salvar(cenario, "Cenario01_AlvoEstatico");
        }

        /// <summary>
        /// Alvo cruzando da direita (boreste) para a esquerda, em rota de colisão.
        /// Pela Regra 15 do RIPEAM, quem vê o outro por boreste deve dar passagem.
        /// </summary>
        static ScenarioDefinition CriarCruzamento()
        {
            var cenario = Base("02 - Cruzamento perpendicular",
                "Um alvo cruza da direita para a esquerda em rota de colisão. Pela Regra 15 " +
                "(RIPEAM), o USV enxerga o alvo por boreste e deve dar passagem, " +
                "manobrando por trás dele.");

            // Sai 600 m a leste e 600 m ao norte, rumo Oeste: chega ao ponto de cruzamento
            // ao mesmo tempo que o USV, que sobe 600 m rumo Norte na mesma velocidade.
            cenario.Targets.Add(new TargetSpec
            {
                Name = "AlvoCruzando",
                StartOffsetXZ = new Vector2(600f, 600f),
                HeadingDegrees = 270f,
                SpeedKnots = 12f,
                Behaviour = TargetBehaviour.StraightLine,
                Length = 60f,
                Beam = 14f,
                HullColor = new Color(0.75f, 0.35f, 0.15f)
            });

            return Salvar(cenario, "Cenario02_Cruzamento");
        }

        /// <summary>
        /// Alvo vindo de frente, na mesma linha. Pela Regra 14, ambos devem guinar
        /// para boreste e passar bombordo com bombordo.
        /// </summary>
        static ScenarioDefinition CriarRodaARoda()
        {
            var cenario = Base("03 - Roda-a-roda (head-on)",
                "Um alvo vem de frente, na mesma linha e em sentido contrário. Pela Regra 14 " +
                "(RIPEAM), o USV deve guinar para boreste e passar bombordo com bombordo.");

            cenario.Targets.Add(new TargetSpec
            {
                Name = "AlvoDeFrente",
                StartOffsetXZ = new Vector2(0f, 1200f),
                HeadingDegrees = 180f,
                SpeedKnots = 12f,
                Behaviour = TargetBehaviour.StraightLine,
                Length = 70f,
                Beam = 16f,
                HullColor = new Color(0.80f, 0.20f, 0.20f)
            });

            return Salvar(cenario, "Cenario03_RodaARoda");
        }

        /// <summary>
        /// Alvo lento à frente, no mesmo rumo. Pela Regra 13, quem ultrapassa é quem
        /// deve se manter afastado — o USV precisa contornar sem cortar a proa do outro.
        /// </summary>
        static ScenarioDefinition CriarUltrapassagem()
        {
            var cenario = Base("04 - Ultrapassagem de alvo mais lento",
                "Um alvo mais lento segue à frente, no mesmo rumo. Pela Regra 13 (RIPEAM), " +
                "o USV é o navio que alcança e deve se manter afastado enquanto ultrapassa.");

            // 400 m à frente a 5 nós, contra os 12 nós do USV: a aproximação é lenta,
            // dando tempo de a manobra de ultrapassagem se desenvolver.
            cenario.Targets.Add(new TargetSpec
            {
                Name = "AlvoLento",
                StartOffsetXZ = new Vector2(0f, 400f),
                HeadingDegrees = 0f,
                SpeedKnots = 5f,
                Behaviour = TargetBehaviour.StraightLine,
                Length = 45f,
                Beam = 12f,
                HullColor = new Color(0.30f, 0.45f, 0.70f)
            });

            return Salvar(cenario, "Cenario04_Ultrapassagem");
        }

        // ---------------- utilidades ----------------

        /// <summary>Cria um cenário com os parâmetros comuns a todos os testes.</summary>
        static ScenarioDefinition Base(string nome, string descricao)
        {
            var cenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            cenario.DisplayName = nome;
            cenario.Description = descricao;
            cenario.UsvStartXZ = PARTIDA_USV;
            cenario.UsvStartHeadingDegrees = 0f;
            cenario.UsvCruiseSpeedKnots = 12f;
            cenario.WaypointOffsetsXZ = new List<Vector2>(ROTA_NORTE);
            cenario.Targets = new List<TargetSpec>();
            cenario.MaxDurationSeconds = 200f;
            cenario.MinSafeDistanceMeters = 100f;
            return cenario;
        }

        static ScenarioDefinition Salvar(ScenarioDefinition cenario, string nomeArquivo)
        {
            AssetDatabase.CreateAsset(cenario, $"{PASTA}/{nomeArquivo}.asset");
            return cenario;
        }

        static void GarantirPasta()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Simulador"))
                AssetDatabase.CreateFolder("Assets", "Simulador");
            if (!AssetDatabase.IsValidFolder(PASTA))
                AssetDatabase.CreateFolder("Assets/Simulador", "Cenarios");
        }
    }
}
