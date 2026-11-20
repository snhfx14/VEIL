using AulaFSM.Core;
using AulaFSM.Visuals;
using UnityEngine;

namespace AulaFSM.Enemy
{
    // =====================================================================
    //  [NORMAL] PATRULHAR — segue o percurso (em ciclo ou vai-e-volta) e olha à volta nos pontos.
    //  Depois de um alerta fica "atento": anda mais depressa e olha mais à volta.
    // =====================================================================
    public class EnemyPatrolState : State<EnemyAI>, IAlertState
    {
        private float _waitTimer;

        public EnemyPatrolState(EnemyAI owner) : base(owner) { }
        public override string Name => Owner.IsWary ? "Patrulhar (atento)" : "Patrulhar";
        public override Color DebugColor => Owner.IsWary ? new Color(0.55f, 0.85f, 0.3f) : new Color(0.3f, 0.85f, 0.35f);
        public AlertLevel Level => AlertLevel.Normal;

        public override void Enter()
        {
            _waitTimer = 0f;
            GoToCurrentPoint();
        }

        public override void Tick(float dt)
        {
            var points = Owner.patrolPoints;
            if (points == null || points.Length == 0)
            {
                Owner.transform.Rotate(0f, Owner.LookAroundSpeed * dt, 0f); // posto fixo: roda devagar
                return;
            }
            if (!Owner.ReachedDestination) return;

            // Posto fixo: roda devagar sobre si próprio (há momentos em que fica de costas)
            if (points.Length == 1)
            {
                Owner.transform.Rotate(0f, Owner.LookAroundSpeed * 0.8f * (Owner.IsWary ? 1.5f : 1f) * dt, 0f);
                return;
            }

            // Parado num ponto: olha à volta (mais tempo e mais rápido se estiver atento)
            _waitTimer += dt;
            float wary = Owner.IsWary ? 1.6f : 1f;
            Owner.transform.Rotate(0f, Mathf.Sin(_waitTimer * 2f) * Owner.LookAroundSpeed * wary * dt, 0f);
            if (_waitTimer >= Owner.patrolWaitTime * wary)
            {
                _waitTimer = 0f;
                NextPoint(points.Length);
                GoToCurrentPoint();
            }
        }

        private void NextPoint(int count)
        {
            if (count < 2) return;
            if (!Owner.pingPongPatrol) { Owner.PatrolIndex = (Owner.PatrolIndex + 1) % count; return; }
            int next = Owner.PatrolIndex + Owner.PatrolDirection;
            if (next < 0 || next >= count) { Owner.PatrolDirection = -Owner.PatrolDirection; next = Owner.PatrolIndex + Owner.PatrolDirection; }
            Owner.PatrolIndex = next;
        }

        private void GoToCurrentPoint()
        {
            var points = Owner.patrolPoints;
            if (points == null || points.Length == 0) return;
            Owner.PatrolIndex = Mathf.Clamp(Owner.PatrolIndex, 0, points.Length - 1);
            Owner.MoveTo(points[Owner.PatrolIndex], Owner.Speed(Owner.patrolSpeed) * (Owner.IsWary ? 1.15f : 1f));
        }
    }

    // =====================================================================
    //  [SUSPEITO] — viu/ouviu algo estranho:
    //  1) pára e olha (tempo de reação)  2) vai ver o local devagar  3) olha à volta  → volta a patrulhar
    //  Se continuar a ver o jogador, a consciência sobe mais depressa → Combate.
    // =====================================================================
    public class EnemySuspiciousState : State<EnemyAI>, IAlertState
    {
        private enum Phase { React, Walk, LookAround }
        private Phase _phase;
        private float _timer, _phaseTimer;
        private Vector3 _point;

        public EnemySuspiciousState(EnemyAI owner) : base(owner) { }
        public override string Name => "Suspeito ?";
        public override Color DebugColor => new Color(1f, 0.9f, 0.2f);
        public AlertLevel Level => AlertLevel.Suspeito;
        public bool IsFinished { get; private set; }

        public override void Enter()
        {
            _timer = _phaseTimer = 0f;
            _phase = Phase.React;
            IsFinished = false;
            _point = Owner.LastKnownTargetPosition;
            Owner.StopMoving();
            Popups.Show(Owner.transform.position + Vector3.up * 2.8f, "?", new Color(1f, 0.9f, 0.2f), 44);
            Sfx.Play(Sfx.Huh, 0.5f);
        }

