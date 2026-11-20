using AulaFSM.Core;
using AulaFSM.Enemy;
using AulaFSM.Visuals;
using UnityEngine;

namespace AulaFSM.Player
{
    // =====================================================================
    //  PARADO — não faz nada, só aplica gravidade.
    // =====================================================================
    public class PlayerIdleState : State<PlayerController>
    {
        public PlayerIdleState(PlayerController owner) : base(owner) { }
        public override string Name => "Parado";
        public override Color DebugColor => new Color(0.75f, 0.85f, 1f);

        public override void Tick(float dt) => Owner.Move(Vector3.zero, dt);
    }

    // =====================================================================
    //  ANDAR — segue o caminho calculado até ao ponto clicado.
    // =====================================================================
    public class PlayerMoveState : State<PlayerController>
    {
        public PlayerMoveState(PlayerController owner) : base(owner) { }
        public override string Name => "Andar";
        public override Color DebugColor => new Color(0.4f, 0.8f, 1f);

        public override void Tick(float dt)
        {
            Owner.Move(Owner.MoveInput * Owner.moveSpeed, dt);
            Owner.FaceDirection(Owner.MoveInput, dt);
        }
    }

    // =====================================================================
    //  ATACAR — automático quando um guarda está numa posição vulnerável.
    //  Pelas costas = eliminação instantânea. Pelo lado = dano da faca (um guarda pesado aguenta).
    //  A posição é REVALIDADA no momento do impacto (o guarda pode virar-se a tempo).
    // =====================================================================
    public class PlayerAttackState : State<PlayerController>
    {
        // Fases da animação (em percentagem do tempo total do ataque)
        private const float WindupEnd = 0.32f;  // puxa a faca atrás + avança para o guarda
        private const float HitMoment = 0.38f;  // o golpe acerta
        private const float SwingEnd = 0.55f;   // fim do corte
        private float _timer;
        private bool _hasHit;
        private EnemyAI _target;

        public PlayerAttackState(PlayerController owner) : base(owner) { }
        public override string Name => "Atacar";
        public override Color DebugColor => new Color(1f, 0.85f, 0.3f);
        public bool IsFinished => _timer >= Owner.attackDuration;

        public override void Enter()
        {
            _timer = 0f;
            _hasHit = false;
            _target = Owner.AttackTarget;
            FaceTarget(1f);
            Sfx.Play(Sfx.Slash, 0.7f, 1.1f);
        }

        public override void Tick(float dt)
        {
            _timer += dt;
            float t = Mathf.Clamp01(_timer / Owner.attackDuration);

            // 1) INVESTIDA: avança rapidamente até ficar colado ao guarda
            if (t < HitMoment && _target != null && !_target.Health.IsDead)
            {
                FaceTarget(dt * 25f);
                Vector3 to = _target.transform.position - Owner.transform.position;
                to.y = 0f;
                if (to.magnitude > 0.95f) Owner.Move(to.normalized * 16f, dt);
                else Owner.Move(Vector3.zero, dt);
            }
            else Owner.Move(Vector3.zero, dt);

            // 2) ANIMAÇÃO DA FACA: puxa atrás → corte rápido → volta ao sítio
            float angle;
            if (t < WindupEnd) angle = Mathf.Lerp(0f, 75f, EaseOut(t / WindupEnd));
            else if (t < SwingEnd) angle = Mathf.Lerp(75f, -125f, EaseOut((t - WindupEnd) / (SwingEnd - WindupEnd)));
            else angle = Mathf.Lerp(-125f, 0f, EaseInOut((t - SwingEnd) / (1f - SwingEnd)));
            if (Owner.swordPivot != null) Owner.swordPivot.localRotation = Quaternion.Euler(0f, angle, 0f);

            // O corpo inclina-se para a frente durante o golpe
            if (Owner.model != null)
            {
                float lean = t < SwingEnd ? Mathf.Sin(t / SwingEnd * Mathf.PI) * 18f : 0f;
                Owner.model.localRotation = Quaternion.Euler(lean, 0f, 0f);
            }

            // 3) IMPACTO
            if (!_hasHit && t >= HitMoment)
            {
                _hasHit = true;
                if (_target != null && !_target.Health.IsDead) Kill();
            }
        }

