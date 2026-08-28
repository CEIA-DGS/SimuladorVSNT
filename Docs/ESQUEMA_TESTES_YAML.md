# Esquema YAML dos testes de navegação — Simulador VSNT

Referência do formato de configuração usado pela bancada de testes de navegação do
simulador do VSNT. A seção final registra as divergências em relação ao formato proposto
para os testes de aceite, como base de um eventual alinhamento entre os dois.

**Implementação:** parser próprio em C# (`Assets/Scripts/Testing/YamlLite.cs`), sem
dependência externa, rodando dentro da Unity. O mapeamento do esquema para os objetos do
simulador fica separado do parser, em `ScenarioConfig.cs` e `TestSuiteConfig.cs`.

---

## 1. Visão geral

São **dois tipos de arquivo**, com papéis distintos:

| Tipo | Responde | Muda com que frequência |
|---|---|---|
| **Cenário** | *O que é este teste?* | raramente |
| **Suíte** | *O que deve ser executado nesta rodada?* | toda hora |

```
Assets/Simulador/Cenarios/
├── suite_padrao.yaml              ← a suíte: lista o que roda
├── Cenario01_AlvoEstatico.yaml    ← obstáculo parado sobre a rota
├── Cenario02_Cruzamento.yaml      ← cruzamento por boreste  (RIPEAM, Regra 15)
├── Cenario03_RodaARoda.yaml       ← encontro de frente      (RIPEAM, Regra 14)
└── Cenario04_Ultrapassagem.yaml   ← alvo mais lento à frente (RIPEAM, Regra 13)
```

Um Play executa a suíte inteira em sequência e grava as métricas em CSV.

### Convenções de coordenadas

- **Eixos:** `X` = Leste, `Z` = Norte, em **metros**, no referencial local da cena.
  A cena é georreferenciada (UTM), e a conversão para lat/lon é feita em runtime por
  `GeoReferenceUTM`. O arquivo de teste **não** usa lat/lon — ver seção 6.
- **Rumos:** graus, `0` = Norte, `90` = Leste, `180` = Sul, `270` = Oeste.
- **Posições relativas:** as posições dos alvos e os waypoints são **deslocamentos em
  relação à partida do USV**, não posições absolutas. Isso permite mover o cenário
  inteiro para outro ponto da carta mudando apenas `usv.startXZ`.
- **Pares:** escritos em linha, `[x, z]`.

---

## 2. Arquivo de cenário

Descreve **uma** situação inicial. Todo campo é opcional: ausente, usa o valor padrão.
O conteúdo pode ficar na raiz do arquivo ou aninhado sob a chave `scenario:`.

### Exemplo completo

```yaml
scenario:
  name: 02 - Cruzamento perpendicular
  description: >-
    Um alvo cruza da direita para a esquerda em rota de colisão. Pela Regra 15
    (RIPEAM), o USV enxerga o alvo por boreste e deve dar passagem.

  usv:
    startXZ: [9900, 7500]
    startHeadingDegrees: 0
    cruiseSpeedKnots: 12
    publishWaypoints: true
    waypointOffsetsXZ:
      - [0, 0]
      - [0, 1500]

  criteria:
    maxDurationSeconds: 180
    minSafeDistanceMeters: 100

  targets:
    - name: AlvoCruzando
      behaviour: StraightLine
      startOffsetXZ: [600, 600]
      headingDegrees: 270
      speedKnots: 12
      lengthMeters: 60
      beamMeters: 14
      hullColor: "#BF5926"
```

### Raiz

| Campo | Tipo | Padrão | Descrição |
|---|---|---|---|
| `name` | texto | nome do arquivo | Nome do cenário. Aparece no relatório e nomeia os arquivos de saída. |
| `description` | texto | `""` | O que o cenário testa e que reação se espera. Aceita bloco `>-`. |

> Alias aceito: `displayName` para `name`.

### `usv`

| Campo | Tipo | Unidade | Padrão | Descrição |
|---|---|---|---|---|
| `startXZ` | `[x, z]` | m | `[9900, 7500]` | Partida do USV, em coordenadas locais absolutas da cena. **Única posição absoluta do arquivo** — todo o resto é relativo a ela. |
| `startHeadingDegrees` | número | grau | `0` | Rumo inicial. |
| `cruiseSpeedKnots` | número | nó | `12` | Velocidade de cruzeiro desejada. |
| `publishWaypoints` | booleano | — | `true` | Se a própria simulação publica a rota. `false` quando os waypoints vêm de fora (ROS). |
| `waypointOffsetsXZ` | lista de `[x, z]` | m | `[[0,0], [0,1000]]` | Rota do USV, **relativa** à partida. Mínimo de 2 pontos. |

