using AulaFSM.Core;
using AulaFSM.Enemy;
using AulaFSM.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AulaFSM
{
    /// <summary>
    /// O próprio fluxo do jogo também é uma máquina de estados
    /// (reutiliza exatamente a mesma classe StateMachine do Player e dos NPCs).
    ///
    /// Estados: Menu → Preparar → A Jogar ⇄ Pausa → Vitória / Game Over
    /// </summary>
    public class GameManager : MonoBehaviour, IStateMachineOwner
    {
        public static GameManager Instance { get; private set; }

        public StateMachine StateMachine { get; private set; }
        public string DisplayName => "Jogo";
        public PlayerController Player { get; private set; }

        public int LevelIndex => LevelManager.CurrentLevel;
        public LevelData Level => LevelManager.Current;
        public int TotalEnemies { get; private set; }
        public int EnemiesKilled { get; private set; }
        public int GemsCollected { get; private set; }
        public int TotalGems { get; private set; }
        public float PlayTime { get; private set; }
        public bool IsPlaying => StateMachine != null && StateMachine.CurrentState == _playing;

        // Estatísticas para a classificação do nível
        public int StealthKills { get; private set; }
        public int Detections { get; private set; }
        public LevelRating Rating { get; private set; }

        private GameMenuState _menu;
        private GameReadyState _ready;
        private GamePlayingState _playing;
        private GamePausedState _paused;
        private GameVictoryState _victory;
        private GameOverState _gameOver;

        private bool _startRequested, _pauseRequested, _resumeRequested;
        private float _lastKillTime = -99f;

        private void Awake()
        {
            Instance = this;
            LevelManager.EnsureInit();

            _menu = new GameMenuState(this);
            _ready = new GameReadyState(this);
            _playing = new GamePlayingState(this);
            _paused = new GamePausedState(this);
            _victory = new GameVictoryState(this);
            _gameOver = new GameOverState(this);

            StateMachine = new StateMachine("Jogo");
            StateMachine.AddTransition(_menu, _ready, () => Consume(ref _startRequested), "carregou em JOGAR");
            StateMachine.AddTransition(_ready, _playing, () => StateMachine.TimeInState > 1.8f, "começou o nível");
            StateMachine.AddTransition(_playing, _paused, () => PausePressed() || Consume(ref _pauseRequested), "pausa");
            StateMachine.AddTransition(_playing, _victory, () => EnemiesKilled >= TotalEnemies && Time.time - _lastKillTime > 0.9f, "eliminou todos os alvos");
            StateMachine.AddTransition(_playing, _gameOver, () => Player != null && Player.Health.IsDead && Player.StateMachine.TimeInState > 1.2f, "o jogador morreu");
            StateMachine.AddTransition(_paused, _playing, () => PausePressed() || Consume(ref _resumeRequested), "retomar");
        }

        private void Start()
        {
            Player = FindFirstObjectByType<PlayerController>();
            TotalEnemies = EnemyAI.All.Count;
            TotalGems = FindObjectsByType<Crystal>(FindObjectsSortMode.None).Length;

            // Ao vir de "Próximo nível"/"Tentar de novo" salta o menu
            bool skip = LevelManager.SkipMenu;
            LevelManager.SkipMenu = false;
            StateMachine.SetState(skip ? (IState)_ready : _menu, skip ? "nível carregado" : "inicial");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        // Usa unscaledDeltaTime para funcionar também em pausa (timeScale = 0)
        private void Update() => StateMachine.Tick(Time.unscaledDeltaTime);

        // ------------------------------------------------------------------ Chamado pelo jogo
        public void AddPlayTime(float dt) => PlayTime += dt;
        public void CollectCrystal() => GemsCollected++;

        public void RegisterEnemyKilled()
        {
            EnemiesKilled++;
            _lastKillTime = Time.time;
        }

        /// <summary>Chamado pelo jogador quando elimina um guarda (stealth = o guarda não o tinha detetado).</summary>
        public void RegisterKill(bool stealth)
        {
            if (stealth) StealthKills++;
        }

        /// <summary>Chamado quando um guarda entra em Combate (o jogador foi detetado).</summary>
        public void RegisterDetected()
        {
            if (IsPlaying) Detections++;
        }

        public void OnVictory()
        {
            Rating = LevelRating.Evaluate(this);
            LevelManager.CompleteLevel(LevelIndex, GemsCollected, Rating.Stars);
        }

        // ------------------------------------------------------------------ Chamado pelos botões do menu
        public void RequestStart() => _startRequested = true;
        public void RequestPause() => _pauseRequested = true;
        public void RequestResume() => _resumeRequested = true;

        public static void LoadLevel(int index)
        {
            LevelManager.CurrentLevel = Mathf.Clamp(index, 0, LevelManager.Count - 1);
            LevelManager.SkipMenu = true;
            ReloadScene();
        }

        public static void Retry() => LoadLevel(LevelManager.CurrentLevel);

        public static void NextLevel() => LoadLevel(LevelManager.CurrentLevel + 1);

        public static void GoToMenu()
        {
            LevelManager.SkipMenu = false;
            ReloadScene();
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        // ------------------------------------------------------------------ Helpers
        private static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

        private static bool PausePressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }
    }

    // ------------------------------------------------------------------ Estados do jogo

    public class GameMenuState : State<GameManager>
    {
        public GameMenuState(GameManager o) : base(o) { }
        public override string Name => "Menu";
        public override Color DebugColor => Color.white;
        public override void Enter() => Time.timeScale = 1f; // os guardas patrulham no fundo do menu
    }

    public class GameReadyState : State<GameManager>
    {
        public GameReadyState(GameManager o) : base(o) { }
        public override string Name => "Preparar";
        public override Color DebugColor => new Color(1f, 0.8f, 0.3f);
    }

    public class GamePlayingState : State<GameManager>
    {
        public GamePlayingState(GameManager o) : base(o) { }
        public override string Name => "A Jogar";
        public override Color DebugColor => new Color(0.4f, 1f, 0.4f);
        public override void Enter() => Time.timeScale = 1f;
        public override void Tick(float dt) => Owner.AddPlayTime(dt);
    }

    public class GamePausedState : State<GameManager>
    {
        public GamePausedState(GameManager o) : base(o) { }
        public override string Name => "Pausa";
        public override Color DebugColor => new Color(1f, 1f, 0.4f);
        public override void Enter() => Time.timeScale = 0f;
        public override void Exit() => Time.timeScale = 1f;
    }

    public class GameVictoryState : State<GameManager>
    {
        public GameVictoryState(GameManager o) : base(o) { }
        public override string Name => "Vitória";
        public override Color DebugColor => new Color(0.4f, 1f, 1f);
        public override void Enter()
        {
            Owner.OnVictory();
            Visuals.Sfx.Play(Visuals.Sfx.Win, 0.8f, 1f, 0f);
        }
    }

    public class GameOverState : State<GameManager>
    {
        public GameOverState(GameManager o) : base(o) { }
        public override string Name => "Game Over";
        public override Color DebugColor => new Color(1f, 0.3f, 0.3f);
        public override void Enter() => Visuals.Sfx.Play(Visuals.Sfx.Lose, 0.8f, 1f, 0f);
    }
}
