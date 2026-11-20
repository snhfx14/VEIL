using System.Collections.Generic;
using AulaFSM.Core;
using AulaFSM.Player;
using AulaFSM.Visuals;
using UnityEngine;
using UnityEngine.AI;

namespace AulaFSM.Enemy
{
    /// <summary>Níveis de alerta de um guarda (cada estado da FSM pertence a um nível).</summary>
    public enum AlertLevel { Normal, Suspeito, Alerta, Combate }

    /// <summary>Implementado pelos estados do guarda para dizer a que nível de alerta pertencem.</summary>
    public interface IAlertState
    {
        AlertLevel Level { get; }
    }

    /// <summary>
    /// NPC inimigo (guarda armado) controlado por uma máquina de estados.
    ///
    /// Níveis de alerta → estados:
    ///   Normal   : Patrulhar
    ///   Suspeito : Suspeito (investiga o que viu/ouviu)
    ///   Alerta   : Procurar, Investigar Cadáver, Fugir (testemunha → pede reforços)
    ///   Combate  : Perseguir, Disparar
    ///   (final)  : Morto
    ///
    /// O MonoBehaviour trata da PERCEÇÃO (visão progressiva, audição, cadáveres) e guarda dados;
    /// as DECISÕES estão nas transições e as AÇÕES em cada estado.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class EnemyAI : MonoBehaviour, IStateMachineOwner
    {
        public static readonly List<EnemyAI> All = new List<EnemyAI>();

        [Header("Tipo de guarda (atributos em FSM_GuardTypes)")]
        public GuardType type = GuardType.Normal;
        public GuardProfile profile = new GuardProfile();

        [Header("Perceção")]
        public float viewDistance = 9f;
        [Range(10f, 360f)] public float viewAngle = 90f;
        public float hearingRadius = 6f;
        public float eyeHeight = 1.2f;

        [Header("Velocidades base (multiplicadas pelo tipo)")]
        public float patrolSpeed = 2.2f;
        public float investigateSpeed = 2.6f;
        public float searchSpeed = 3.4f;
        public float chaseSpeed = 4.4f;
        public float fleeSpeed = 6f;

        [Header("Combate (dispara à distância) — NÃO ALTERADO")]
        public float attackRange = 9f;
        public float attackWindup = 0.18f;   // laser vermelho a apontar = aviso (curto!) antes do tiro
        public float attackRecover = 0.12f;  // intervalo até ao próximo tiro → ~3,3 tiros/segundo
        public float attackDamage = 25f;     // 4 tiros = morto
        public float bulletSpeed = 40f;

        [Header("Deteção progressiva")]
        [Tooltip("Segundos a ver o jogador AO PERTO, em estado Normal, até o detetar por completo.")]
        public float timeToConfirmTarget = 0.2f;
        [Range(0f, 1f), Tooltip("Consciência a partir da qual fica Suspeito.")]
        public float suspicionThreshold = 0.2f;
        [Tooltip("À distância máxima de visão, detetar demora (1 + este valor) vezes mais.")]
        public float distanceDetectionPenalty = 2f;
        [Tooltip("Abaixo desta distância a deteção é imediata (se o vir).")]
        public float instantDetectRange = 2f;
        [Tooltip("Quanto a consciência desce por segundo quando deixa de ver o jogador.")]
        public float awarenessDecay = 0.35f;
        public float suspiciousDetectionMultiplier = 1.5f;
        public float alertDetectionMultiplier = 2.5f;

        [Header("Tempos de reação / comportamento")]
        public float patrolWaitTime = 1.2f;
        public float suspiciousReactionDelay = 0.6f;
        public float investigateLookTime = 2.5f;
        public float suspiciousTimeout = 8f;
        public float loseTargetTime = 1.2f;
        public float searchDuration = 10f;
        public float searchRadius = 6f;
        public float corpseInspectTime = 1.5f;
        public float scaredDuration = 2f;
        [Tooltip("Depois de uma procura, continua mais atento durante este tempo (não volta logo ao normal).")]
        public float waryDuration = 20f;
        [Tooltip("Tempo mínimo entre avisos por rádio recebidos (evita reações repetidas).")]
        public float alertCooldown = 4f;

        [Header("Coordenação")]
        public float witnessRadius = 9f;
        public float allyAlertRadius = 10f;
        public float corpseAlertRadius = 16f;
        [Range(0.3f, 1f)] public float corpseSightMultiplier = 0.85f;

        [Header("Eliminações (de frente ou por trás)")]
        [Tooltip("A partir deste ângulo o ataque é pelas COSTAS → eliminação instantânea mesmo para guardas resistentes.")]
        public float backstabAngle = 115f;
        [Tooltip("Em Alerta/Combate o alcance para eliminar é multiplicado por este valor (tem de chegar mais perto).")]
        public float alertKillRangeMultiplier = 0.7f;

        [Header("Patrulha")]
        public Vector3[] patrolPoints;
        public bool pingPongPatrol;

        [Header("Referências")]
        public PlayerController target;
        public Renderer bodyRenderer;
        public Transform aimLaser;   // laser vermelho (escala Z = comprimento)
        public Light flashlight;     // lanterna (luz spot) = o campo de visão

        // ------------------------------------------------------------------ Dados partilhados com os estados
        public NavMeshAgent Agent { get; private set; }
        public Health Health { get; private set; }
        public StateMachine StateMachine { get; private set; }
        public string DisplayName { get; private set; }

        public bool CanSeeTarget { get; private set; }
        /// <summary>0 → não viu nada … 1 → detetou o jogador.</summary>
        public float Awareness { get; private set; }
        public float LastTimeSeenTarget { get; private set; } = -99f;
        public float LastTimeHeardSomething { get; private set; } = -99f;
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Vector3 LastHitSource { get; private set; }
        public Vector3 ScarePosition { get; private set; }
        public Vector3 SearchCenter { get; set; }
        public EnemyAI CorpseTarget { get; private set; }
        public int PatrolIndex { get; set; }
        public int PatrolDirection { get; set; } = 1;
        public float LookAroundSpeed { get; private set; } = 60f;

        public AlertLevel CurrentAlert => StateMachine?.CurrentState is IAlertState s ? s.Level : AlertLevel.Normal;
        public bool IsUnaware => CurrentAlert < AlertLevel.Combate && !CanSeeTarget;
        public bool IsWary => Time.time < _waryUntil;
        public bool IsScared => Time.time < _scaredUntil;

        public bool TargetAlive => target != null && !target.Health.IsDead && (GameManager.Instance == null || GameManager.Instance.IsPlaying);
        public bool HeardSomethingRecently => Time.time - LastTimeHeardSomething < 0.25f;
        public bool LostTarget => Time.time - LastTimeSeenTarget > loseTargetTime;
        public float DistanceToTarget => target != null ? Vector3.Distance(transform.position, target.transform.position) : float.MaxValue;

        // Valores efetivos (base × tipo × alerta da zona)
        public float Reaction => profile.reactionMultiplier * _personalReaction;
        public float EffectiveViewDistance => viewDistance * (1f + 0.08f * GuardSquad.ZoneAlert) * (IsWary ? 1.1f : 1f);
        public float EffectiveAttackRange => attackRange + profile.attackRangeBonus;
        public bool TargetInAttackRange => DistanceToTarget <= EffectiveAttackRange;
        public float Speed(float baseSpeed) => baseSpeed * profile.speedMultiplier * (1f + 0.06f * GuardSquad.ZoneAlert);

        // Estados
        private EnemyPatrolState _patrol;
        private EnemySuspiciousState _suspicious;
        private EnemyInvestigateCorpseState _corpse;
        private EnemySearchState _search;
        private EnemyChaseState _chase;
        private EnemyAttackState _attack;
        private EnemyFleeState _flee;
        private EnemyDeadState _dead;

        private bool _attackedPending, _alertPending;
        private float _scaredUntil = -99f, _waryUntil = -99f, _lastAlertTime = -99f;
        private float _personalReaction = 1f;
        private Vector3 _lastAlertPosition, _lastTargetPos, _targetVelocity;
        private readonly HashSet<EnemyAI> _knownCorpses = new HashSet<EnemyAI>();
        private readonly RaycastHit[] _rayHits = new RaycastHit[8];

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Health = GetComponent<Health>();
            Health.Damaged += OnDamaged;
            DisplayName = $"{profile.displayName} {All.Count + 1}";
            name = DisplayName;

            // Pequenas diferenças individuais: nem todos os guardas se comportam igual
            _personalReaction = Random.Range(0.85f, 1.15f);
            patrolWaitTime *= Random.Range(0.7f, 1.5f);
            LookAroundSpeed = Random.Range(40f, 90f);
            pingPongPatrol = pingPongPatrol || Random.value < 0.4f;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            if (target == null) target = FindFirstObjectByType<PlayerController>();
            if (target != null) _lastTargetPos = target.transform.position;
            SetLaser(false, 0f);
            BuildStateMachine();
        }

