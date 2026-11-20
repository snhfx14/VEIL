using System.Collections.Generic;
using AulaFSM.Enemy;
using UnityEngine;

namespace AulaFSM
{
    /// <summary>
    /// Definição de um nível. O mapa é desenhado em texto (cada carácter = 2x2 metros):
    ///   .  chão                 #  caixa alta (bloqueia passagem E visão)
    ///   B  caixas empilhadas    o  caixas baixas (bloqueiam passagem, NÃO bloqueiam visão)
    ///   P  jogador              G  diamante
    ///   Guardas:  E normal   H pesado   S atirador   F rápido
    ///
    /// Routes: um percurso por guarda, pela ordem em que aparecem no mapa (linha a linha).
    ///   Formato "coluna,linha coluna,linha ..." — um só ponto = guarda de posto fixo (roda devagar).
    /// </summary>
    public class LevelData
    {
        public string Name;
        public string Hint;
        public string[] Map;
        public string[] Routes;
        public float GuardViewDistance = 9f;
        public float GuardChaseSpeed = 4.4f;
        public float GuardConfirmTime = 0.2f;

        public static bool TryGetGuardType(char c, out GuardType type)
        {
            switch (c)
            {
                case 'E': type = GuardType.Normal; return true;
                case 'H': type = GuardType.Pesado; return true;
                case 'S': type = GuardType.Atirador; return true;
                case 'F': type = GuardType.Rapido; return true;
                default: type = GuardType.Normal; return false;
            }
        }

        public int GuardCount
        {
            get
            {
                int n = 0;
                foreach (var row in Map) foreach (char c in row) if (TryGetGuardType(c, out _)) n++;
                return n;
            }
        }