        public override void Tick(float dt)
        {
            _timer += dt;
            _phaseTimer += dt;

            // Se voltar a ver/ouvir algo, atualiza o ponto a investigar
            if (Owner.CanSeeTarget || Owner.HeardSomethingRecently) _point = Owner.LastKnownTargetPosition;

            switch (_phase)
            {
                case Phase.React:
                    Owner.FaceTowards(_point, 300f, dt);
                    if (_phaseTimer >= Owner.suspiciousReactionDelay / Owner.Reaction)
                    {
                        _phase = Phase.Walk;
                        _phaseTimer = 0f;
                        Owner.MoveTo(_point, Owner.Speed(Owner.investigateSpeed));
                    }
                    break;

                case Phase.Walk:
                    if (Owner.ReachedDestination) { _phase = Phase.LookAround; _phaseTimer = 0f; }
                    else if (_phaseTimer > 0.5f) { _phaseTimer = 0f; Owner.MoveTo(_point, Owner.Speed(Owner.investigateSpeed)); }
                    break;

                case Phase.LookAround:
                    Owner.transform.Rotate(0f, Mathf.Sin(_phaseTimer * 2.5f) * 140f * dt, 0f);
                    if (_phaseTimer >= Owner.investigateLookTime) IsFinished = true;
                    break;
            }
            if (_timer >= Owner.suspiciousTimeout) IsFinished = true;
        }
    }

    // =====================================================================
    //  [ALERTA] INVESTIGAR CADÁVER — aproxima-se do corpo, examina-o, dá o alerta por rádio.
    // =====================================================================
    public class EnemyInvestigateCorpseState : State<EnemyAI>, IAlertState
    {
        private EnemyAI _corpse;
        private float _inspectTimer, _repath;
        private bool _arrived;

        public EnemyInvestigateCorpseState(EnemyAI owner) : base(owner) { }
        public override string Name => "Investigar Corpo";
        public override Color DebugColor => new Color(1f, 0.6f, 0.2f);
        public AlertLevel Level => AlertLevel.Alerta;
        public bool IsFinished { get; private set; }

        public override void Enter()
        {
            _corpse = Owner.CorpseTarget;
            _inspectTimer = _repath = 0f;
            _arrived = false;
            IsFinished = _corpse == null;
            Popups.Show(Owner.transform.position + Vector3.up * 2.8f, "?!", new Color(1f, 0.6f, 0.2f), 40);
            Sfx.Play(Sfx.Huh, 0.6f, 0.8f);
        }

        public override void Tick(float dt)
        {
            if (IsFinished || _corpse == null) { IsFinished = true; return; }
            Vector3 corpsePos = _corpse.transform.position;

            if (!_arrived)
            {
                _repath -= dt;
                if (_repath <= 0f) { _repath = 0.5f; Owner.MoveTo(corpsePos, Owner.Speed(Owner.investigateSpeed) * 1.2f); }
                if (Vector3.Distance(Owner.transform.position, corpsePos) < 1.8f) { _arrived = true; Owner.StopMoving(); }
                return;
            }

            // Examina o corpo e depois dá o alerta
            Owner.FaceTowards(corpsePos, 200f, dt);
            _inspectTimer += dt;
            if (_inspectTimer >= Owner.corpseInspectTime)
            {
                Owner.ReportCorpse(_corpse);
                IsFinished = true;
            }
        }

        public override void Exit() => Owner.ClearCorpseTarget();
    }

    // =====================================================================
    //  [ALERTA] PROCURAR — sabe que há uma ameaça: procura ativamente à volta do centro da procura.
    //  Os pontos são reservados no GuardSquad → os guardas espalham-se em vez de irem ao mesmo sítio.
    //  Ao desistir não volta logo ao normal: fica "atento" durante algum tempo.
    // =====================================================================
    public class EnemySearchState : State<EnemyAI>, IAlertState
    {
        private float _timer, _lookTimer, _duration;
        private Vector3 _center;
        private bool _looking;

        public EnemySearchState(EnemyAI owner) : base(owner) { }
        public override string Name => "Procurar";
        public override Color DebugColor => new Color(0.65f, 0.35f, 1f);
        public AlertLevel Level => AlertLevel.Alerta;
        public bool IsFinished => _timer >= _duration;

        public override void Enter()
        {
            _timer = 0f;
            _duration = Owner.searchDuration + 2f * GuardSquad.ZoneAlert;
            _center = Owner.SearchCenter;
            // Primeiro vai diretamente ao ponto suspeito
            _looking = false;
            Owner.MoveTo(_center, Owner.Speed(Owner.searchSpeed));
        }

        public override void Tick(float dt)
        {
            _timer += dt;

            // Novo aviso por rádio durante a procura → muda o centro da procura
            if (Owner.ConsumeAlert())
            {
                _center = Owner.SearchCenter;
                _timer = Mathf.Min(_timer, _duration * 0.3f);
                _looking = false;
                Owner.MoveTo(_center, Owner.Speed(Owner.searchSpeed));
            }

            if (!_looking)
            {
                if (Owner.ReachedDestination) { _looking = true; _lookTimer = 0f; Owner.StopMoving(); }
                return;
            }

            // Olha à volta e depois vai espreitar outro ponto (coordenado com os colegas)
            _lookTimer += dt;
            Owner.transform.Rotate(0f, 150f * dt, 0f);
            if (_lookTimer >= 1.2f / Owner.Reaction)
            {
                _looking = false;
                Vector3 p = GuardSquad.ClaimSearchPoint(Owner, _center, Owner.searchRadius, 3f);
                Owner.MoveTo(p, Owner.Speed(Owner.searchSpeed));
            }
        }