        /// <summary>
        /// O "cérebro" do NPC: todos os estados e as regras para mudar entre eles.
        /// A ORDEM importa: a primeira transição verdadeira é a escolhida.
        /// </summary>
        private void BuildStateMachine()
        {
            _patrol = new EnemyPatrolState(this);
            _suspicious = new EnemySuspiciousState(this);
            _corpse = new EnemyInvestigateCorpseState(this);
            _search = new EnemySearchState(this);
            _chase = new EnemyChaseState(this);
            _attack = new EnemyAttackState(this);
            _flee = new EnemyFleeState(this);
            _dead = new EnemyDeadState(this);

            StateMachine = new StateMachine(DisplayName);
            bool Detected() => Awareness >= 1f;

            // --- Globais ---
            StateMachine.AddAnyTransition(_dead, () => Health.IsDead, "foi eliminado");
            StateMachine.AddAnyTransition(_chase, () => !Health.IsDead && Consume(ref _attackedPending), "foi atacado");

            // --- NORMAL: Patrulhar ---
            StateMachine.AddTransition(_patrol, _chase, Detected, "detetou o jogador");
            StateMachine.AddTransition(_patrol, _flee, () => IsScared, "viu um colega morrer");
            StateMachine.AddTransition(_patrol, _corpse, () => CorpseTarget != null, "encontrou um cadáver");
            StateMachine.AddTransition(_patrol, _search, () => Consume(ref _alertPending), "alerta por rádio");
            StateMachine.AddTransition(_patrol, _suspicious, () => TargetAlive && (Awareness >= suspicionThreshold || HeardSomethingRecently), "viu/ouviu algo estranho");

            // --- SUSPEITO ---
            StateMachine.AddTransition(_suspicious, _chase, Detected, "confirmou o jogador");
            StateMachine.AddTransition(_suspicious, _flee, () => IsScared, "viu um colega morrer");
            StateMachine.AddTransition(_suspicious, _corpse, () => CorpseTarget != null, "encontrou um cadáver");
            StateMachine.AddTransition(_suspicious, _search, () => Consume(ref _alertPending), "alerta por rádio");
            StateMachine.AddTransition(_suspicious, _patrol, () => _suspicious.IsFinished || !TargetAlive, "não encontrou nada");

            // --- ALERTA: Investigar cadáver ---
            StateMachine.AddTransition(_corpse, _chase, Detected, "detetou o jogador");
            StateMachine.AddTransition(_corpse, _search, () => _corpse.IsFinished, "cadáver → procurar o assassino");

            // --- ALERTA: Procurar ---
            StateMachine.AddTransition(_search, _chase, Detected, "encontrou o jogador");
            StateMachine.AddTransition(_search, _corpse, () => CorpseTarget != null, "encontrou outro cadáver");
            StateMachine.AddTransition(_search, _patrol, () => _search.IsFinished || !TargetAlive, "desistiu (fica mais atento)");

            // --- ALERTA: Fugir (testemunha) ---
            StateMachine.AddTransition(_flee, _chase, Detected, "viu o assassino");
            StateMachine.AddTransition(_flee, _search, () => !IsScared, "acalmou-se → procurar");

            // --- COMBATE: Perseguir ---
            StateMachine.AddTransition(_chase, _attack, () => TargetInAttackRange && CanSeeTarget, "jogador na mira");
            StateMachine.AddTransition(_chase, _search, () => LostTarget, "perdeu o contacto visual");
            StateMachine.AddTransition(_chase, _patrol, () => !TargetAlive, "alvo indisponível");

            // --- COMBATE: Disparar ---
            StateMachine.AddTransition(_attack, _chase, () => _attack.IsFinished && TargetAlive, "disparou");
            StateMachine.AddTransition(_attack, _patrol, () => _attack.IsFinished, "disparou");

            StateMachine.SetState(_patrol);
        }

