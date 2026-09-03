# Simulador VSNT — parâmetros do ambiente

Referência dos parâmetros ajustáveis do ambiente de simulação do VSNT (embarcação
autônoma não tripulada), do Projeto PRISMA — CASNAV / Marinha do Brasil, desenvolvido na
Unidade Embrapii CEIA-UFG.

Esta documentação é gerada a partir dos comentários do próprio código, então acompanha o
que está implementado. Cada parâmetro listado nas páginas de classe pode ser ajustado sem
recompilar nada.

---

## Onde os parâmetros ficam

Há **três lugares** distintos onde o comportamento da simulação é configurado. Saber qual
é qual evita procurar no lugar errado.

| Onde | O que se ajusta | Como |
|---|---|---|
| **Inspector da Unity** | quase tudo: dinâmica, sensores, câmera, tráfego, água | selecionar o objeto na cena e editar o campo |
| **Arquivos YAML** | os cenários de teste de navegação e a bateria que os executa | editar o texto em `Assets/Simulador/Cenarios/` |
| **Menus do Editor** | construção do cenário a partir da carta náutica, tráfego, bancada | menu `Cenário Real` na barra superior |

Os campos que aparecem no Inspector são os campos públicos das classes documentadas
aqui. O texto de ajuda que a Unity mostra ao passar o mouse é o mesmo descrito na página
da classe.

Para os arquivos YAML, a referência completa do formato — todos os campos, unidades,
obrigatoriedade e valores padrão — está em `Docs/ESQUEMA_TESTES_YAML.md`, fora desta
documentação gerada.

---

## Mapa dos subsistemas

### Embarcação e movimento

O USV pode ser conduzido de dois modos, que não convivem: manual, para inspeção visual,
e autônomo, para os testes.

| Classe | Papel | Ajustes típicos |
|---|---|---|
| `UsvDynamics` | modelo hidrodinâmico 3-DOF (avanço, deriva, guinada) | massa, amortecimento, inércia |
| `UsvController` | converte rumo e velocidade desejados em forças e momento | ganhos do controlador |
| `LosGuidance` | guiagem *line-of-sight*: calcula rumo para o próximo waypoint | distância de antecipação, velocidade de cruzeiro |
| `WaypointManager` | mantém a rota ativa e decide quando trocar de trecho | folga da transição entre trechos |
| `UsvManualController` | condução manual por teclado, para inspeção | — |
| `BoatController` | condução alternativa com flutuação por onda | velocidade, resposta do leme, resposta à onda |

> A condução manual precisa estar desligada durante um teste: entrada de teclado torna a
> execução não reprodutível. A bancada desliga automaticamente.

### Tráfego e alvos

| Classe | Papel | Ajustes típicos |
|---|---|---|
| `DynamicVessel` | embarcação que percorre uma rota em velocidade constante | rota, velocidade, dimensões do casco |
| `VesselType` | tipo de embarcação no padrão AIS, como recurso reutilizável | dimensões, categoria AIS, cor |
| `AisBroadcaster` | faz uma embarcação emitir sua identificação AIS | MMSI, período de emissão |

### Ambiente

| Classe | Papel | Ajustes típicos |
|---|---|---|
| `GeoReferenceUTM` | converte entre coordenadas da cena e latitude/longitude | origem UTM, zona, escala |
| `GeoReferenceOrigin` | ponto de origem geográfica da cena | latitude e longitude de referência |
| `TacticalChart` | carta tática sobreposta, alternada pela tecla **M** | tamanho, opacidade, imagem da carta |
| `WaterAnimator` | animação visual da superfície da água | amplitude, comprimento e velocidade da onda |
| `WaveUtil` | função de altura de onda compartilhada pela cena | — |

### Percepção

Duas gerações de sensores convivem. `VesselSensor` é a varredura de contatos usada pela
carta tática; o pacote `Sensors` é a pilha mais recente, com modelos de ruído e publicação
em ROS.

| Classe | Papel | Ajustes típicos |
|---|---|---|
| `VesselSensor` | detecção de embarcações com alcance, campo de visão e oclusão | alcance, abertura, período de varredura |
| `Contact` | um contato detectado, com posição e instante da detecção | — |
| `GpsSensor`, `ImuSensor`, `RadarSensor`, `CameraSensor`, `AisSensor` | sensores simulados | período de amostragem, alcance, resolução |
| `GaussianNoiseGps`, `GaussianNoiseImu`, `GaussianNoiseRadar` | modelos de ruído aplicados às leituras | desvio padrão, viés |
| `BaseSensor`, `BasePublisher` | base comum de amostragem e publicação | frequência |

