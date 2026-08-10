@mainpage Documentação do Meu Projeto Unity

# CenárioMarítimoVSNT — Simulador do VSNT (Projeto PRISMA)

Módulo do **simulador** do **VSNT** (embarcação autônoma não tripulada / USV) do
**Projeto PRISMA** — CASNAV / Marinha do Brasil, desenvolvido na Unidade Embrapii
**CEIA-UFG**.

Cenário marítimo 3D + **carta náutica georreferenciada** + **embarcações dinâmicas** +
**sensor de percepção**, para validar algoritmos de navegação e percepção antes de
embarcar no USV físico (VSNT/DGS-15 "Fúria da Noite").

> **Responsável pelo módulo:** Rian de Souza Santos (Squad 2)

---

## 🚀 Como abrir

1. **Unity `6000.5.3f1`** (Unity 6.5), pipeline **URP** — instale exatamente essa versão pelo Unity Hub.
2. Baixe o projeto do **Unity Version Control** (ver [Baixando o projeto](#-baixando-o-projeto) abaixo).
3. Abra a pasta pelo Unity Hub. O Unity regenera `Library/` na primeira abertura (pode demorar alguns minutos).
4. Abra a cena `Assets/Scenes/SampleScene.unity`.

### Rodando o cenário (menus do Editor)

**Cenário Real** (Baía de Guanabara — foco atual):

1. `Cenário Real → 1. Construir a partir da Carta` — ambiente 3D + USV + sensor + carta tática.
2. `Cenário Real → 2. Vetorizar Carta do Ambiente 3D` — gera SVG + GeoJSON georreferenciado.
3. `Cenário Real → 3. Adicionar Tráfego (Embarcações)` — embarcações dinâmicas.
4. **Play** → clique no Game view → **WASD** move, **M** alterna a carta tática.

---

## 📦 Baixando o projeto

Este projeto usa o **Unity Version Control** (org **DGS-Ceia**, repositório
`CenarioMaritimoVSNT`). Para pegar o projeto:

1. Peça acesso à org **DGS-Ceia** (Ryan / responsável do módulo adiciona você em Membros).
2. No **Unity Hub** ou no **Editor** → **Window → Unity Version Control**, faça login.
3. Crie um workspace apontando para o repositório `CenarioMaritimoVSNT`.
4. Faça o **update** para baixar os arquivos.

O Unity VCS já ignora as pastas geradas (`Library/`, `Temp/`, `Logs/`) automaticamente.

---

## 📁 Estrutura

```
Assets/              ← coração do projeto (é o que importa)
├── Scripts/         ← runtime (georef, barco, sensor, carta tática…)
├── Editor/          ← builders e factories (menus do Unity)
├── Scenes/          ← cena principal
├── CartaReal/       ← dados da carta S-57 (heightmap, metadata, GeoJSON)
├── Simulador/       ← VesselTypes (ScriptableObjects, "AIS")
└── ...
Packages/            ← manifest de pacotes
ProjectSettings/     ← configs do projeto
Library/ Temp/ Logs/ ← gerados pelo Unity (não são versionados)
```

> 📚 A documentação técnica detalhada (handoff de contexto e pipeline da carta) é
> mantida **fora do repositório**, com o responsável do módulo — não está versionada
> aqui por conter detalhes internos do projeto.

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
- ⚠️ Unity 6.5 deprecou `GetInstanceID()` (erro) e `FindObjectsSortMode` (aviso) —
  usar `FindObjectsByType<T>()` e chavear dicionários por referência de objeto.
- ⚠️ O projeto de referência do colega é **HDRP** — materiais/água/shaders **não portam**
  para este projeto (URP). Só modelos `.fbx` e scripts são reaproveitáveis.

---

## 🔒 Aviso

Projeto vinculado à **Marinha do Brasil (CASNAV / PRISMA)**. Repositório **privado** —
não tornar público nem redistribuir sem autorização da chefia do projeto.