        /// <summary>Converte o texto de um percurso em células (coluna, linha).</summary>
        public List<Vector2Int> GetRoute(int guardIndex)
        {
            var result = new List<Vector2Int>();
            if (Routes == null || guardIndex >= Routes.Length || string.IsNullOrWhiteSpace(Routes[guardIndex])) return result;
            foreach (var token in Routes[guardIndex].Split(' '))
            {
                var parts = token.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int c) && int.TryParse(parts[1], out int r))
                    result.Add(new Vector2Int(c, r));
            }
            return result;
        }

        // ------------------------------------------------------------------ Os 5 níveis
        // Progressão: 2 → 3 → 4 → 4 → 5 guardas. A dificuldade sobe sobretudo pelos TIPOS de guarda,
        // pelos percursos que se cruzam e pela posição dos diamantes — não só pelo número de inimigos.
        public static readonly LevelData[] All =
        {
            // 1 — Tutorial: dois guardas normais com percursos simples. O diamante do meio está numa
            //     sala com uma só entrada; o do canto direito obriga a esperar que o guarda se afaste.
            new LevelData
            {
                Name = "Armazém", Hint = "Ataca os guardas pelas costas.",
                GuardViewDistance = 8.5f, GuardChaseSpeed = 4.2f, GuardConfirmTime = 0.3f,
                Map = new[]
                {
                    "..................",
                    ".G.......o........",
                    "..####...o..####..",
                    "..#..........E.#..",
                    "..#............#..",
                    "......####........",
                    "......#..#....oo..",
                    "......#G.#........",
                    "......#..#........",
                    ".E....##.#........",
                    "..............##..",
                    "..oo..........#G..",
                    "..............#...",
                    "....###...........",
                    "......#.....o.....",
                    "..................",
                    "........P.........",
                    "..................",
                },
                Routes = new[]
                {
                    "13,3 13,1 4,1 4,4 12,4",   // E: volta ao bloco de cima
                    "1,9 1,15 6,15 4,11",       // E: lado esquerdo, para cima e para baixo
                }
            },

            // 2 — Docas: aparece o guarda RÁPIDO. Caixas baixas (o) deixam ver através: são zonas perigosas.
            new LevelData
            {
                Name = "Docas", Hint = "Caixas baixas não te escondem!",
                GuardViewDistance = 9f, GuardChaseSpeed = 4.4f, GuardConfirmTime = 0.25f,
                Map = new[]
                {
                    "..................",
                    ".G......#.....F...",
                    "........#.........",
                    "..####..#..ooo....",
                    "..#.........o..G..",
                    "..#..E............",
                    "......#####....#..",
                    "...........#...#..",
                    "..oo.......#......",
                    "..o....BB.....E...",
                    ".......BB.....#...",
                    ".####.........#...",
                    "........G.........",
                    "......#....#..oo..",
                    "..#...#....#......",
                    "..#...............",
                    "........P.........",
                    "..................",
                },
                Routes = new[]
                {
                    "14,1 10,1 10,5 16,5 16,2", // F: ronda rápida no canto de cima
                    "5,5 5,9 1,9 1,6",          // E: patrulha o lado esquerdo
                    "14,9 17,9 17,14 12,15 12,12", // E: patrulha a zona de baixo à direita
                }
            },

            // 3 — Labirinto: aparece o guarda PESADO, de posto fixo a guardar um diamante numa sala.
            //     Aguenta um golpe pelo lado: tens de o apanhar mesmo pelas costas quando se vira.
            new LevelData
            {
                Name = "Labirinto", Hint = "O guarda pesado só cai pelas costas.",
                GuardViewDistance = 9.5f, GuardChaseSpeed = 4.6f, GuardConfirmTime = 0.2f,
                Map = new[]
                {
                    "..................",
                    ".G..#.........#.G.",
                    "....#...E.....#...",
                    ".####..o.o..####..",
                    ".......####.......",
                    "..F...........E...",
                    "......o....o......",
                    "###....#..#....###",
                    ".......#GH#.......",
                    ".......####.......",
                    "..B.............B.",
                    "........o.........",
                    ".####.......####..",
                    "........##........",
                    "..G.............G.",
                    "...o..........o...",
                    "..#.....P......#..",
                    "..................",
                },
                Routes = new[]
                {
                    "8,2 12,2 12,1 6,1",        // E: corredor de cima
                    "2,5 6,5 4,11 1,9",         // F: cobre o lado esquerdo depressa
                    "14,5 14,9 10,11 16,13",    // E: atravessa o lado direito
                    "9,8",                      // H: posto fixo na sala do diamante
                }
            },

            // 4 — Fábrica: aparece o ATIRADOR, com visão longa sobre o corredor de cima.
            //     Os percursos do rápido e do normal cruzam-se: é preciso esperar pela janela certa.
            new LevelData
            {
                Name = "Fábrica", Hint = "O atirador vê longe. Usa as caixas altas.",
                GuardViewDistance = 10f, GuardChaseSpeed = 4.8f, GuardConfirmTime = 0.17f,
                Map = new[]
                {
                    "..................",
                    ".G..............G.",
                    "...S....##........",
                    "..##....##....##..",
                    "..##..........##..",
                    ".....oooo.........",
                    "......B....B......",
                    ".####........####.",
                    "........F.........",
                    "...o..........o...",
                    "..#....####....#..",
                    "..#.E..........#..",
                    "..#......H.....#..",
                    "........G.........",
                    ".B......o.......B.",
                    "......#....#......",
                    "......#.P..#......",
                    "..................",
                },
                Routes = new[]
                {
                    "3,2 6,1 12,1",             // S: vigia o corredor de cima
                    "8,8 16,8 13,12 4,12 1,8",  // F: grande volta pelo meio
                    "4,11 3,14 8,13 6,11",      // E: guarda o diamante de baixo
                    "9,12 13,12 13,14 9,13",    // H: ronda curta e lenta
                }
            },

            // 5 — Quartel: todos os tipos. O atirador cobre o diamante de cima, o pesado guarda o lado direito,
            //     e o guarda de baixo passa perto do início — começa por te esconder atrás das caixas.
            new LevelData
            {
                Name = "Quartel", Hint = "Estuda os percursos antes de avançar.",
                GuardViewDistance = 10.5f, GuardChaseSpeed = 5f, GuardConfirmTime = 0.15f,
                Map = new[]
                {
                    "..................",
                    ".S......G.........",
                    "..###.........###.",
                    "..#.....E.......#.",
                    "......######......",
                    "..................",
                    ".##....o..o.....##",
                    ".#..F.........H..#",
                    "........BB........",
                    "........BB........",
                    "..G.............G.",
                    ".####...o...####..",
                    "........E.........",
                    "..................",
                    "..#...#....#...#..",
                    "..#...#....#...#..",
                    "........P.........",
                    "..................",
                },
                Routes = new[]
                {
                    "1,1 6,1",                  // S: vigia o diamante de cima
                    "8,3 13,3 13,5 4,5 4,3",    // E: volta à sala de cima
                    "4,7 2,9 6,10 7,7",         // F: lado esquerdo
                    "14,7 12,9",                // H: lado direito, devagar
                    "8,12 3,13 13,13",          // E: passa perto do início
                }
            },
        };
    }

    /// <summary>Nível atual + progresso guardado (PlayerPrefs): desbloqueios, diamantes e estrelas.</summary>
    public static class LevelManager
    {
        private const string UnlockedKey = "fsm_unlocked";
        private const string GemsKey = "fsm_gems";
        private const string DoneKey = "fsm_done_";
        private const string StarsKey = "fsm_stars_";

        public static int CurrentLevel = -1;
        public static bool SkipMenu;

        public static int Count => LevelData.All.Length;
        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Count);
        public static int TotalGems => PlayerPrefs.GetInt(GemsKey, 0);
        public static bool IsCompleted(int i) => PlayerPrefs.GetInt(DoneKey + i, 0) == 1;
        public static int BestStars(int i) => PlayerPrefs.GetInt(StarsKey + i, 0);
        public static bool IsUnlocked(int i) => i < Unlocked;
        public static bool HasNext => CurrentLevel + 1 < Count;

        public static LevelData Current
        {
            get
            {
                EnsureInit();
                return LevelData.All[CurrentLevel];
            }
        }

        public static void EnsureInit()
        {
            if (CurrentLevel < 0 || CurrentLevel >= Count)
                CurrentLevel = Mathf.Clamp(Unlocked - 1, 0, Count - 1);
        }

        /// <summary>Concluir só exige eliminar os alvos; as estrelas guardam o melhor desempenho.</summary>
        public static void CompleteLevel(int index, int gemsCollected, int stars)
        {
            PlayerPrefs.SetInt(DoneKey + index, 1);
            PlayerPrefs.SetInt(UnlockedKey, Mathf.Max(Unlocked, Mathf.Min(Count, index + 2)));
            PlayerPrefs.SetInt(GemsKey, TotalGems + gemsCollected);
            PlayerPrefs.SetInt(StarsKey + index, Mathf.Max(BestStars(index), stars));
            PlayerPrefs.Save();
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(GemsKey);
            for (int i = 0; i < Count; i++)
            {
                PlayerPrefs.DeleteKey(DoneKey + i);
                PlayerPrefs.DeleteKey(StarsKey + i);
            }
            PlayerPrefs.Save();
            CurrentLevel = 0;
        }
    }
}
