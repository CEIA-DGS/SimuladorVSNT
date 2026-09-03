# CenárioMarítimoVSNT — Simulador do VSNT (Projeto PRISMA)

Módulo do **simulador** do **VSNT** (embarcação autônoma não tripulada / USV) do
**Projeto PRISMA** — CASNAV / Marinha do Brasil, desenvolvido na Unidade Embrapii
**CEIA-UFG**.

Um **cenário marítimo 3D** construído a partir de uma **carta náutica real** da Baía de
Guanabara, com **embarcação georreferenciada**, **tráfego dinâmico** e um **sensor de
percepção** — tudo para validar algoritmos de navegação e percepção antes de embarcar no
USV físico (VSNT/DGS-15 "Fúria da Noite").

> **Responsável pelo módulo:** Rian de Souza Santos (Squad 2)
> **Engine:** Unity 6.5 (`6000.5.3f1`) · pipeline URP

---

## 📸 O simulador em ação

Tudo abaixo roda sobre a **Baía de Guanabara real** — terreno, profundidade e linha de
costa vêm da carta náutica oficial.

### 🗺️ Cenário construído a partir da carta náutica real

![Cenário 3D gerado da carta náutica, com a costa da Baía de Guanabara ao fundo](Screenshots/01-cenario-costa.png)

O ambiente 3D é **gerado automaticamente** a partir da carta náutica eletrônica
**BR501511 "Barra do Rio de Janeiro"** (padrão IHO **S-57**, da DHN/Marinha). Um pipeline
em **Python + GDAL** lê a carta, reprojeta para UTM 23S, rasteriza a **batimetria real**
(de -2 m a 100 m de profundidade) num *heightmap* e extrai os obstáculos. O Unity monta o
terreno, a água e a costa a partir desses dados — ou seja, **o relevo submarino e a linha
de costa que você vê são os reais** da entrada da Baía de Guanabara (área ≈ 20 × 15 km).

### 🚤 A embarcação — USV DGS-15 "Fúria da Noite"

![Embarcação USV DGS-15 navegando, com HUD de posição local e geográfica](Screenshots/02-usv-dgs15.png)

A embarcação do jogador é o **USV DGS-15**, construído por código com as **medidas reais**
do barco (4,5 m de comprimento, 2,0 m de boca, mastro em "A" com o sensor no topo). Tem
casco tipo RIB (tubos infláveis), console e motor de popa — e responde a **WASD/setas**,
com flutuação pela onda e bloqueio de terra. O **HUD** (canto superior esquerdo) mostra em
tempo real a posição **local (X, Z)** e a **geográfica (lat, lon)** — porque cada ponto do
cenário está georreferenciado.

### 🧭 Carta tática georreferenciada (tecla **M**)

![Carta tática em visão geral: a Baía de Guanabara colorida por profundidade, com os marcadores das embarcações e o alcance do sensor](Screenshots/03-carta-tatica.png)

Uma **carta tática 2D ao vivo** (tecla **M** alterna os modos) mostra a situação do alto.
Na **visão geral** acima, a baía inteira aparece **colorida por profundidade** (azul =
água, verde = terra), com os marcadores:

- **Triângulo vermelho** = o USV (aponta o rumo).
- **Quadrados coloridos** = embarcações (ground truth, cor por porte).
- **Anéis ciano / amarelo** = contatos que o **sensor** está detectando.
- **Círculo grande** = alcance do sensor.

O georreferenciamento foi **validado ponto a ponto contra o GDAL (erro < 1 mm)**, e o
simulador **exporta a carta vetorizada em GeoJSON (WGS84)** — um produto pronto para
alimentar sistemas de navegação.

### 📡 Tráfego dinâmico + sensor de percepção

![USV detectando outra embarcação à frente, com o contato marcado na carta tática](Screenshots/04-trafego-sensor.png)

O cenário é povoado por **embarcações dinâmicas** (cargueiro, tanque, lancha, pesqueiro…)
que seguem **trajetórias suaves por spline**, cada uma com um **tipo** e código AIS de
plausibilidade. A bordo do USV, um **sensor** (alcance 3,5 km) faz varredura periódica:
**detecta** as embarcações no alcance, checa **oclusão** (uma ilha entre o USV e o alvo
bloqueia a visada) e faz **tracking** dos contatos. Isso separa o que "existe no mundo"
do que "o USV percebe" — a base para calcular **risco de colisão (CPA/TCPA)** e aplicar as
regras do RIPEAM.

---

## 🛰️ Da carta náutica ao ambiente — e de volta

O pipeline **fecha o ciclo**: a carta S-57 vira ambiente 3D, e o ambiente 3D é
**vetorizado de volta** numa nova carta georreferenciada — provando que o
georreferenciamento se mantém de ponta a ponta.

