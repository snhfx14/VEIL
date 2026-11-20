using AulaFSM.Core;
using AulaFSM.Enemy;
using AulaFSM.Visuals;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace AulaFSM.Player
{
    /// <summary>
    /// Controlador do jogador (assassino). NÃO tem lógica de comportamento com "ifs" gigantes:
    /// lê o input, calcula o caminho e delega tudo na máquina de estados.
    ///
    /// Controlo: clicar/arrastar no chão para mover (com pathfinding à volta das caixas).
    /// Ao chegar perto de um guarda numa POSIÇÃO VÁLIDA (por trás/lado, sem paredes), o ataque é automático.
    ///
    /// Estados: Parado, Andar, Atacar, Ferido, Morto.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class PlayerController : MonoBehaviour, IStateMachineOwner
    {
        [Header("Movimento (clique do rato)")]
        public float moveSpeed = 6.5f;
        public float rotationSpeed = 900f;
        public float gravity = -25f;
        public float repathInterval = 0.08f;

        [Header("Ataque automático (stealth)")]
        public float killRange = 1.8f;
        public float attackDuration = 0.42f;
        [Tooltip("Dano da faca num ataque de frente/lado (elimina guardas com 100 de vida ou menos). Pelas costas é sempre instantâneo.")]
        public float knifeDamage = 100f;
        [Tooltip("Tolerância extra de distância ao revalidar o golpe no momento do impacto.")]
        public float hitValidationTolerance = 0.6f;
        [Tooltip("Ao clicar num guarda, o jogador tenta chegar a este ponto atrás dele.")]
        public float approachBehindDistance = 1.1f;

        [Header("Dano recebido")]
        public float hurtDuration = 0.25f;
        public float knockbackSpeed = 8f;
        public float invulnerabilityAfterHit = 0.15f; // curta: o fogo contínuo dos guardas acerta quase todos os tiros

        [Header("Referências visuais")]
        public Transform model;        // corpo (roda ao morrer)
        public Transform swordPivot;   // faca (roda ao atacar)
        public Renderer bodyRenderer;

        // ------------------------------------------------------------------ Dados partilhados com os estados
        public CharacterController Controller { get; private set; }
        public Health Health { get; private set; }
        public StateMachine StateMachine { get; private set; }
        public string DisplayName => "Player";

        public Vector3 MoveInput { get; private set; }       // direção até ao próximo ponto do caminho
        public bool HasMoveInput => _hasPath;
        public EnemyAI AttackTarget { get; private set; }    // guarda que pode ser eliminado agora (posição válida)
        public bool HasTargetInRange => AttackTarget != null;
        public Vector3 LastHitSource { get; private set; }

        /// <summary>Guarda em que o jogador clicou: persegue-o até o eliminar.</summary>
        public EnemyAI ChaseTarget { get; private set; }

        // Caminho atual (usado pelo PathVisual para desenhar o trajeto no chão)
        public Vector3[] PathCorners => _corners;
        public int PathCornerIndex => _cornerIndex;
        public Vector3 Destination => _hasPath ? _corners[_corners.Length - 1] : transform.position;

        /// <summary>Os guardas "ouvem" o jogador quando ele ataca.</summary>
        public bool IsMakingNoise => StateMachine.CurrentState == _attack;

        // Estados
        private PlayerIdleState _idle;
        private PlayerMoveState _move;
        private PlayerAttackState _attack;
        private PlayerHurtState _hurt;
        private PlayerDeadState _dead;

        // Caminho (NavMesh)
        private NavMeshPath _path;
        private Vector3[] _corners = new Vector3[0];
        private int _cornerIndex;
        private bool _hasPath;
        private float _repathTimer;
        private Vector3 _requestedDestination;

        // Deteção de bloqueio
        private const float CornerReachDistance = 0.3f;
        private const float StuckCheckInterval = 0.35f;
        private float _progressTimer;
        private Vector3 _lastProgressPos;
        private int _stuckRetries;

        private bool _hurtPending;
        private float _verticalVelocity;
        private Color _baseColor;
        private Renderer[] _modelRenderers;
        private bool _visible = true;

        private void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
            Health.Damaged += OnDamaged;
            if (bodyRenderer != null) _baseColor = bodyRenderer.material.color;
            _path = new NavMeshPath();

            BuildStateMachine();
        }

        /// <summary>
        /// TODO o "cérebro" do jogador está aqui: que estados existem e quando se muda entre eles.
        /// </summary>
        private void BuildStateMachine()
        {
            _idle = new PlayerIdleState(this);
            _move = new PlayerMoveState(this);
            _attack = new PlayerAttackState(this);
            _hurt = new PlayerHurtState(this);
            _dead = new PlayerDeadState(this);

            StateMachine = new StateMachine("Player");

            // Transições globais (prioridade máxima, por esta ordem)
            StateMachine.AddAnyTransition(_dead, () => Health.IsDead, "vida chegou a 0");
            StateMachine.AddAnyTransition(_hurt, () => !Health.IsDead && ConsumeHurt(), "levou um tiro");

            // Parado
            StateMachine.AddTransition(_idle, _attack, () => HasTargetInRange, "guarda vulnerável ao alcance");
            StateMachine.AddTransition(_idle, _move, () => HasMoveInput, "clicou no chão");

            // Andar
            StateMachine.AddTransition(_move, _attack, () => HasTargetInRange, "guarda vulnerável ao alcance");
            StateMachine.AddTransition(_move, _idle, () => !HasMoveInput, "chegou ao destino");

            // Atacar
            StateMachine.AddTransition(_attack, _idle, () => _attack.IsFinished, "golpe terminou");

            // Ferido
            StateMachine.AddTransition(_hurt, _idle, () => _hurt.IsFinished, "recuperou");

            StateMachine.SetState(_idle);
        }

        private void Update()
        {
            bool canAct = GameManager.Instance == null || GameManager.Instance.IsPlaying;
            ReadInput(canAct);
            FollowPath();
            FindAttackTarget(canAct);
            StateMachine.Tick(Time.deltaTime);
            UpdateBlink();
        }

        // ------------------------------------------------------------------ Input (rato)

        private void ReadInput(bool canAct)
        {
            var mouse = Mouse.current;

            if (!canAct || Health.IsDead)
            {
                ClearPath();
                ChaseTarget = null;
                return;
            }

            // O alvo morreu (ou desapareceu): pára a perseguição
            if (ChaseTarget != null && (ChaseTarget.Health.IsDead || !ChaseTarget.isActiveAndEnabled))
            {
                ChaseTarget = null;
                ClearPath();
            }

            if (mouse == null) return;

            _repathTimer -= Time.deltaTime;
            bool clicked = mouse.leftButton.wasPressedThisFrame;

            if (clicked && TryGetMouseGroundPoint(mouse.position.ReadValue(), out var clickPoint))
            {
                // Clicou num guarda? → perseguir até o eliminar
                var enemy = FindEnemyUnderCursor(mouse.position.ReadValue(), clickPoint);
                if (enemy != null)
                {
                    ChaseTarget = enemy;
                    _repathTimer = 0f;
                    ClickMarker.Spawn(enemy.transform.position, new Color(1f, 0.25f, 0.2f, 0.9f));
                    Sfx.Play(Sfx.Click, 0.5f, 1.3f);
                }
                else
                {
                    ChaseTarget = null;
                    if (SetDestination(clickPoint))
                    {
                        ClickMarker.Spawn(_corners[_corners.Length - 1], new Color(1f, 1f, 1f, 0.85f));
                        Sfx.Play(Sfx.Click, 0.35f);
                    }
                }
            }
            else if (ChaseTarget == null && mouse.leftButton.isPressed && _repathTimer <= 0f)
            {
                // Manter carregado e arrastar: o destino segue o rato
                _repathTimer = repathInterval;
                if (TryGetMouseGroundPoint(mouse.position.ReadValue(), out var dragPoint)) SetDestination(dragPoint);
            }

            // A perseguir: recalcula o caminho porque o guarda está a mexer-se.
            // O destino é um ponto ATRÁS do guarda (se for alcançável), para facilitar o ataque pelas costas.
            if (ChaseTarget != null && _repathTimer <= 0f)
            {
                _repathTimer = 0.12f;
                Vector3 behind = ChaseTarget.transform.position - ChaseTarget.transform.forward * approachBehindDistance;
                if (!SetDestination(behind)) SetDestination(ChaseTarget.transform.position);
            }
        }

        /// <summary>Guarda debaixo do cursor (raycast ao colisor ou perto do ponto clicado no chão).</summary>
        private static EnemyAI FindEnemyUnderCursor(Vector2 screenPos, Vector3 groundPoint)
        {
            var cam = Camera.main;
            if (cam != null && Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                var e = hit.collider.GetComponentInParent<EnemyAI>();
                if (e != null && !e.Health.IsDead) return e;
            }

            EnemyAI best = null;
            float bestDist = 1.4f; // tolerância para não ter de acertar em cheio
            foreach (var e in EnemyAI.All)
            {
                if (e == null || e.Health.IsDead) continue;
                Vector3 d = e.transform.position - groundPoint;
                d.y = 0f;
                if (d.magnitude < bestDist) { bestDist = d.magnitude; best = e; }
            }
            return best;
        }

        private static bool TryGetMouseGroundPoint(Vector2 screenPos, out Vector3 point)
        {
            point = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        /// <summary>
        /// Calcula o caminho pelo NavMesh até ao ponto clicado.
        /// O NavMesh só cobre o chão livre (as caixas são obstáculos), por isso o destino é sempre
        /// projetado para o ponto alcançável mais próximo e o caminho contorna as caixas.
        /// </summary>
        public bool SetDestination(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out var targetHit, 3f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(transform.position, out var selfHit, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.CalculatePath(selfHit.position, targetHit.position, NavMesh.AllAreas, _path)) return false;
            if (_path.status == NavMeshPathStatus.PathInvalid || _path.corners.Length < 2) return false;

            _corners = _path.corners;   // caminho parcial → termina no ponto alcançável mais próximo
            _cornerIndex = 1;
            _hasPath = true;
            _requestedDestination = point;
            _progressTimer = 0f;
            _lastProgressPos = transform.position;
            return true;
        }

        public void ClearPath()
        {
            _hasPath = false;
            MoveInput = Vector3.zero;
            _stuckRetries = 0;
        }

        private void FollowPath()
        {
            if (!_hasPath) { MoveInput = Vector3.zero; return; }

            Vector3 pos = transform.position;
            Vector3 delta = Flat(_corners[_cornerIndex] - pos);

            // Avança para o próximo canto quando chega perto OU quando já o ultrapassou
            // (evita pequenos movimentos de vaivém à volta do canto)
            while (delta.magnitude < CornerReachDistance || PassedCorner(pos))
            {
                _cornerIndex++;
                if (_cornerIndex >= _corners.Length) { ClearPath(); return; }
                delta = Flat(_corners[_cornerIndex] - pos);
            }
            MoveInput = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.zero;

            CheckStuck(pos);
        }

        private bool PassedCorner(Vector3 pos)
        {
            if (_cornerIndex >= _corners.Length - 1 || _cornerIndex < 1) return false;
            Vector3 corner = _corners[_cornerIndex];
            Vector3 incoming = Flat(corner - _corners[_cornerIndex - 1]);
            // Já ultrapassou o canto na direção em que vinha (e está mesmo ao lado dele)?
            return incoming.sqrMagnitude > 0.001f &&
                   Vector3.Dot(Flat(pos - corner), incoming) > 0f &&
                   Flat(pos - corner).magnitude < 0.6f;
        }

        /// <summary>
        /// Se o jogador deixar de progredir (ex.: empurrado contra um obstáculo), recalcula o caminho
        /// uma vez a partir da posição atual; se continuar sem progredir, pára (sem insistir).
        /// </summary>
        private void CheckStuck(Vector3 pos)
        {
            if (!(StateMachine.CurrentState is PlayerMoveState)) { _progressTimer = 0f; _lastProgressPos = pos; return; }
            _progressTimer += Time.deltaTime;
            if (_progressTimer < StuckCheckInterval) return;

            float moved = Flat(pos - _lastProgressPos).magnitude;
            _progressTimer = 0f;
            _lastProgressPos = pos;
            if (moved >= moveSpeed * StuckCheckInterval * 0.25f) { _stuckRetries = 0; return; }

            _stuckRetries++;
            if (_stuckRetries > 1 || !SetDestination(_requestedDestination)) ClearPath();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>
        /// Procura o guarda mais próximo que possa ser eliminado DAQUI:
        /// distância, ângulo (não pela frente), linha de visão e nível de alerta do guarda
        /// (as regras estão em EnemyAI.CanBeAssassinatedBy).
        /// </summary>
        private void FindAttackTarget(bool canAct)
        {
            AttackTarget = null;
            if (!canAct || Health.IsDead) return;
            float best = float.MaxValue;
            foreach (var enemy in EnemyAI.All)
            {
                if (enemy == null || enemy.Health.IsDead) continue;
                if (!enemy.CanBeAssassinatedBy(transform.position, killRange, 0f, out _)) continue;
                float dist = Vector3.Distance(enemy.transform.position, transform.position);
                if (dist < best) { best = dist; AttackTarget = enemy; }
            }
        }

        // ------------------------------------------------------------------ Condições

        private bool ConsumeHurt()
        {
            if (!_hurtPending) return false;
            _hurtPending = false;
            return true;
        }

        private void OnDamaged(float amount, Vector3 source)
        {
            LastHitSource = source;
            _hurtPending = true;
        }

        // ------------------------------------------------------------------ Ações usadas pelos estados

        /// <summary>Move o CharacterController com gravidade.</summary>
        public void Move(Vector3 horizontalVelocity, float deltaTime)
        {
            if (!Controller.enabled) return;
            _verticalVelocity = Controller.isGrounded ? -2f : _verticalVelocity + gravity * deltaTime;
            Vector3 velocity = horizontalVelocity + Vector3.up * _verticalVelocity;
            Controller.Move(velocity * deltaTime);
        }

        public void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * deltaTime);
        }

        public void SetBodyColor(Color color)
        {
            if (bodyRenderer != null) bodyRenderer.material.color = color;
        }

        public void ResetBodyColor() => SetBodyColor(_baseColor);

        /// <summary>Pisca enquanto está invulnerável depois de levar dano.</summary>
        private void UpdateBlink()
        {
            if (model == null) return;
            if (_modelRenderers == null) _modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            bool blink = Health.IsInvulnerable && !Health.IsDead;
            bool visible = !blink || Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
            if (visible == _visible) return;
            _visible = visible;
            foreach (var r in _modelRenderers) r.enabled = visible;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, killRange);
            if (_hasPath)
            {
                Gizmos.color = Color.cyan;
                for (int i = _cornerIndex; i < _corners.Length; i++)
                    Gizmos.DrawLine(i == _cornerIndex ? transform.position : _corners[i - 1], _corners[i]);
            }
        }
    }
}
