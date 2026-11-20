using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM
{
    /// <summary>
    /// Menus coloridos (IMGUI com texturas arredondadas geradas por código).
    /// O que aparece depende do ESTADO atual da FSM do jogo (Menu, Preparar, Pausa, Vitória, Game Over).
    /// Tudo é desenhado numa resolução virtual de 1080 de altura e escalado para o ecrã.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private const float H = 1080f;
        private float W;
        private int _selected = -1;

        // Paleta
        private static readonly Color Teal = new Color(0.16f, 0.70f, 0.62f);
        private static readonly Color TealDark = new Color(0.05f, 0.27f, 0.27f);
        private static readonly Color Yellow = new Color(1f, 0.80f, 0.12f);
        private static readonly Color Orange = new Color(1f, 0.55f, 0.12f);
        private static readonly Color Green = new Color(0.30f, 0.80f, 0.30f);
        private static readonly Color Blue = new Color(0.22f, 0.50f, 0.95f);
        private static readonly Color Red = new Color(0.90f, 0.20f, 0.22f);
        private static readonly Color Gray = new Color(0.45f, 0.50f, 0.56f);
        private static readonly Color Ink = new Color(0.08f, 0.09f, 0.12f);

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.StateMachine.CurrentState == null) return;

            float s = Screen.height / H;
            W = Screen.width / s;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            UIKit.BeginFrame(s);

            switch (gm.StateMachine.CurrentState)
            {
                case GameMenuState _: DrawMenu(gm); break;
                case GameReadyState _: DrawReady(gm); break;
                case GamePausedState _: DrawPause(gm); break;
                case GameVictoryState _: DrawVictory(gm); break;
                case GameOverState _: DrawGameOver(gm); break;
            }

            GUI.matrix = oldMatrix;
        }

        // =====================================================================  MENU INICIAL
        private void DrawMenu(GameManager gm)
        {
            if (_selected < 0) _selected = LevelManager.CurrentLevel;

            GUI.DrawTexture(new Rect(0, 0, W, H), UIKit.Gradient(new Color(0.04f, 0.32f, 0.42f, 0.88f), new Color(0.05f, 0.08f, 0.22f, 0.95f)));

            // Título
            UIKit.Text(new Rect(0, 40, W, 130), "LÂMINA SILENCIOSA", 104, Yellow, Ink, 7);
            var tag = new Rect(W / 2 - 330, 175, 660, 56);
            GUI.Box(tag, GUIContent.none, UIKit.Box(Red, Ink));
            UIKit.Text(tag, "STEALTH  •  MÁQUINAS DE ESTADOS", 28, Color.white, Ink, 2);

            // Painel principal
            var panel = new Rect(W / 2 - 460, 260, 920, 560);
            GUI.Box(panel, GUIContent.none, UIKit.Box(Teal, TealDark, 24, 6));
            UIKit.Text(new Rect(panel.x, panel.y + 22, panel.width, 60), "ESCOLHE O NÍVEL", 44, Color.white, Ink, 3);
            DrawGems(new Rect(panel.xMax - 190, panel.y + 26, 160, 52), LevelManager.TotalGems.ToString());

            // Cartões dos níveis
            int n = LevelManager.Count;
            const float cw = 145f, ch = 160f, gap = 22f;
            float total = n * cw + (n - 1) * gap;
            float x0 = panel.x + (panel.width - total) / 2f;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (cw + gap), panel.y + 105, cw, ch);
                bool unlocked = LevelManager.IsUnlocked(i);
                bool done = LevelManager.IsCompleted(i);
                Color c = !unlocked ? Gray : done ? Green : Orange;

                if (i == _selected)
                    GUI.Box(new Rect(r.x - 9, r.y - 9, r.width + 18, r.height + 18), GUIContent.none, UIKit.Box(Yellow, Ink, 22, 5));

                string sub = !unlocked ? "BLOQUEADO" : LevelData.All[i].Name.ToUpper();
                if (UIKit.Card(r, unlocked ? (i + 1).ToString() : "?", sub, c, unlocked)) _selected = i;

                // Melhor classificação (estrelas) por baixo do cartão
                if (done) DrawStars(new Vector2(r.center.x, r.yMax + 22), LevelManager.BestStars(i), 13f, 32f);
            }

            // Informação do nível escolhido
            var lvl = LevelData.All[_selected];
            UIKit.Text(new Rect(panel.x, panel.y + 300, panel.width, 50),
                $"Nível {_selected + 1} — {lvl.Name}   •   {lvl.GuardCount} alvos", 32, Color.white, Ink, 3);

            // Botão JOGAR
            var play = new Rect(W / 2 - 220, panel.y + 360, 440, 125);
            if (UIKit.BigButton(play, "JOGAR", Yellow, 70))
            {
                if (_selected == LevelManager.CurrentLevel) gm.RequestStart();
                else GameManager.LoadLevel(_selected);
            }

            // Repor progresso
            var reset = new Rect(panel.x + 24, panel.yMax - 62, 200, 42);
            if (UIKit.BigButton(reset, "Repor progresso", Gray, 20))
            {
                LevelManager.ResetProgress();
                _selected = 0;
            }

            // Cartão de controlos
            var help = new Rect(W / 2 - 600, 850, 1200, 190);
            GUI.Box(help, GUIContent.none, UIKit.Box(new Color(0f, 0f, 0f, 0.55f), new Color(1f, 1f, 1f, 0.15f), 22, 3));
            string[,] items =
            {
                { "CLIQUE", "mover (podes arrastar)" },
                { "GUARDA", "clica para o perseguir" },
                { "COSTAS", "por trás = eliminação" },
                { "F1", "ver as máquinas de estados" },
            };
            float iw = help.width / 2f;
            for (int i = 0; i < 4; i++)
            {
                float ix = help.x + 40 + (i % 2) * iw;
                float iy = help.y + 25 + (i / 2) * 62;
                var key = new Rect(ix, iy, 140, 48);
                GUI.Box(key, GUIContent.none, UIKit.Box(Color.white, Ink, 14, 3));
                UIKit.Text(key, items[i, 0], 24, Ink, Color.clear, 0);
                UIKit.Text(new Rect(ix + 155, iy, iw - 200, 48), items[i, 1], 26, Color.white, Ink, 2, TextAnchor.MiddleLeft);
            }
            UIKit.Text(new Rect(help.x, help.yMax - 52, help.width, 40),
                "Pela frente não dá! Evita as lanternas e não deixes corpos à vista.", 24, Yellow, Ink, 2);
        }

        // =====================================================================  PREPARAR
        private void DrawReady(GameManager gm)
        {
            float t = gm.StateMachine.TimeInState;
            float alpha = 1f - Mathf.Clamp01((t - 1.3f) / 0.5f);
            float pop = 1f + Mathf.Max(0f, 0.25f - t) * 2f; // pequeno "zoom" de entrada
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);

            UIKit.Text(new Rect(0, 300, W, 160), $"NÍVEL {gm.LevelIndex + 1}", Mathf.RoundToInt(130 * pop), Yellow, Ink, 8);
            UIKit.Text(new Rect(0, 450, W, 70), gm.Level.Name.ToUpper(), 52, Color.white, Ink, 4);
            var pill = new Rect(W / 2 - 340, 545, 680, 70);
            GUI.Box(pill, GUIContent.none, UIKit.Box(Red, Ink));
            UIKit.Text(pill, $"Elimina os {gm.TotalEnemies} guardas!", 36, Color.white, Ink, 3);
            if (!string.IsNullOrEmpty(gm.Level.Hint))
                UIKit.Text(new Rect(0, 635, W, 50), gm.Level.Hint, 30, Yellow, Ink, 3);

            GUI.color = old;
        }

        // =====================================================================  PAUSA
        private void DrawPause(GameManager gm)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), UIKit.Solid(new Color(0, 0, 0, 0.6f)));
            var panel = new Rect(W / 2 - 300, 230, 600, 600);
            GUI.Box(panel, GUIContent.none, UIKit.Box(Teal, TealDark, 24, 6));
            UIKit.Text(new Rect(panel.x, panel.y + 30, panel.width, 100), "PAUSA", 80, Yellow, Ink, 6);

            if (UIKit.BigButton(new Rect(panel.x + 80, panel.y + 170, 440, 110), "CONTINUAR", Green, 48)) gm.RequestResume();
            if (UIKit.BigButton(new Rect(panel.x + 80, panel.y + 300, 440, 110), "RECOMEÇAR", Orange, 48)) GameManager.Retry();
            if (UIKit.BigButton(new Rect(panel.x + 80, panel.y + 430, 440, 110), "MENU", Blue, 48)) GameManager.GoToMenu();
        }

        // =====================================================================  VITÓRIA
        private void DrawVictory(GameManager gm)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), UIKit.Gradient(new Color(0.1f, 0.35f, 0.15f, 0.75f), new Color(0.02f, 0.12f, 0.08f, 0.9f)));
            UIKit.Text(new Rect(0, 90, W, 140), "NÍVEL COMPLETO!", 110, Yellow, Ink, 8);

            var rating = gm.Rating;
            var panel = new Rect(W / 2 - 470, 240, 940, 640);
            GUI.Box(panel, GUIContent.none, UIKit.Box(Teal, TealDark, 24, 6));

            // Nota (S/A/B/C) + estrelas
            Color rankColor = rating.Rank == "S" ? Yellow : rating.Rank == "A" ? Green : rating.Rank == "B" ? Blue : Gray;
            var rankBox = new Rect(panel.x + 40, panel.y + 35, 170, 170);
            GUI.Box(rankBox, GUIContent.none, UIKit.Box(rankColor, Ink, 30, 6));
            UIKit.Text(rankBox, rating.Rank, 120, Color.white, Ink, 7);
            UIKit.Text(new Rect(rankBox.x, rankBox.yMax + 4, rankBox.width, 40), $"{rating.Score} pts", 26, Color.white, Ink, 2);
            DrawStars(new Vector2(panel.x + 600, panel.y + 80), rating.Stars, 38f, 95f);

            // Objetivos das estrelas
            float ox = panel.x + 270, oy = panel.y + 140;
            DrawObjective(new Rect(ox, oy, 640, 40), true, "Eliminar todos os alvos");
            DrawObjective(new Rect(ox, oy + 42, 640, 40), rating.AllGems, $"Apanhar todos os diamantes  ({gm.GemsCollected}/{gm.TotalGems})");
            DrawObjective(new Rect(ox, oy + 84, 640, 40), rating.Undetected, rating.Undetected ? "Nunca ser detetado" : $"Nunca ser detetado  (detetado {gm.Detections}x)");

            // Resumo do desempenho
            var stats = new Rect(panel.x + 40, panel.y + 290, panel.width - 80, 110);
            GUI.Box(stats, GUIContent.none, UIKit.Box(new Color(0f, 0f, 0f, 0.25f), new Color(0f, 0f, 0f, 0.2f), 16, 2));
            UIKit.Text(new Rect(stats.x, stats.y + 8, stats.width / 2f, 45), $"Silenciosas:  {gm.StealthKills} / {gm.TotalEnemies}", 30, Color.white, Ink, 3);
            UIKit.Text(new Rect(stats.center.x, stats.y + 8, stats.width / 2f, 45), $"Alvos:  {gm.EnemiesKilled} / {gm.TotalEnemies}", 30, Color.white, Ink, 3);
            UIKit.Text(new Rect(stats.x, stats.y + 56, stats.width / 2f, 45), $"Diamantes:  {gm.GemsCollected} / {gm.TotalGems}", 30, new Color(0.6f, 1f, 1f), Ink, 3);
            UIKit.Text(new Rect(stats.center.x, stats.y + 56, stats.width / 2f, 45), $"Tempo:  {gm.PlayTime:0.0} s", 30, Color.white, Ink, 3);

            if (LevelManager.HasNext)
            {
                if (UIKit.BigButton(new Rect(panel.center.x - 250, panel.y + 420, 500, 110), "PRÓXIMO NÍVEL", Green, 50)) GameManager.NextLevel();
            }
            else
            {
                UIKit.Text(new Rect(panel.x, panel.y + 430, panel.width, 90), "Completaste todos os níveis!", 40, Yellow, Ink, 4);
            }
            if (UIKit.BigButton(new Rect(panel.center.x - 250, panel.y + 545, 240, 75), "REPETIR", Orange, 30)) GameManager.Retry();
            if (UIKit.BigButton(new Rect(panel.center.x + 10, panel.y + 545, 240, 75), "MENU", Blue, 30)) GameManager.GoToMenu();
        }

        private static void DrawObjective(Rect r, bool done, string text)
        {
            DrawStars(new Vector2(r.x + 16, r.center.y), done ? 1 : 0, 15f, 0f, 1);
            UIKit.Text(new Rect(r.x + 45, r.y, r.width - 45, r.height), text, 28, done ? Color.white : new Color(1f, 1f, 1f, 0.55f), Ink, 2, TextAnchor.MiddleLeft);
        }

        /// <summary>Desenha "total" estrelas centradas em "center" (preenchidas até "filled").</summary>
        private static void DrawStars(Vector2 center, int filled, float size, float spacing, int total = 3)
        {
            float x0 = center.x - spacing * (total - 1) / 2f;
            for (int i = 0; i < total; i++)
            {
                Color c = i < filled ? Yellow : new Color(0.2f, 0.25f, 0.3f);
                GUI.DrawTexture(new Rect(x0 + i * spacing - size, center.y - size, size * 2f, size * 2f), UIKit.Star(c, Ink));
            }
        }

        // =====================================================================  GAME OVER
        private void DrawGameOver(GameManager gm)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), UIKit.Gradient(new Color(0.45f, 0.02f, 0.05f, 0.7f), new Color(0.1f, 0f, 0.02f, 0.9f)));
            UIKit.Text(new Rect(0, 160, W, 150), "APANHADO!", 130, Red, Ink, 9);
            UIKit.Text(new Rect(0, 330, W, 60), $"Eliminaste {gm.EnemiesKilled} de {gm.TotalEnemies} guardas.", 38, Color.white, Ink, 3);
            UIKit.Text(new Rect(0, 390, W, 60), "Dica: espera que o guarda se vire e ataca por trás.", 30, Yellow, Ink, 3);

            if (UIKit.BigButton(new Rect(W / 2 - 310, 500, 620, 125), "TENTAR DE NOVO", Orange, 52)) GameManager.Retry();
            if (UIKit.BigButton(new Rect(W / 2 - 160, 650, 320, 95), "MENU", Blue, 40)) GameManager.GoToMenu();
        }

        // =====================================================================  Ícones
        private static void DrawGems(Rect r, string text)
        {
            GUI.Box(r, GUIContent.none, UIKit.Box(new Color(0, 0, 0, 0.45f), new Color(1, 1, 1, 0.2f), 20, 3));
            DrawDiamond(new Vector2(r.x + 30, r.center.y), 15, new Color(0.3f, 1f, 1f));
            UIKit.Text(new Rect(r.x + 55, r.y, r.width - 60, r.height), text, 32, Color.white, Ink, 2, TextAnchor.MiddleLeft);
        }

        private static void DrawDiamond(Vector2 center, float size, Color color)
        {
            // Textura já em forma de losango (rodar com GUI.matrix escalada desalinhava o ícone)
            GUI.DrawTexture(new Rect(center.x - size * 1.3f, center.y - size * 1.3f, size * 2.6f, size * 2.6f), UIKit.Diamond(color, Ink));
        }
    }

    /// <summary>Texturas e estilos para os menus (cantos arredondados, gradientes, texto com contorno).</summary>
    public static class UIKit
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, GUIStyle> Styles = new Dictionary<string, GUIStyle>();
        private static GUIStyle _text;

        public static Texture2D Rounded(Color fill, Color border, int radius = 18, int borderWidth = 4, bool shine = true)
        {
            string key = $"r{fill}{border}{radius}{borderWidth}{shine}";
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float qy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - radius; // < 0 dentro
                float alpha = Mathf.Clamp01(0.5f - d);
                Color inner = fill;
                if (shine) inner = Color.Lerp(fill * 0.82f, Color.Lerp(fill, Color.white, 0.18f), y / (float)size); // brilho em cima
                inner.a = fill.a;
                float b = Mathf.Clamp01(d + borderWidth + 0.5f);
                Color c = Color.Lerp(inner, border, b);
                c.a *= alpha;
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        /// <summary>Estrela de 5 pontas com contorno (gerada por código).</summary>
        public static Texture2D Star(Color fill, Color border)
        {
            string key = $"s{fill}{border}";
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u) - Mathf.PI / 2f;           // uma ponta para cima
                float k = Mathf.Abs(Mathf.Repeat(a / (2f * Mathf.PI / 5f), 1f) - 0.5f) * 2f; // 1 nas pontas, 0 entre elas
                float edge = Mathf.Lerp(0.42f, 0.95f, k * k);        // raio do contorno da estrela
                float d = (r - edge) * size * 0.5f;                     // < 0 dentro (em píxeis)
                Color inner = Color.Lerp(fill, Color.white, v > 0.2f ? 0.25f : 0f);
                inner.a = 1f;
                Color c = Color.Lerp(inner, border, Mathf.Clamp01(d + 3.5f));
                c.a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        /// <summary>Losango (diamante) com contorno e brilho.</summary>
        public static Texture2D Diamond(Color fill, Color border)
        {
            string key = $"d{fill}{border}";
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = (Mathf.Abs(u) + Mathf.Abs(v)) * size * 0.5f - size * 0.46f; // < 0 dentro
                float alpha = Mathf.Clamp01(0.5f - d);
                Color inner = (u < 0f && v > 0f) ? Color.Lerp(fill, Color.white, 0.45f) : (u > 0f && v < 0f ? fill * 0.75f : fill);
                inner.a = 1f;
                Color c = Color.Lerp(inner, border, Mathf.Clamp01(d + 4.5f));
                c.a = alpha;
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        public static Texture2D Gradient(Color top, Color bottom)
        {
            string key = $"g{top}{bottom}";
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        public static Texture2D Solid(Color c) => Gradient(c, c);

        public static GUIStyle Box(Color fill, Color border, int radius = 18, int borderWidth = 4)
        {
            string key = $"b{fill}{border}{radius}{borderWidth}";
            if (Styles.TryGetValue(key, out var st) && st.normal.background != null) return st;
            st = new GUIStyle { border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2) };
            st.normal.background = Rounded(fill, border, radius, borderWidth);
            Styles[key] = st;
            return st;
        }

        public static GUIStyle Button(Color c)
        {
            string key = $"btn{c}";
            if (Styles.TryGetValue(key, out var st) && st.normal.background != null) return st;
            var ink = new Color(0.08f, 0.09f, 0.12f);
            st = new GUIStyle { border = new RectOffset(20, 20, 20, 20) };
            st.normal.background = Rounded(c, ink, 18, 5);
            st.hover.background = Rounded(Color.Lerp(c, Color.white, 0.2f), ink, 18, 5);
            st.active.background = Rounded(c * 0.75f + new Color(0, 0, 0, 0.25f), ink, 18, 5);
            Styles[key] = st;
            return st;
        }

        // ------------------------------------------------------------------ Botões com hover próprio
        // O hover usa a posição do rato do Input System (sempre atualizada), em vez dos eventos do IMGUI,
        // que só chegam quando há eventos de rato e deixavam o hover "preso"/a piscar.
        private static Vector2 _mouse;
        private static readonly Dictionary<int, float> HoverAnim = new Dictionary<int, float>();
        private static readonly Color InkC = new Color(0.08f, 0.09f, 0.12f);

        public static void BeginFrame(float scale)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
            {
                Vector2 p = m.position.ReadValue();
                _mouse = new Vector2(p.x, Screen.height - p.y) / scale;
            }
            else if (Event.current != null) _mouse = Event.current.mousePosition;
        }

        private static float Hover(Rect r, bool enabled, out bool hovering)
        {
            hovering = enabled && r.Contains(_mouse);
            int id = (Mathf.RoundToInt(r.x) * 73856093) ^ (Mathf.RoundToInt(r.y) * 19349663) ^ Mathf.RoundToInt(r.width);
            HoverAnim.TryGetValue(id, out float a);
            if (Event.current.type == EventType.Repaint)
            {
                a = Mathf.MoveTowards(a, hovering ? 1f : 0f, Time.unscaledDeltaTime * 9f);
                HoverAnim[id] = a;
            }
            return a;
        }

        private static bool Clicked(Rect r, bool enabled)
        {
            var e = Event.current;
            if (!enabled || e.type != EventType.MouseDown || e.button != 0 || !r.Contains(_mouse)) return false;
            e.Use();
            Visuals.Sfx.Play(Visuals.Sfx.Click, 0.6f);
            return true;
        }

        private static Rect Grow(Rect r, float a, out float lift)
        {
            float k = 1f + 0.05f * a;
            lift = -5f * a;
            float w = r.width * k, h = r.height * k;
            return new Rect(r.center.x - w / 2f, r.center.y - h / 2f + lift, w, h);
        }

        private static void DrawButtonBody(Rect r, Color color, float hover, bool pressed)
        {
            if (Event.current.type != EventType.Repaint) return;
            // sombra
            GUI.Box(new Rect(r.x, r.y + 7, r.width, r.height), GUIContent.none, Box(new Color(0, 0, 0, 0.35f), Color.clear, 18, 0));
            Color body = pressed ? color * 0.8f : color;
            body.a = 1f;
            GUI.Box(r, GUIContent.none, Box(body, InkC, 18, 5));
            if (hover > 0.01f && !pressed)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, hover * 0.35f);
                GUI.Box(r, GUIContent.none, Box(Color.white, InkC, 18, 5));
                GUI.color = old;
            }
        }

        /// <summary>Botão grande com texto com contorno. Devolve true quando é clicado.</summary>
        public static bool BigButton(Rect r, string label, Color color, int fontSize, bool enabled = true)
        {
            float a = Hover(r, enabled, out bool hovering);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool pressed = hovering && mouse != null && mouse.leftButton.isPressed;
            var dr = Grow(r, pressed ? 0f : a, out _);
            DrawButtonBody(dr, enabled ? color : new Color(0.45f, 0.5f, 0.56f), a, pressed);
            Text(dr, label, Mathf.RoundToInt(fontSize * (1f + 0.04f * a)), Color.white, InkC, Mathf.Max(2, fontSize / 14));
            return Clicked(r, enabled);
        }

        /// <summary>Cartão de nível (número grande + legenda).</summary>
        public static bool Card(Rect r, string big, string small, Color color, bool enabled)
        {
            float a = Hover(r, enabled, out bool hovering);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool pressed = hovering && mouse != null && mouse.leftButton.isPressed;
            var dr = Grow(r, pressed ? 0f : a * 1.4f, out _);
            DrawButtonBody(dr, color, a, pressed);
            Text(new Rect(dr.x, dr.y + 10, dr.width, 90), big, 78, Color.white, InkC, 4);
            Text(new Rect(dr.x, dr.y + dr.height - 55, dr.width, 40), small, 19, Color.white, InkC, 2);
            return Clicked(r, enabled);
        }

        /// <summary>Texto com contorno (desenha a cor do contorno 8 vezes à volta).</summary>
        public static void Text(Rect r, string text, int size, Color color, Color outline, int thickness,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            if (_text == null) _text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            _text.fontSize = size;
            _text.alignment = anchor;

            var tint = GUI.color;
            if (thickness > 0 && outline.a > 0f)
            {
                _text.normal.textColor = new Color(outline.r, outline.g, outline.b, outline.a * tint.a);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    GUI.Label(new Rect(r.x + dx * thickness, r.y + dy * thickness, r.width, r.height), text, _text);
                }
                // sombra por baixo
                GUI.Label(new Rect(r.x, r.y + thickness * 1.8f, r.width, r.height), text, _text);
            }
            _text.normal.textColor = new Color(color.r, color.g, color.b, color.a * tint.a);
            GUI.Label(r, text, _text);
        }
    }
}