        private void Kill()
        {
            Vector3 dir = _target.transform.position - Owner.transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Owner.transform.forward;

            // REVALIDAÇÃO no momento do golpe: distância, ângulo, linha de visão e alerta
            if (!_target.CanBeAssassinatedBy(Owner.transform.position, Owner.killRange, Owner.hitValidationTolerance, out bool fromBehind))
            {
                _target.OnAssassinationFailed(Owner.transform.position);
                Popups.Show(_target.transform.position + Vector3.up * 2.2f, "FALHOU!", new Color(1f, 0.35f, 0.3f), 30);
                Sfx.Play(Sfx.Huh, 0.6f, 0.7f);
                return;
            }

            // Stealth = o guarda não estava em combate nem a ver o jogador
            bool stealth = _target.IsUnaware;
            _target.ReceiveHit(fromBehind ? 9999f : Owner.knifeDamage, Owner.transform.position);

            if (!_target.Health.IsDead)
            {
                // Guarda resistente (ex.: pesado) sobreviveu ao golpe pelo lado
                Blood.Spawn(_target.transform.position + Vector3.up * 1.1f, dir, 0.6f);
                Sfx.Play(Sfx.Stab, 0.7f, 1.3f);
                Popups.Show(_target.transform.position + Vector3.up * 2.2f, "RESISTIU!", new Color(1f, 0.55f, 0.2f), 30);
                CameraFollow.Shake(0.12f, 0.2f);
                return;
            }

            GameManager.Instance?.RegisterKill(stealth);
            Blood.Spawn(_target.transform.position + Vector3.up * 1.1f, dir, 1.6f);
            SlashTrail.Spawn(Owner.transform.position + Vector3.up * 1.15f, Owner.transform.forward);
            Sfx.Play(Sfx.Stab, 1f);
            Popups.Show(_target.transform.position + Vector3.up * 2.2f, stealth ? "SILENCIOSO!" : "ELIMINADO!",
                stealth ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.85f, 0.2f), 34);

            // "Sentir" o golpe: congela uma fração de segundo + abana a câmara
            TimeFx.HitStop(0.075f);
            CameraFollow.Shake(0.2f, 0.35f);
            CameraFollow.Punch(0.12f);

            // Último guarda do nível → câmara lenta
            bool anyAlive = false;
            foreach (var e in EnemyAI.All) if (e != null && !e.Health.IsDead) { anyAlive = true; break; }
            if (!anyAlive) TimeFx.SlowMotion(0.3f, 0.9f);
        }

        public override void Exit()
        {
            if (Owner.swordPivot != null) Owner.swordPivot.localRotation = Quaternion.identity; // volta à posição normal
            if (Owner.model != null) Owner.model.localRotation = Quaternion.identity;
        }

        private void FaceTarget(float k)
        {
            if (_target == null) return;
            Vector3 dir = _target.transform.position - Owner.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                Owner.transform.rotation = Quaternion.Slerp(Owner.transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(k));
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
        private static float EaseInOut(float x) => x * x * (3f - 2f * x);
    }

    // =====================================================================
    //  FERIDO — levou um tiro: empurrão (knockback) e fica invulnerável por um tempo.
    // =====================================================================
    public class PlayerHurtState : State<PlayerController>
    {
        private float _timer;
        private Vector3 _knockback;

        public PlayerHurtState(PlayerController owner) : base(owner) { }
        public override string Name => "Ferido";
        public override Color DebugColor => new Color(1f, 0.35f, 0.35f);
        public bool IsFinished => _timer >= Owner.hurtDuration;

        public override void Enter()
        {
            _timer = 0f;
            Vector3 away = Owner.transform.position - Owner.LastHitSource;
            away.y = 0f;
            _knockback = (away.sqrMagnitude > 0.01f ? away.normalized : -Owner.transform.forward) * Owner.knockbackSpeed;
            Owner.Health.SetInvulnerable(Owner.invulnerabilityAfterHit);
            Owner.SetBodyColor(DebugColor);
            CameraFollow.Shake(0.25f, 0.25f);
            Blood.Spawn(Owner.transform.position + Vector3.up * 1.1f, _knockback.normalized, 0.6f);
            Sfx.Play(Sfx.Hurt, 0.8f);
        }

        public override void Tick(float dt)
        {
            _timer += dt;
            float decay = 1f - Mathf.Clamp01(_timer / Owner.hurtDuration);
            Owner.Move(_knockback * decay, dt);
        }

        public override void Exit() => Owner.ResetBodyColor();
    }

    // =====================================================================
    //  MORTO — estado final: cai para trás e deixa de responder.
    // =====================================================================
    public class PlayerDeadState : State<PlayerController>
    {
        private Quaternion _startRotation;
        private Vector3 _startPosition;
        private float _timer;

        public PlayerDeadState(PlayerController owner) : base(owner) { }
        public override string Name => "Morto";
        public override Color DebugColor => new Color(0.5f, 0.5f, 0.5f);

        public override void Enter()
        {
            _timer = 0f;
            Owner.ClearPath();
            Owner.SetBodyColor(DebugColor);
            if (Owner.swordPivot != null) Owner.swordPivot.gameObject.SetActive(false);
            if (Owner.model != null)
            {
                _startRotation = Owner.model.localRotation;
                _startPosition = Owner.model.localPosition;
            }
            CameraFollow.Shake(0.5f, 0.4f);
            Blood.Spawn(Owner.transform.position + Vector3.up, -Owner.transform.forward, 1.5f);
        }

        public override void Tick(float dt)
        {
            _timer += dt;
            Owner.Move(Vector3.zero, dt);
            if (Owner.model == null) return;

            float t = Mathf.SmoothStep(0f, 1f, _timer / 0.6f);
            Owner.model.localRotation = Quaternion.Slerp(_startRotation, Quaternion.Euler(-90f, 0f, 0f) * _startRotation, t);
            Owner.model.localPosition = Vector3.Lerp(_startPosition, new Vector3(_startPosition.x, 0.35f, _startPosition.z), t);
        }
    }
}
