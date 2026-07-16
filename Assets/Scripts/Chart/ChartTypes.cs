using System;
using System.Collections.Generic;
using UnityEngine;

namespace CenarioMaritimo.Chart
{
    // Classes de objeto da carta, nomeadas conforme o padrão IHO S-57 (ver relatório
    // "Levantamento de requisitos", seção 2.3): LNDARE = área de terra, DEPARE = área
    // de profundidade. BOYSHP/UWTROC aproximam boia e rochedo (objetos pontuais).
    public enum ObjClass { LNDARE, DEPARE }
    public enum PointObjClass { BOYSHP, UWTROC } // boia / rochedo submerso

    [Serializable]
    public class ChartFeature
    {
        public ObjClass objectClass;
        public List<Vector2> ringXZ = new(); // anel externo do polígono, plano local X,Z (metros)
        public List<Vector2> holeXZ; // anel interno (buraco), opcional — ex.: a faixa mais rasa/a ilha
        public float DRVAL1; // profundidade mínima (m abaixo do datum) — só DEPARE
        public float DRVAL2; // profundidade máxima (m abaixo do datum) — só DEPARE
    }

    [Serializable]
    public class ChartPointFeature
    {
        public PointObjClass objectClass;
        public Vector2 posicaoXZ; // plano local X,Z (metros) — .x = X, .y = Z
        public string nome;
    }
}
