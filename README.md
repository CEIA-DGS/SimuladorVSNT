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

## 📦 Baixando o projeto

O projeto vive em **dois repositórios** (o time trabalha no Unity VCS; o GitHub é a
"vitrine" com este README):

- **Unity Version Control** (org **DGS-Ceia**, repo `SimuladorVSNT`) — fonte principal.
  No Unity: **Window → Unity Version Control**, login, workspace apontando para o repo, e **update**.
- **GitHub** (`CEIA-DGS/SimuladorVSNT`, privado) — usa **Git LFS** para os binários.
  Rode `git lfs install` uma vez, depois `git clone`.

O Unity ignora as pastas geradas (`Library/`, `Temp/`, `Logs/`) automaticamente.

---

## 📁 Estrutura

```
Assets/              ← coração do projeto
├── Scripts/         ← runtime (georref, barco, sensor, carta tática…)
├── Editor/          ← builders e factories (menus do Unity)
├── Scenes/          ← cena principal
├── CartaReal/       ← dados da carta S-57 (heightmap, metadata, GeoJSON)
├── Simulador/       ← VesselTypes (ScriptableObjects, "AIS")
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

**Próximos passos:** CPA/TCPA (risco de colisão), regras RIPEAM, replay de AIS
histórico real, testes de desempenho, integração com o time.

---

## ⚙️ Ambiente & convenções

- **Unity 6.5** (`6000.5.3f1`) / **URP 17.5.0** / Input System novo / Shader Graph.
- ⚠️ O projeto de referência é **HDRP** — materiais/água/shaders **não portam**
  para este projeto (URP). Só modelos `.fbx` e scripts são reaproveitáveis.
