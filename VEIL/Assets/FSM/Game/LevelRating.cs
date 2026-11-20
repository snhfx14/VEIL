using UnityEngine;

namespace AulaFSM
{
    /// <summary>
    /// Classificação do nível. Concluir só exige eliminar todos os alvos (como antes);
    /// as estrelas e a nota premiam o jogo em stealth:
    ///   ★ concluído   ★ todos os diamantes   ★ nunca detetado
    ///   Nota S = 3 estrelas e todas as eliminações silenciosas.
    /// </summary>
    public struct LevelRating
    {
        public int Stars;          // 1..3
        public string Rank;        // S, A, B, C
        public int Score;
        public bool AllGems, Undetected, AllStealth;

        // Pontuação (valores num só sítio)
        private const int PointsCompletion = 500;
        private const int PointsPerStealthKill = 150;
        private const int PointsPerKill = 50;
        private const int PointsPerGem = 100;
        private const int PointsAllGems = 300;
        private const int PointsUndetected = 500;
        private const int PenaltyPerDetection = 100;

        public static LevelRating Evaluate(GameManager gm)
        {
            var r = new LevelRating
            {
                AllGems = gm.TotalGems == 0 || gm.GemsCollected >= gm.TotalGems,
                Undetected = gm.Detections == 0,
                AllStealth = gm.TotalEnemies > 0 && gm.StealthKills >= gm.TotalEnemies
            };

            r.Stars = 1 + (r.AllGems ? 1 : 0) + (r.Undetected ? 1 : 0);

            int normalKills = Mathf.Max(0, gm.EnemiesKilled - gm.StealthKills);
            r.Score = PointsCompletion
                      + gm.StealthKills * PointsPerStealthKill
                      + normalKills * PointsPerKill
                      + gm.GemsCollected * PointsPerGem
                      + (r.AllGems ? PointsAllGems : 0)
                      + (r.Undetected ? PointsUndetected : -PenaltyPerDetection * gm.Detections);
            r.Score = Mathf.Max(0, r.Score);

            if (r.Stars == 3 && r.AllStealth) r.Rank = "S";
            else if (r.Stars == 3 || (r.Stars == 2 && r.AllStealth)) r.Rank = "A";
            else if (r.Stars == 2) r.Rank = "B";
            else r.Rank = "C";
            return r;
        }
    }
}