**1. Carta náutica original** (desenhada direto dos dados da S-57 — a referência):

![Carta náutica original extraída da S-57, com faróis, boias, naufrágios e isóbatas](Assets/CartaReal/carta_real.png)

**2. Carta vetorizada a partir do ambiente 3D** (contornos por *marching squares* sobre a
malha do terreno, reconvertidos para lat/lon e exportados em **GeoJSON WGS84**):

![Carta vetorizada gerada a partir do cenário 3D do Unity](Assets/CartaReal/carta_do_ambiente.png)

As duas **batem**: a costa e as isóbatas geradas do ambiente coincidem com a carta oficial
(a versão do ambiente é só mais "grosseira", limitada pela resolução da grade de 45 m). O
produto final (`carta_vetorizada_unity.geojson`) cai exatamente na Baía de Guanabara
(confere em [geojson.io](https://geojson.io)) e está pronto para alimentar sistemas de
navegação.

---

## 🚀 Como abrir

1. **Unity `6000.5.3f1`** (Unity 6.5), pipeline **URP** — instale exatamente essa versão pelo Unity Hub.
2. Baixe o projeto (ver [Baixando o projeto](#-baixando-o-projeto) abaixo).
3. Abra a pasta pelo Unity Hub. O Unity regenera `Library/` na primeira abertura (pode demorar alguns minutos).
4. Abra a cena `Assets/Scenes/SampleScene.unity`.

### Rodando o cenário (menus do Editor)

**Cenário Real** (Baía de Guanabara — foco do projeto):

1. `Cenário Real → 1. Construir a partir da Carta` — ambiente 3D + USV + sensor + carta tática.
2. `Cenário Real → 2. Vetorizar Carta do Ambiente 3D` — gera SVG + GeoJSON georreferenciado.
3. `Cenário Real → 3. Adicionar Tráfego (Embarcações)` — embarcações dinâmicas.
4. **Play** → clique no Game view → **WASD** move, **M** alterna a carta tática.

---

## 🧪 Bancada de testes de navegação

Os cenários de teste são declarados em **arquivos YAML**, não na cena. Isso deixa a
bateria versionável, revisável em *diff* e editável sem abrir a Unity — e é o que permite
rodar exatamente o mesmo conjunto de testes contra duas versões de um algoritmo.

```bash
Assets/Simulador/Cenarios/
├── suite_padrao.yaml              # a bateria: o que roda e em que ordem
├── Cenario01_AlvoEstatico.yaml    # obstáculo parado sobre a rota
├── Cenario02_Cruzamento.yaml      # cruzamento perpendicular  (RIPEAM, Regra 15)
├── Cenario03_RodaARoda.yaml       # encontro de frente        (RIPEAM, Regra 14)
└── Cenario04_Ultrapassagem.yaml   # alvo mais lento à frente  (RIPEAM, Regra 13)
```

**Como rodar a bateria inteira:**

1. `Cenário Real → 1. Construir a partir da Carta`
2. `Cenário Real → 5. Preparar Bancada por Arquivo (YAML)`
3. Apague o `TrafegoDinamico` da Hierarchy, se existir — o tráfego ambiente contamina a medição.
4. **Play** → os cenários rodam em sequência e o resumo sai no Console.

**Saída** (em `Assets/CartaReal/Testes/`, configurável em `output.folder`):

| Arquivo | Conteúdo |
|---|---|
| `<bateria>_<data>_execucoes.csv` | uma linha por execução: aprovado, duração, CPA mínimo, colisão |
| `<bateria>_<data>_cpa.csv` | uma linha por encontro USV × alvo, com CPA e instante |
| `<bateria>_<data>_resumo.md` | resumo legível, com as reprovações detalhadas |
| `<cenario>[_seedN].png` | mapa da execução desenhado sobre a carta |

Os números saem sempre com **ponto** decimal, independente do locale da máquina, para os
CSV abrirem direto no `pandas`/R. Para Excel em português, troque `output.csvSeparator`
para `";"`.

**Editando a bateria** — tudo em `suite_padrao.yaml`:

```yaml
scenarios:
  - file: Cenario02_Cruzamento.yaml     # cenário determinístico de um arquivo
  - name: Um teste escrito aqui mesmo   # ou declarado direto na suíte
    usv:
      startXZ: [9900, 7500]
    targets:
      - name: Alvo
        behaviour: StraightLine
        startOffsetXZ: [0, 800]
  - random:                             # estresse: uma execução por semente
      seeds: [20260813, 20260814]
      targetCount: [6, 14]
```

> A **semente** é o que torna um teste aleatório útil: anotada, ela reproduz exatamente as
> mesmas posições, rotas e velocidades. Ela aparece no CSV, no resumo e no nome do PNG.

Os cenários também existem como *assets* (`Cenário Real → Ferramentas → Criar Cenários de
Teste`), para editar no Inspector. `Ferramentas → Exportar Cenários para YAML` converte os
assets em arquivos; `Cenário Real → 4. Preparar Bancada de Testes` roda **um** cenário por vez.

---

## 📚 Documentação dos parâmetros (Doxygen)

Todo parâmetro ajustável do ambiente — Inspector, arquivos YAML e menus do Editor — está
documentado no próprio código e sai em formato web pelo Doxygen.

**Instalar (uma vez por máquina):**

```bash
winget install DimitriVanHeesch.Doxygen
```

**Gerar**, a partir da raiz do repositório:

```bash
doxygen Doxyfile
```

Abrir `docs-gerados/html/index.html` no navegador. A página inicial é um guia por
subsistema — embarcação, ambiente, percepção, ROS, bancada de testes e menus do Editor —
com as convenções de coordenadas, rumos e unidades.

A pasta `docs-gerados/` **não é versionada**: como a documentação sai do código, regenerar
é mais barato que versionar, e assim ela nunca fica defasada. A configuração fica no
`Doxyfile`, com caminhos relativos, e a página inicial em
[Docs/DOXYGEN_MAINPAGE.md](Docs/DOXYGEN_MAINPAGE.md).

> O Graphviz é opcional. Sem ele os diagramas de herança não são gerados, o que não afeta
> a referência de parâmetros. Para habilitá-los, instale o Graphviz e mude `HAVE_DOT` para
> `YES` no `Doxyfile`.

---

## 📦 Baixando o projeto (setup para o time)

> **O código é versionado no GitHub.** Todo trabalho — seu e dos demais — entra por
> aqui. O Unity Version Control é mantido apenas como registro/arquivo do projeto no
> CEIA, alimentado a partir do GitHub pelo responsável do módulo. **Não faça check-in
> direto no Unity VCS:** duas origens de mudança fazem os históricos divergirem e geram
> conflitos difíceis de resolver.

### 1. Instale o Git LFS (uma vez por máquina)

Os binários do projeto (imagens, modelos `.fbx`, o heightmap da carta) são guardados
via Git LFS. **Instale antes do primeiro clone**, senão você baixa ponteiros em vez dos
arquivos:

```bash
git lfs install
```

### 2. Clone o repositório

```bash
git clone https://github.com/CEIA-DGS/SimuladorVSNT.git
```

Se você clonou antes de instalar o LFS, rode `git lfs pull` dentro da pasta.

### 3. Configure o merge de cenas e prefabs (uma vez por máquina)

Arquivos do Unity (`.unity`, `.prefab`, `.asset`) não se resolvem bem com o merge
comum do Git. O Unity traz uma ferramenta que entende a estrutura desses arquivos —
o repositório já a declara no `.gitattributes`, mas cada máquina precisa registrá-la:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

*(Ajuste o caminho se sua instalação do Unity estiver em outro lugar.)*

### 4. Abra e gere o cenário

Abra a pasta pelo **Unity Hub** (versão `6000.5.3f1`). A primeira abertura demora:
o Unity regenera a pasta `Library/`, que não é versionada.

O terreno, a água e os obstáculos **não vêm no repositório** — são gerados a partir
da carta náutica por `Cenário Real → 1. Construir a partir da Carta`. Isso mantém a
cena versionada leve e evita conflitos: o cenário é sempre reproduzível a partir do
`heightmap.bytes` e do `metadata.json`.

### 5. Trabalhando no dia a dia

- Crie uma branch por tarefa: `git checkout -b feature/nome-da-tarefa`
- **Não commite direto na `main`** — abra Pull Request
- **Não commite a cena com o cenário gerado** (o `CenarioRealGerado` da Hierarchy).
  Antes de commitar a cena, apague esse objeto e salve.
- Avise o time quando for mexer na `SampleScene`: cenas conflitam com facilidade,
  mesmo com o merge inteligente configurado.

---

## 📁 Estrutura

```
Assets/              ← coração do projeto
├── Scripts/         ← runtime (georref, barco, sensor, carta tática…)
├── Editor/          ← builders e factories (menus do Unity)
├── Scenes/          ← cena principal
├── CartaReal/       ← dados da carta S-57 (heightmap, metadata, GeoJSON)
├── Simulador/       ← VesselTypes ("AIS") e Cenarios/ (bateria de testes em YAML)
└── ...
Packages/            ← manifest de pacotes
ProjectSettings/     ← configs do projeto
Screenshots/         ← imagens deste README
Library/ Temp/ Logs/ ← gerados pelo Unity (não versionados)
```

> 📚 A documentação técnica detalhada (handoff de contexto e pipeline da carta) é
> mantida **fora do repositório**, com o responsável do módulo.

---

## 🗺️ Estado atual

**Concluído:** cenário real (Baía de Guanabara) navegável; carta georreferenciada
(SVG + GeoJSON validada vs GDAL, erro < 1 mm); embarcações dinâmicas por spline com
tipagem AIS; carta tática com visão geral; sensor com detecção / oclusão / tracking.

---

## 🚧 Roadmap / próximos passos

**🎯 Percepção & decisão**
- **CPA/TCPA** — usar os contatos do sensor para calcular ponto e tempo de aproximação máxima (risco de colisão).
- **RIPEAM / COLREGS** — motor de decisão (cruzamento, ultrapassagem, vante) com sugestão e execução de manobra.
- **Tracking robusto** — filtro de Kalman, associação de dados e IDs persistentes para os contatos.
- **Modelo de sensor realista** — ruído/incerteza na medição, falsos positivos/negativos, curva de detecção por distância e condição de mar; explorar múltiplos sensores (radar, câmera / visão computacional, LiDAR).

**📡 AIS mais realista**
- Migrar do **rótulo de plausibilidade** atual para **dados AIS reais** (MMSI, tipo, dimensões, SOG/COG, status de navegação).
- **Replay de tráfego AIS histórico** real da Baía de Guanabara (navios reais no cenário real).
- **Fusão sensor + AIS** — o sensor detecta, o AIS identifica; simular perda de sinal e ruído.

**🌊 Física & ambiente**
- Trocar o **movimento cinemático** por **física real** (Rigidbody + forças hidrodinâmicas: empuxo por volume submerso, arrasto, momento de guinada).
- **Resposta do casco às ondas** (pitch / roll / heave) no lugar da flutuação simples; **correnteza e vento** afetando a deriva.
- **Ondas Gerstner / FFT** e condições ambientais: maré, estado de mar, dia/noite, neblina/chuva (afetando o sensor).

**🗺️ Novos cenários (outras cartas)**
- Rodar o mesmo pipeline com **outras cartas S-57**, em **cenas separadas** (ver tabela abaixo).

**🏗️ Arquitetura & desempenho**
- **Object pooling / GPU instancing / LOD** para escalar o número de embarcações sem perder FPS.
- **Integração ROS2** (via ROS-TCP) — publicar a percepção e consumir comandos de navegação.

### 🌎 Cartas disponíveis para novos cenários

O acervo do projeto **já tem 15 cartas S-57 da DHN**. O mesmo pipeline
(**S-57 → ambiente 3D → carta georreferenciada**) roda com qualquer uma — basta gerar cada
cenário em **cena separada**. Por região:

| Região | Cartas (BR) | UF | Interesse de teste |
|---|---|---|---|
| **Baía de Guanabara** | `501511` ✅ · `501512` · `401506` | RJ | **atual** — entrada movimentada, canal, Ponte Rio–Niterói |
| **Cabo Frio / Arraial do Cabo** | `401508` · `501503` | RJ | costa aberta, ilhas e reserva marinha |
| **Baía de Santos** | `401711` · `501701` | SP | maior porto do país, canal estreito e tráfego denso — ótimo p/ **RIPEAM** |
| **Baía de Paranaguá** | `401820` · `501821` · `501822` | PR | Canal da Galheta, manguezais e muitas ilhas — testar **oclusão** do sensor |
| **Baía de Vitória / Vila Velha** | `401410` · `501401` · `601401` | ES | canal portuário sinuoso, terminais |
| **Rio Grande / Lagoa dos Patos** | `402110` · `502101` | RS | canal longo e águas rasas — navegação restrita |

> As cartas (`.000`, padrão S-57) ficam em `Vetoriais S-57/`, uma pasta por célula
> (`BR<número>`). Fonte: **DHN / Centro de Hidrografia da Marinha (CHM)**. Localização de
> cada carta **verificada pelo extent geográfico via GDAL** (não pelo nome). Cada região
> tem cartas em escalas diferentes (aproximação 1:45.000 → porto 1:12.000), permitindo
> começar largo e depois detalhar.

---

## ⚙️ Ambiente & convenções

- **Unity 6.5** (`6000.5.3f1`) / **URP 17.5.0** / Input System novo / Shader Graph.
- ⚠️ O projeto de referência é **HDRP** — materiais/água/shaders **não portam**
  para este projeto (URP). Só modelos `.fbx` e scripts são reaproveitáveis.
phi