        public override void Exit()
        {
            GuardSquad.ReleaseSearchPoint(Owner);
            Owner.BecomeWary();
        }
    }

    // =====================================================================
    //  [COMBATE] PERSEGUIR — corre atrás do jogador e chama os colegas por rádio.
    // =====================================================================
    public class EnemyChaseState : State<EnemyAI>, IAlertState
    {
        private const float RepathInterval = 0.2f;
        private float _repathTimer;

        public EnemyChaseState(EnemyAI owner) : base(owner) { }
        public override string Name => "Perseguir !";
        public override Color DebugColor => new Color(1f, 0.5f, 0.1f);
        public AlertLevel Level => AlertLevel.Combate;

        public override void Enter()
        {
            _repathTimer = 0f;

            // Só conta como NOVA deteção quando entra em combate vindo de fora do combate
            // (o ciclo Disparar → Perseguir entre tiros não volta a alertar nem a contar).
            bool wasInCombat = Owner.StateMachine.PreviousState is IAlertState prev && prev.Level == AlertLevel.Combate;
            if (wasInCombat) return;

            Owner.AlertAllies();
            GameManager.Instance?.RegisterDetected();
            Popups.Show(Owner.transform.position + Vector3.up * 2.8f, "!", new Color(1f, 0.3f, 0.2f), 52);
            Sfx.Play(Sfx.Alert, 0.6f);
        }