> Alias aceito: `waypoints` para `waypointOffsetsXZ`.

### `criteria`

| Campo | Tipo | Unidade | Padrão | Descrição |
|---|---|---|---|---|
| `maxDurationSeconds` | número | s | `180` | Tempo máximo de simulação. Atingido o limite, a execução encerra. |
| `minSafeDistanceMeters` | número | m | `100` | Distância mínima de segurança. Aproximar-se mais que isso de um alvo **reprova** a execução. |

### `targets` — lista de alvos

| Campo | Tipo | Unidade | Padrão | Descrição |
|---|---|---|---|---|
| `name` | texto | — | `Target` | Identifica o alvo no relatório e no CSV. |
| `behaviour` | enum | — | `StraightLine` | `Static`, `StraightLine` ou `Route`. Ver abaixo. |
| `startOffsetXZ` | `[x, z]` | m | `[0, 500]` | Posição inicial, **relativa** à partida do USV. |
| `headingDegrees` | número | grau | `180` | Rumo inicial. |
| `speedKnots` | número | nó | `10` | Velocidade. Ignorado quando `behaviour: Static`. |
| `lengthMeters` | número | m | `40` | Comprimento do casco. Define a distância de contato. |
| `beamMeters` | número | m | `10` | Boca. |
| `hullColor` | `#RRGGBB` | — | vermelho fosco (RGB `0.70, 0.25, 0.20`) | Cor do casco, só para distinguir visualmente. Precisa de aspas, senão o `#` vira comentário. |
| `loopRoute` | booleano | — | `true` | Repete a rota ao chegar ao fim. Só para `Route`. |
| `routeOffsetsXZ` | lista de `[x, z]` | m | `[]` | Rota própria do alvo, **relativa** à partida do USV. Só para `Route`. |

> Aliases aceitos: `length` / `beam` / `route`.

**Valores de `behaviour`:**

| Valor | Comportamento |
|---|---|
| `Static` | Nunca se move. Obstáculo fixo: rocha, boia, embarcação fundeada. |
| `StraightLine` | Segue em linha reta no rumo e velocidade declarados, indefinidamente. |
| `Route` | Percorre `routeOffsetsXZ`, opcionalmente em laço. |

---

## 3. Arquivo de suíte

Não descreve cenário nenhum — define as condições comuns e **lista o que executar**.

```yaml
suite: Bateria padrão RIPEAM
description: Os quatro encontros clássicos mais uma varredura de estresse.

environment:
  waterHeight: 0.05
  fixedTimeStep: 0.02
  randomSeed: 12345

output:
  folder: Assets/CartaReal/Testes
  exportMaps: true
  exportResults: true
  csvSeparator: ","

scenarios:
  - file: Cenario01_AlvoEstatico.yaml
  - file: Cenario02_Cruzamento.yaml
  - file: Cenario03_RodaARoda.yaml
  - file: Cenario04_Ultrapassagem.yaml
  - random:
      seeds: [20260813, 20260814, 20260815]
      targetCount: [6, 14]
```

### Raiz

| Campo | Tipo | Padrão | Descrição |
|---|---|---|---|
| `suite` | texto | nome do arquivo | Nome da bateria. Vira prefixo dos arquivos de saída. |
| `description` | texto | `""` | O que a bateria mede. |
| `scenarios` | lista | **obrigatório** | O que executar, na ordem. |

> Aliases aceitos: `name` para `suite`; `cenarios` para `scenarios`.

### `environment`

As condições **compartilhadas por todas as execuções**. É o que torna os números
comparáveis entre si: se duas execuções rodassem com passos de tempo diferentes, os KPIs
não significariam nada em conjunto.

| Campo | Tipo | Unidade | Padrão | Descrição |
|---|---|---|---|---|
| `waterHeight` | número | m | `0.05` | Altura `Y` da malha de água onde o USV e os alvos são posicionados. |
| `fixedTimeStep` | número | s | `0.02` | Passo fixo de simulação. `0.02` = 50 Hz. |
| `randomSeed` | inteiro | — | `12345` | Semente do gerador aleatório global, para que qualquer ruído incidental (ex.: modelos de sensor) se repita igual entre execuções. |
| `timeScale` | número | ×  | `1` | Quantas vezes mais rápido que o tempo real a bateria roda. **Não altera resultado algum:** muda o tempo simulado por segundo de relógio, não o tamanho do passo de física. |

> `timeScale` só é seguro porque **tudo que afeta medição roda no passo fixo** — dinâmica,
> controlador, guiagem, waypoints, alvos, sensor e a própria bancada. Mover qualquer um
> desses para um `Update` por quadro quebraria a garantia. Valores altos demais para a
> taxa de quadros simplesmente não são alcançados; nunca produzem resultado errado.