        private void Update()
        {
            if (StateMachine == null) return;
            if (!Health.IsDead) UpdatePerception();
            StateMachine.Tick(Time.deltaTime);
            if (bodyRenderer != null && !Health.IsDead && StateMachine.CurrentState is IState s && !(s is EnemyAttackState))
                bodyRenderer.material.color = Color.Lerp(bodyRenderer.material.color, s.DebugColor, Time.deltaTime * 10f);
        }

        // ------------------------------------------------------------------ Perceção

        private void UpdatePerception()
        {
            float dt = Time.deltaTime;

            // Velocidade do jogador (usada só pelo Atirador para antecipar o movimento)
            if (target != null && dt > 0f)
            {
                _targetVelocity = Vector3.Lerp(_targetVelocity, (target.transform.position - _lastTargetPos) / dt, dt * 10f);
                _lastTargetPos = target.transform.position;
            }

            CanSeeTarget = TargetAlive && CheckVision();
            if (CanSeeTarget)
            {
                LastTimeSeenTarget = Time.time;
                LastKnownTargetPosition = target.transform.position;

                // Deteção PROGRESSIVA: mais rápida ao perto, em estados de alerta, e para guardas mais reativos
                float dist = DistanceToTarget;
                float distanceFactor = 1f + distanceDetectionPenalty * Mathf.Clamp01(dist / EffectiveViewDistance);
                float levelFactor = CurrentAlert == AlertLevel.Suspeito ? suspiciousDetectionMultiplier
                                  : CurrentAlert >= AlertLevel.Alerta ? alertDetectionMultiplier : 1f;
                if (IsWary) levelFactor *= 1.3f;
                float rate = Reaction * levelFactor / (Mathf.Max(0.05f, timeToConfirmTarget) * distanceFactor);
                Awareness = Mathf.Min(1f, Awareness + rate * dt);
                if (dist <= instantDetectRange) Awareness = 1f;
            }
            else
            {
                float decay = CurrentAlert >= AlertLevel.Alerta ? awarenessDecay * 0.5f : awarenessDecay;
                Awareness = Mathf.Max(0f, Awareness - decay * dt);
            }

            // Ouvir: os ataques do jogador fazem barulho
            if (TargetAlive && target.IsMakingNoise && DistanceToTarget <= hearingRadius)
                Hear(target.transform.position);

            if (CurrentAlert < AlertLevel.Combate && CorpseTarget == null) LookForCorpses();
        }