        public override void Tick(float dt)
        {
            Owner.SearchCenter = Owner.LastKnownTargetPosition; // se o perder, procura a partir daqui
            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;
                Owner.MoveTo(Owner.LastKnownTargetPosition, Owner.Speed(Owner.chaseSpeed));
            }
            if (Owner.CanSeeTarget)
                Owner.FaceTowards(Owner.LastKnownTargetPosition, 360f, dt);
        }
    }

    // =====================================================================
    //  [COMBATE] DISPARAR — aponta o laser vermelho (aviso), dispara, recupera.
    //  (sistema de disparo inalterado)
    // =====================================================================
    public class EnemyAttackState : State<EnemyAI>, IAlertState
    {
        private float _timer;
        private bool _hasShot;

        public EnemyAttackState(EnemyAI owner) : base(owner) { }
        public override string Name => "Disparar";
        public override Color DebugColor => new Color(0.95f, 0.15f, 0.15f);
        public AlertLevel Level => AlertLevel.Combate;
        public bool IsFinished => _timer >= Owner.attackWindup + Owner.attackRecover;

        public override void Enter()
        {
            _timer = 0f;
            _hasShot = false;
            Owner.StopMoving();
        }

        public override void Tick(float dt)
        {
            _timer += dt;

            if (_timer < Owner.attackWindup)
            {
                // Aponta durante 70% da preparação, depois "tranca" a mira (dá para desviar!)
                if (_timer < Owner.attackWindup * 0.7f && Owner.target != null)
                    Owner.FaceTowards(Owner.target.transform.position, 720f, dt);

                // Laser de mira até à primeira parede
                Vector3 origin = Owner.MuzzlePosition;
                float length = Owner.ClearDistance(origin, Owner.transform.forward, Owner.EffectiveAttackRange + 3f);
                Owner.SetLaser(true, length);

                float blink = Mathf.PingPong(_timer * 12f, 1f);
                SetColor(Color.Lerp(DebugColor, Color.white, blink));
            }
            else
            {
                if (!_hasShot)
                {
                    _hasShot = true;
                    Owner.SetLaser(false, 0f);
                    Owner.Shoot();
                }
                SetColor(DebugColor);
            }
        }

        public override void Exit() => Owner.SetLaser(false, 0f);

        private void SetColor(Color c)
        {
            if (Owner.bodyRenderer != null) Owner.bodyRenderer.material.color = c;
        }
    }

    // =====================================================================
    //  [ALERTA] FUGIR — testemunhou uma eliminação: recua e pede reforços por rádio,
    //  depois vai procurar o assassino.
    // =====================================================================
    public class EnemyFleeState : State<EnemyAI>, IAlertState
    {
        private float _repathTimer;

        public EnemyFleeState(EnemyAI owner) : base(owner) { }
        public override string Name => "Recuar !!";
        public override Color DebugColor => new Color(0.25f, 0.45f, 1f);
        public AlertLevel Level => AlertLevel.Alerta;

        public override void Enter()
        {
            _repathTimer = 0f;
            Popups.Show(Owner.transform.position + Vector3.up * 2.8f, "REFORÇOS!", new Color(0.45f, 0.65f, 1f), 28);
            Sfx.Play(Sfx.Huh, 0.6f, 1.6f);
            GuardSquad.Broadcast(Owner, Owner.ScarePosition, Owner.corpseAlertRadius);
        }

        public override void Tick(float dt)
        {
            _repathTimer -= dt;
            if (_repathTimer > 0f && !Owner.ReachedDestination) return;
            _repathTimer = 0.7f;

            // Recua para longe do jogador (se o vê) ou do sítio onde o colega morreu
            Vector3 threat = Owner.CanSeeTarget && Owner.target != null ? Owner.target.transform.position : Owner.ScarePosition;
            Vector3 away = Owner.transform.position - threat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -Owner.transform.forward;

            Vector3 desired = Owner.transform.position + away.normalized * 6f;
            if (EnemyAI.RandomPointNear(desired, 2f, out var point) ||
                EnemyAI.RandomPointNear(Owner.transform.position, 6f, out point))
                Owner.MoveTo(point, Owner.Speed(Owner.fleeSpeed));
        }

        public override void Exit() => Owner.SearchCenter = Owner.ScarePosition;
    }

    // =====================================================================
    //  MORTO — estado final: cai, deixa sangue, assusta quem viu.
    //  O corpo FICA no chão para poder ser descoberto pelos outros guardas.
    // =====================================================================
    public class EnemyDeadState : State<EnemyAI>
    {
        private const float FallTime = 0.4f;
        private const float SlideTime = 0.3f;
        private float _timer;
        private Transform _model;
        private Quaternion _modelStartWorld;
        private Vector3 _modelStartLocalPos, _startPos, _away, _fallAxis;

        public EnemyDeadState(EnemyAI owner) : base(owner) { }
        public override string Name => "Morto";
        public override Color DebugColor => new Color(0.35f, 0.35f, 0.35f);

        public override void Enter()
        {
            _timer = 0f;
            Owner.StopMoving();
            Owner.SetLaser(false, 0f);
            Owner.Agent.enabled = false;
            foreach (var col in Owner.GetComponentsInChildren<Collider>()) col.enabled = false;
            GuardSquad.ReleaseSearchPoint(Owner);

            GameManager.Instance?.RegisterEnemyKilled();
            Owner.ScareWitnesses();

            // Direção do golpe → o corpo é projetado e cai para esse lado
            _away = Owner.transform.position - Owner.LastHitSource;
            _away.y = 0f;
            _away = _away.sqrMagnitude > 0.01f ? _away.normalized : -Owner.transform.forward;
            _fallAxis = Vector3.Cross(Vector3.up, _away);
            _startPos = Owner.transform.position;

            _model = Owner.transform.Find("Model");
            if (_model != null)
            {
                _modelStartWorld = _model.rotation;
                _modelStartLocalPos = _model.localPosition;

                // A arma salta da mão
                var weapon = _model.Find("WeaponPivot");
                if (weapon != null)
                {
                    weapon.SetParent(null, true);
                    weapon.gameObject.AddComponent<Debris>().Launch(_away * 3f + Vector3.up * 5f + Random.insideUnitSphere);
                }
            }
            if (Owner.bodyRenderer != null) Owner.bodyRenderer.material.color = Color.white; // flash do impacto
        }

        public override void Tick(float dt)
        {
            if (_timer > FallTime + 0.3f) return; // animação terminada: o corpo fica parado no chão
            _timer += dt;

            // Projetado para trás (desliza e trava)
            float s = Mathf.Clamp01(_timer / SlideTime);
            Owner.transform.position = _startPos + _away * (1.6f * (1f - (1f - s) * (1f - s)));

            // Cai na direção do golpe, com um pequeno "ressalto"
            if (_model != null)
            {
                float t = Mathf.Clamp01(_timer / FallTime);
                float fall = t < 1f ? EaseInBack(t) : 1f;
                float bounce = _timer > FallTime ? Mathf.Sin(Mathf.Min(1f, (_timer - FallTime) / 0.2f) * Mathf.PI) * 6f : 0f;
                _model.rotation = Quaternion.AngleAxis(90f * fall - bounce, _fallAxis) * _modelStartWorld;
                _model.localPosition = Vector3.Lerp(_modelStartLocalPos, _modelStartLocalPos + Vector3.up * 0.3f, t);
            }
            if (Owner.bodyRenderer != null)
                Owner.bodyRenderer.material.color = Color.Lerp(Owner.bodyRenderer.material.color, DebugColor, 0.5f);
        }

        private static float EaseInBack(float x) => x * x * (2.2f * x - 1.2f);
    }
}