### `output`

| Campo | Tipo | Padrão | Descrição |
|---|---|---|---|
| `folder` | texto | `Assets/CartaReal/Testes` | Pasta de saída, relativa à raiz do projeto. |
| `exportMaps` | booleano | `true` | Gera um PNG por execução, com as trajetórias desenhadas sobre a carta. |
| `exportResults` | booleano | `true` | Gera os CSV e o resumo. |
| `csvSeparator` | texto | `","` | Separador de coluna. Números saem **sempre com ponto decimal**, independente do locale da máquina. |

### `scenarios` — as três formas de entrada

**1. Referência a arquivo externo.** Caminho procurado primeiro ao lado da suíte, depois
a partir da raiz do projeto.

```yaml
- file: Cenario02_Cruzamento.yaml
```

**2. Cenário escrito na própria suíte.** Mesmo esquema da seção 2, sem o `scenario:`.

```yaml
- name: Teste pontual
  usv:
    startXZ: [9900, 7500]
  targets:
    - name: Alvo
      behaviour: StraightLine
      startOffsetXZ: [0, 800]
```

**3. Entrada aleatória — uma execução por semente.**

```yaml
- random:
    seeds: [20260813, 20260814, 20260815]
```

| Campo | Tipo | Unidade | Padrão | Descrição |
|---|---|---|---|---|
| `seeds` | lista de inteiros | — | — | **Uma execução por semente.** A mesma semente reproduz exatamente as mesmas posições, rotas, velocidades e tamanhos. |
| `seed` | inteiro | — | `20260813` | Alternativa a `seeds`, para uma execução só. |
| `usvStartXZ` | `[x, z]` | m | `[9900, 7500]` | Partida do USV no cenário gerado. |
| `areaCenterXZ` | `[x, z]` | m | `[0, 0]` | Centro da área de geração. Em zero, usa a partida do USV. |
| `areaRadius` | número | m | `2000` | Raio da área onde os alvos podem nascer. |
| `minDistanceFromUsv` | número | m | `300` | Distância mínima do USV para um alvo nascer. Evita começar já em colisão. |
| `targetCount` | `[mín, máx]` | — | `[6, 14]` | Quantidade de alvos sorteada nessa faixa. |
| `speedRangeKnots` | `[mín, máx]` | nó | `[4, 16]` | Faixa de velocidade dos alvos. |
| `lengthRangeMeters` | `[mín, máx]` | m | `[15, 120]` | Faixa de comprimento. A boca sai como fração do comprimento. |
| `staticTargetRatio` | número `0..1` | — | `0.15` | Proporção de alvos parados. |
| `routePoints` | `[mín, máx]` | — | `[2, 4]` | Quantidade de pontos da rota de cada alvo. |
| `legLengthMeters` | `[mín, máx]` | m | `[400, 1500]` | Comprimento de cada perna da rota. |
| `publishUsvWaypoints` | booleano | — | `true` | Gera também uma rota para o USV. Desligado, o USV fica parado e o teste não mede nada. |
| `maxDurationSeconds` | número | s | `300` | Duração máxima da execução gerada. |
| `minSafeDistanceMeters` | número | m | `100` | Critério de aprovação. |
| `landHeightThreshold` | número | m | `0.3` | Acima desta altura de terreno considera-se terra firme. Nenhum alvo nasce em terra. |
| `waterSearchAttempts` | inteiro | — | `30` | Tentativas de sorteio por ponto antes de desistir de achar água. |

> Alias aceito: `sementes` para `seeds`.

---

## 4. Subconjunto de YAML suportado

O parser aceita um subconjunto deliberado. Isso importa para interoperabilidade: uma
biblioteca completa (yaml-cpp, por exemplo) aceita construções que o parser daqui
rejeita. **Um arquivo que use recurso fora desta lista funciona de um lado e quebra do
outro.**

**Aceito:**

- mapas e listas em bloco (indentação por espaços);
- listas em linha: `[9900, 7500]`, inclusive aninhadas;
- escalares simples, entre aspas duplas (`\"` e `\\` escapados) ou simples (`''`);
- blocos de texto multilinha: `>` e `|`, com sufixos `-` e `+`;
- comentários com `#`, incluindo no fim da linha;
- marcadores `---` e `...`.

**Rejeitado, com número de linha no erro:**

- mapas em linha: `{a: 1}`;
- âncoras, aliases e tags: `&x`, `*x`, `!!tipo`;
- TAB na indentação.

**Comportamento na leitura:** tolerante com ausência, rigoroso com erro. Campo ausente usa
o padrão — o arquivo só precisa declarar o que difere. Campo presente e malformado
**interrompe a execução** com a linha, em vez de virar zero silenciosamente:

```
Linha 34: 'abc' não é um número.
```

Numa bancada de testes, rodar um teste diferente do que está escrito no arquivo é o pior
desfecho possível.

---

## 5. Saída — as métricas

Ao fim da bateria são gravados, na pasta de `output.folder`:

| Arquivo | Conteúdo |
|---|---|
| `<suíte>_<data>_execucoes.csv` | uma linha por execução |
| `<suíte>_<data>_cpa.csv` | uma linha por encontro USV × alvo |
| `<suíte>_<data>_resumo.md` | resumo legível, com as reprovações detalhadas |
| `<cenário>[_seedN].png` | mapa da execução desenhado sobre a carta |

Dois níveis porque a métrica principal — o **CPA observado** (menor distância realmente
atingida) — é **por alvo**, não por execução.

**`_execucoes.csv`:**

```
indice, cenario, origem, semente, aprovado, colisao, colidiu_com, rota_concluida,
duracao_s, cpa_min_m, cpa_min_alvo, cpa_min_t_s, distancia_seguranca_m,
alvos, violacoes, mapa
```

**`_cpa.csv`:**

```
indice, cenario, semente, alvo, cpa_m, t_cpa_s, distancia_contato_m, violou_seguranca
```

Booleanos saem como `1`/`0`, para agregarem direto como média. Números com **ponto**
decimal e `UTF-8 BOM`, independente do locale — em máquina configurada em pt-BR o padrão
do sistema sairia `0,05`, colidindo com o separador de coluna e corrompendo o arquivo sem
lançar erro nenhum.

---

## 6. Divergências em relação ao formato dos testes de aceite

Comparação com o formato proposto para os testes de aceite, cuja implementação é um
binário em C++ com parsing por reflect-cpp.

A ferramenta de parsing é detalhe de implementação de cada lado e não precisa ser comum.
O **esquema**, sim: sem um acordo sobre nomes, unidades e estrutura, os arquivos deixam
de ser intercambiáveis entre as duas implementações.

| Assunto | Testes de aceite | Bancada do simulador | Observação |
|---|---|---|---|
| **Nomenclatura** | `snake_case` | `camelCase` | Diferença estética, mas precisa ser uniforme. |
| **Velocidade** | `speed_mps` (m/s) | `speedKnots` (nó) | Nó é a unidade da carta náutica e do RIPEAM; m/s é a do modelo dinâmico. Convém adotar uma e converter na borda de cada implementação. |
| **Posição** | lat/lon absoluta | deslocamento em metros relativo à partida | Absoluta prende o cenário a um ponto do mapa; relativa permite reaproveitar a mesma geometria em qualquer carta. Uma âncora absoluta com deslocamentos relativos atende aos dois casos. |
| **Comportamento** | `behavior` + `behavior_parameters.<nome>` | `behaviour` (enum) + campos irmãos | A primeira forma repete o nome do comportamento como chave interna, e é extensível a comportamentos arbitrários; a segunda é mais enxuta, mas limitada aos valores do enum. Uma *tagged union* atende aos dois objetivos. |
| **KPI** | `kpis.minimum_distance: true` | `criteria.minSafeDistanceMeters: 100` | Na primeira forma o KPI é apenas coletado; na segunda ele também serve de critério, e a execução se aprova ou reprova sem intervenção. |
| **Identificação** | `metadata.id: CA-AT-001` | apenas `name` | Um identificador estável sobrevive à renomeação do cenário e serve de chave nos CSV. Ausente na bancada; recomenda-se adotar. |
| **Reprodutibilidade** | não previsto | `seeds: [...]` + `fixedTimeStep` | Sem semente e sem passo de tempo declarados no arquivo, duas execuções deixam de ser comparáveis sem que isso apareça em lugar nenhum. |
| **Granularidade da saída** | série temporal dos KPIs | agregado por execução e por encontro | O agregado permite ranquear execuções; a série temporal permite entender por que uma falhou. Os dois níveis são complementares. |
| **Organização** | um arquivo por caso de teste | idem, mais um arquivo de suíte listando o que executa | Estrutura equivalente, definida de forma independente nas duas propostas. |

### Encaminhamento sugerido

1. Consolidar o esquema num documento único; este arquivo pode servir de ponto de partida.
2. Acordar o subconjunto de YAML da seção 4, para que as duas implementações leiam os
   mesmos arquivos.
3. Adotar um identificador estável por cenário, com convenção de nomes definida.
4. Definir unidades e nomenclatura, convertendo na borda de cada implementação.
5. Incluir semente e passo de tempo no arquivo, como pré-requisito de comparabilidade.