        private bool CheckVision()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = target.transform.position + Vector3.up * 1f;
            return CanSeePoint(eye, targetPoint, EffectiveViewDistance, true);
        }

        /// <summary>Distância + cone de visão + linha de visão (as caixas tapam).</summary>
        private bool CanSeePoint(Vector3 eye, Vector3 point, float maxDistance, bool pointIsPlayer)
        {
            Vector3 to = point - eye;
            float distance = to.magnitude;
            if (distance > maxDistance) return false;

            // Fora do cone de visão não vê (nem que esteja mesmo atrás dele → assassinato furtivo!)
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

            return pointIsPlayer ? FirstHitIsTarget(eye, to / distance, distance + 0.5f)
                                 : ClearDistance(eye, to / distance, distance, true) >= distance - 0.2f;
        }

        private bool FirstHitIsTarget(Vector3 origin, Vector3 dir, float maxDistance)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, _rayHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            Transform closestHit = null;
            for (int i = 0; i < count; i++)
            {
                var h = _rayHits[i];
                if (h.collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (h.distance < closest) { closest = h.distance; closestHit = h.collider.transform; }
            }
            return closestHit != null && closestHit.GetComponentInParent<PlayerController>() == target;
        }

        /// <summary>Distância livre até ao primeiro obstáculo (ignora guardas; opcionalmente ignora o jogador).</summary>
        public float ClearDistance(Vector3 origin, Vector3 dir, float max, bool ignorePlayer = false)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, _rayHits, max, ~0, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < count; i++)
            {
                var col = _rayHits[i].collider;
                if (col.GetComponentInParent<EnemyAI>() != null) continue;
                if (ignorePlayer && col.GetComponentInParent<PlayerController>() != null) continue;
                if (_rayHits[i].distance < best) best = _rayHits[i].distance;
            }
            return best;
        }

        private bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            return len < 0.01f || ClearDistance(from, d / len, len, true) >= len - 0.05f;
        }

        public void Hear(Vector3 position)
        {
            LastTimeHeardSomething = Time.time;
            LastKnownTargetPosition = position;
        }

        // ------------------------------------------------------------------ Cadáveres e coordenação

        private void LookForCorpses()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            foreach (var other in All)
            {
                if (other == this || other == null || !other.Health.IsDead || _knownCorpses.Contains(other)) continue;
                if (!CanSeePoint(eye, other.transform.position + Vector3.up * 0.4f, EffectiveViewDistance * corpseSightMultiplier, false)) continue;

                _knownCorpses.Add(other);
                // Cadáver que a zona já conhece: não repete a reação completa
                if (!GuardSquad.IsDiscovered(other)) CorpseTarget = other;
            }
        }

        /// <summary>Chamado no fim da investigação: regista o cadáver e avisa os colegas por rádio.</summary>
        public void ReportCorpse(EnemyAI corpse)
        {
            SearchCenter = corpse.transform.position;
            if (!GuardSquad.RegisterCorpse(corpse)) return; // outro guarda já tinha dado o alerta
            Popups.Show(transform.position + Vector3.up * 2.8f, "CORPO!", new Color(1f, 0.5f, 0.2f), 32);
            Sfx.Play(Sfx.Alert, 0.6f, 0.8f);
            GuardSquad.Broadcast(this, corpse.transform.position, corpseAlertRadius);
        }

        public void ClearCorpseTarget() => CorpseTarget = null;

        /// <summary>Aviso por rádio de um colega. Devolve false se for ignorado.</summary>
        public bool ReceiveAlert(Vector3 position)
        {
            if (Health.IsDead || CurrentAlert == AlertLevel.Combate) return false;
            bool sameArea = Vector3.Distance(position, _lastAlertPosition) < 3f;
            if (sameArea && Time.time - _lastAlertTime < alertCooldown) return false;
            _lastAlertTime = Time.time;
            _lastAlertPosition = position;
            SearchCenter = position;
            _alertPending = true;
            return true;
        }

        public bool ConsumeAlert() => Consume(ref _alertPending);

        /// <summary>Quando um guarda entra em combate, chama os colegas próximos para procurarem ali.</summary>
        public void AlertAllies() => GuardSquad.Broadcast(this, LastKnownTargetPosition, allyAlertRadius);

        /// <summary>Quando morre, os colegas que estejam a ver ficam com medo e pedem reforços.</summary>
        public void ScareWitnesses()
        {
            foreach (var ally in All)
            {
                if (ally == this || ally == null || ally.Health.IsDead) continue;
                Vector3 allyEye = ally.transform.position + Vector3.up * ally.eyeHeight;
                if (!ally.CanSeePoint(allyEye, transform.position + Vector3.up * 1f, ally.witnessRadius, false)) continue;
                ally._knownCorpses.Add(this);
                GuardSquad.RegisterCorpse(this);
                ally.Scare(transform.position);
            }
        }

        public void Scare(Vector3 position)
        {
            if (Health.IsDead) return;
            _scaredUntil = Time.time + scaredDuration;
            ScarePosition = position;
            LastKnownTargetPosition = position;
            SearchCenter = position;
        }

        /// <summary>Depois de um alerta não volta logo ao normal: fica mais atento durante algum tempo.</summary>
        public void BecomeWary() => _waryUntil = Time.time + waryDuration;

        // ------------------------------------------------------------------ Eliminação stealth

        /// <summary>
        /// Pode ser eliminado por alguém nesta posição? (de FRENTE ou por TRÁS — a direção do guarda não é requisito)
        ///  - tem de estar dentro do alcance (menor se o guarda estiver em Alerta/Combate);
        ///  - sem paredes/caixas entre os dois.
        /// fromBehind = true → eliminação instantânea; de frente/lado → dano da faca.
        /// </summary>
        public bool CanBeAssassinatedBy(Vector3 attackerPos, float range, float tolerance, out bool fromBehind)
        {
            fromBehind = false;
            if (Health.IsDead) return false;

            Vector3 toAttacker = attackerPos - transform.position;
            toAttacker.y = 0f;
            float dist = toAttacker.magnitude;

            bool alerted = CurrentAlert >= AlertLevel.Alerta;
            float maxRange = (alerted ? range * alertKillRangeMultiplier : range) + tolerance;
            if (dist > maxRange) return false;
            if (!ClearLine(attackerPos + Vector3.up, transform.position + Vector3.up)) return false;

            float angle = Vector3.Angle(transform.forward, toAttacker); // 0 = mesmo à frente, 180 = mesmo atrás
            fromBehind = angle >= backstabAngle;
            return true;
        }

        /// <summary>O guarda virou-se a tempo: o ataque falhou e ele percebe tudo.</summary>
        public void OnAssassinationFailed(Vector3 attackerPos)
        {
            Awareness = 1f;
            LastKnownTargetPosition = attackerPos;
            LastTimeSeenTarget = Time.time;
            _attackedPending = true;
        }

        // ------------------------------------------------------------------ Combate

        /// <summary>Golpe do jogador (dano da faca ou eliminação instantânea pelas costas).</summary>
        public void ReceiveHit(float damage, Vector3 sourcePosition)
        {
            LastHitSource = sourcePosition;
            Health.TakeDamage(damage, sourcePosition);
        }

        private void OnDamaged(float amount, Vector3 source)
        {
            if (Health.IsDead) return;
            // Sobreviveu (ex.: guarda pesado): sabe exatamente onde está o atacante
            Awareness = 1f;
            LastKnownTargetPosition = source;
            LastTimeSeenTarget = Time.time;
            _attackedPending = true;
        }

        public Vector3 MuzzlePosition => transform.position + Vector3.up * 1.17f + transform.forward * 1.1f + transform.right * 0.15f;

        public void Shoot()
        {
            if (target == null) return;
            Vector3 muzzle = MuzzlePosition;
            Vector3 aim = target.transform.position + Vector3.up * 1.1f;
            // Atirador: antecipa o movimento do jogador (os outros tipos têm aimLead = 0 → igual ao original)
            if (profile.aimLead > 0f)
                aim += _targetVelocity * (Vector3.Distance(muzzle, aim) / bulletSpeed) * profile.aimLead;
            Vector3 dir = aim - muzzle;
            dir.y = 0f; // tiro horizontal
            Bullet.Spawn(muzzle, dir.normalized, bulletSpeed, attackDamage);
            Gfx.Burst(muzzle, new Color(1f, 0.85f, 0.3f), 5, 3f, 0.1f); // clarão do disparo
            Sfx.Play(Sfx.Gun, 0.55f);
            Gfx.Flash(muzzle, new Color(1f, 0.8f, 0.4f), 7f, 6f, 0.12f); // o disparo ilumina a sala
        }

        public void SetLaser(bool visible, float length)
        {
            if (aimLaser == null) return;
            aimLaser.gameObject.SetActive(visible);
            if (visible) aimLaser.localScale = new Vector3(1f, 1f, Mathf.Max(0.1f, length));
        }

        // ------------------------------------------------------------------ Utilitários para os estados

        private static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

        public void FaceTowards(Vector3 position, float degreesPerSecond, float dt)
        {
            Vector3 dir = position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degreesPerSecond * dt);
        }

        public bool ReachedDestination =>
            Agent.enabled && !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;

        public void MoveTo(Vector3 destination, float speed)
        {
            if (!Agent.enabled || !Agent.isOnNavMesh) return;
            Agent.isStopped = false;
            Agent.speed = speed;
            Agent.SetDestination(destination);
        }

        public void StopMoving()
        {
            if (!Agent.enabled || !Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        public static bool RandomPointNear(Vector3 center, float radius, out Vector3 result)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 random = center + new Vector3(Random.Range(-radius, radius), 0f, Random.Range(-radius, radius));
                if (NavMesh.SamplePosition(random, out var hit, 2f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = center;
            return false;
        }

        // ------------------------------------------------------------------ Gizmos (cone de visão no Scene view)

        private void OnDrawGizmos()
        {
            Color c = StateMachine?.CurrentState?.DebugColor ?? Color.green;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.8f);
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            float dist = Application.isPlaying ? EffectiveViewDistance : viewDistance;
            Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
            Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawLine(eye, eye + left * dist);
            Gizmos.DrawLine(eye, eye + right * dist);
            // Zona "pelas costas" (eliminação instantânea)
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.4f);
            Vector3 bl = Quaternion.Euler(0, -backstabAngle, 0) * transform.forward;
            Vector3 br = Quaternion.Euler(0, backstabAngle, 0) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + bl * 2f);
            Gizmos.DrawLine(transform.position, transform.position + br * 2f);
        }
    }
}
