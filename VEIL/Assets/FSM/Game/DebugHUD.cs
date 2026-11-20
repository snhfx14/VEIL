using System.Collections.Generic;
using AulaFSM.Enemy;
using AulaFSM.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AulaFSM
{
    /// <summary>
    /// HUD durante o jogo + visualização das máquinas de estados em tempo real:
    ///  - label com o estado atual por cima de cada personagem
    ///  - painel com o estado atual, tempo no estado e histórico de transições (F1 liga/desliga)
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        public bool showDebugPanel = true;

        private GUIStyle _label, _panelText, _title;
        private Texture2D _white;
        private readonly List<IStateMachineOwner> _owners = new List<IStateMachineOwner>();
        private static readonly Color Ink = new Color(0.08f, 0.09f, 0.12f);

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) showDebugPanel = !showDebugPanel;
        }

        private void InitStyles()
        {
            if (_label != null) return;
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
            _panelText = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
        }

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.StateMachine.CurrentState == null) return;
            InitStyles();

            var state = gm.StateMachine.CurrentState;
            bool inGame = state is GamePlayingState || state is GameReadyState;

            // Nos ecrãs de menu/pausa/vitória/game over só o MenuUI desenha (nada por cima dos painéis)
            if (!inGame) return;

            CollectOwners(gm.Player);
            DrawWorldLabels();
            DrawPopups();
            DrawPlayerHUD(gm);
            if (showDebugPanel) DrawDebugPanel();
        }

        private void CollectOwners(PlayerController player)
        {
            _owners.Clear();
            if (GameManager.Instance != null) _owners.Add(GameManager.Instance);
            if (player != null) _owners.Add(player);
            foreach (var e in EnemyAI.All)
                if (e != null && e.StateMachine != null) _owners.Add(e);
        }

        // --------------------------------------------------------------- Labels por cima das personagens
        private void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var owner in _owners)
            {
                if (owner is GameManager) continue;
                var state = owner.StateMachine.CurrentState;
                if (state == null) continue;

                Vector3 screen = cam.WorldToScreenPoint(owner.transform.position + Vector3.up * 2.4f);
                if (screen.z < 0) continue;
                var rect = new Rect(screen.x - 90, Screen.height - screen.y - 14, 180, 28);
                ShadowLabel(rect, state.Name, _label, state.DebugColor);

                // Medidor de consciência do guarda (enche à medida que te vê: amarelo → vermelho)
                if (owner is EnemyAI enemy && !enemy.Health.IsDead && enemy.Awareness > 0.01f)
                {
                    var bar = new Rect(screen.x - 32, Screen.height - screen.y + 12, 64, 8);
                    Fill(bar, new Color(0, 0, 0, 0.7f));
                    Fill(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * enemy.Awareness, bar.height - 2),
                        Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.15f, 0.1f), enemy.Awareness));
                }

                // Barra de vida (só o jogador)
                if (owner is PlayerController p)
                {
                    var bar = new Rect(screen.x - 30, Screen.height - screen.y + 12, 60, 7);
                    Fill(bar, new Color(0, 0, 0, 0.6f));
                    Fill(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * p.Health.Normalized, bar.height - 2),
                        Color.Lerp(Color.red, Color.green, p.Health.Normalized));
                }
            }
        }

        // --------------------------------------------------------------- Textos flutuantes
        private static void DrawPopups()
        {
            var cam = Camera.main;
            var list = Visuals.Popups.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                float age = Time.unscaledTime - p.Born;
                if (age > Visuals.Popups.Life || cam == null) { list.RemoveAt(i); continue; }
                float k = age / Visuals.Popups.Life;

                Vector3 sp = cam.WorldToScreenPoint(p.Position + Vector3.up * (k * 1.2f));
                if (sp.z < 0) continue;
                float pop = k < 0.12f ? Mathf.Lerp(0.4f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.12f) / 0.15f));
                int size = Mathf.RoundToInt(p.Size * pop * Screen.height / 1080f);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, 1f - Mathf.Clamp01((k - 0.7f) / 0.3f));
                UIKit.Text(new Rect(sp.x - 150, Screen.height - sp.y - 30, 300, 60), p.Text, size, p.Color, Ink, Mathf.Max(2, size / 12));
                GUI.color = old;
            }
        }

        // --------------------------------------------------------------- HUD do jogador (topo do ecrã)
        private void DrawPlayerHUD(GameManager gm)
        {
            float s = Screen.height / 1080f;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            // Alvos (centro)
            var targets = new Rect(w / 2 - 170, 18, 340, 64);
            GUI.Box(targets, GUIContent.none, UIKit.Box(new Color(0.9f, 0.2f, 0.22f), Ink, 20, 4));
            UIKit.Text(targets, $"ALVOS  {gm.EnemiesKilled} / {gm.TotalEnemies}", 34, Color.white, Ink, 3);

            // Alerta da zona (sobe com cada cadáver descoberto) e se já foste detetado
            int zone = GuardSquad.ZoneAlert;
            var zoneRect = new Rect(w / 2 - 170, 90, 340, 40);
            Color zoneColor = zone == 0 ? new Color(0.2f, 0.5f, 0.3f, 0.85f) : Color.Lerp(new Color(0.9f, 0.6f, 0.1f), new Color(0.9f, 0.1f, 0.1f), (zone - 1) / 2f);
            GUI.Box(zoneRect, GUIContent.none, UIKit.Box(zoneColor, Ink, 14, 3));
            string zoneText = zone == 0 ? "ZONA CALMA" : zone == 1 ? "ZONA EM ALERTA" : zone == 2 ? "ALERTA ELEVADO" : "ALERTA MÁXIMO";
            UIKit.Text(zoneRect, gm.Detections > 0 ? $"{zoneText}  •  DETETADO {gm.Detections}x" : zoneText, 20, Color.white, Ink, 2);

            // Vida + diamantes + nível (esquerda)
            var box = new Rect(18, 18, 330, 120);
            GUI.Box(box, GUIContent.none, UIKit.Box(new Color(0, 0, 0, 0.5f), new Color(1, 1, 1, 0.15f), 18, 3));
            UIKit.Text(new Rect(36, 26, 300, 34), $"NÍVEL {gm.LevelIndex + 1} — {gm.Level.Name}", 22, Color.white, Ink, 2, TextAnchor.MiddleLeft);
            if (gm.Player != null)
            {
                var bar = new Rect(36, 66, 290, 22);
                GUI.Box(bar, GUIContent.none, UIKit.Box(new Color(0.15f, 0.15f, 0.18f), Ink, 8, 2));
                float hp = gm.Player.Health.Normalized;
                if (hp > 0.01f)
                    GUI.Box(new Rect(bar.x + 3, bar.y + 3, (bar.width - 6) * hp, bar.height - 6), GUIContent.none,
                        UIKit.Box(Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.3f), hp), Color.clear, 6, 0));
            }
            UIKit.Text(new Rect(36, 94, 300, 34), $"Diamantes: {gm.GemsCollected} / {gm.TotalGems}", 22, new Color(0.5f, 1f, 1f), Ink, 2, TextAnchor.MiddleLeft);

            GUI.matrix = old;
        }

        // --------------------------------------------------------------- Painel de debug das FSMs
        private void DrawDebugPanel()
        {
            const float width = 400f;
            float x = Screen.width - width - 16;
            float y = 16;

            float height = 30;
            foreach (var o in _owners) height += 40 + Mathf.Min(3, o.StateMachine.History.Count) * 15;
            Fill(new Rect(x, y, width, height), new Color(0, 0, 0, 0.6f));

            GUI.Label(new Rect(x + 10, y + 6, width, 20), "<color=#9cf>MÁQUINAS DE ESTADOS</color>  <size=11>(F1 esconder)</size>", _title);
            y += 30;

            foreach (var owner in _owners)
            {
                var sm = owner.StateMachine;
                var state = sm.CurrentState;
                string hex = state != null ? ColorUtility.ToHtmlStringRGB(state.DebugColor) : "FFFFFF";
                GUI.Label(new Rect(x + 10, y, width - 20, 20),
                    $"<b>{owner.DisplayName}</b>:  <color=#{hex}><b>{state?.Name}</b></color>   <size=11>{sm.TimeInState:0.0}s</size>", _title);
                y += 20;

                int lines = Mathf.Min(3, sm.History.Count);
                for (int i = 0; i < lines; i++)
                {
                    string alpha = i == 0 ? "dddddd" : "888888";
                    GUI.Label(new Rect(x + 22, y, width - 30, 16), $"<color=#{alpha}>{sm.History[i]}</color>", _panelText);
                    y += 15;
                }
                y += 20;
            }
        }

        // --------------------------------------------------------------- Helpers
        private void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private static void ShadowLabel(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}