> Os modelos de ruído sorteiam números do gerador aleatório global da Unity. A bancada de
> testes fixa a semente desse gerador no início de cada execução, para que o ruído se
> repita igual e o resultado continue reprodutível.

### Integração ROS

O projeto compila com o símbolo `ROS2` definido. Publicadores e assinantes seguem os tipos
do ROS 2, não do ROS 1.

| Classe | Publica ou assina |
|---|---|
| `OdomPublisher`, `TrueStatePublisher` | estado e odometria do USV |
| `Lidar2DPublisher`, `PointCloudPublisher` | varredura laser e nuvem de pontos |
| `CameraImagePublisher`, `RosCameraPublisher` | imagem da câmera |
| `RosGpsPublisher`, `RosImuPublisher`, `RosRadarPublisher`, `RosAisPublisher` | leituras dos sensores simulados |
| `ClockPublisher` | relógio da simulação |
| `RosWaypointSubscriber` | recebe waypoints externos e os entrega ao `WaypointManager` |

> Não é necessário ter ROS instalado para usar o simulador. Sem um servidor ativo, os
> publicadores apenas registram falha de conexão no console.

### Bancada de testes de navegação

Os cenários de teste são declarados em arquivos YAML, não no Inspector. As classes abaixo
são o mecanismo; os parâmetros ficam nos arquivos.

| Classe | Papel |
|---|---|
| `ScenarioDefinition` | um cenário: pose inicial do USV, rota e alvos |
| `TestSuiteConfig` | uma bateria: o que executa, em que ordem e sob que condições |
| `ScenarioRunner` | executa um cenário e mede o resultado |
| `ScenarioSuiteRunner` | executa a bateria inteira em sequência |
| `RandomScenarioGenerator` | gera cenários de estresse a partir de uma semente |
| `ScenarioMetrics` | mede o CPA observado, violações de segurança e contato |
| `ScenarioResultsExporter` | grava as métricas em CSV e o resumo em Markdown |
| `ScenarioMapExporter` | desenha o mapa da execução sobre a carta |
| `ScenarioConfig`, `YamlLite` | leitura dos arquivos de configuração |

### Construção do cenário (menus do Editor)

Executados pelo menu `Cenário Real`, não durante o jogo.

| Classe | Menu |
|---|---|
| `CenarioRealBuilder` | 1. Construir a partir da Carta |
| `CartaVetorizadaExporter` | 2. Vetorizar Carta do Ambiente 3D |
| `TrafegoBuilder` | 3. Adicionar Tráfego (Embarcações) |
| `ScenarioSetup` | 4 e 5. Preparar Bancada de Testes |
| `EmbarcacaoFactory`, `EmbarcacaoObstaculoFactory` | criação de embarcações e obstáculos |
| `VesselTypeSetup` | criação dos tipos de embarcação AIS |
| `ChartExporter` | exportação da carta |

---

## Convenções

**Coordenadas.** `X` aponta para Leste, `Z` para Norte, em metros, no referencial local da
cena. `Y` é a vertical. A cena é georreferenciada em UTM; a conversão para latitude e
longitude é feita em tempo de execução.

**Rumos.** Em graus, com `0` no Norte e crescendo no sentido horário: `90` é Leste, `180`
Sul, `270` Oeste.

**Velocidades.** Declaradas em **nós** nos parâmetros voltados ao usuário, por ser a
unidade da carta náutica e do RIPEAM. Internamente são convertidas para metros por
segundo, que é a unidade do modelo dinâmico.

**Passo de tempo.** Tudo que afeta medição roda no passo fixo de física, nunca por quadro.
É o que garante que a mesma execução produza sempre o mesmo resultado, independentemente
da taxa de quadros da máquina.

---

## Como regenerar esta documentação

Requer o Doxygen instalado. A partir da raiz do repositório:

```
doxygen Doxyfile
```

A saída é gravada em `docs-gerados/html/`; abrir `index.html` no navegador. A pasta não é
versionada — cada regeneração parte do código atual, de modo que a documentação nunca
fica defasada em relação ao que está implementado.
