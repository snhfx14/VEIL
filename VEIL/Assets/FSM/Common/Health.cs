using System;
using UnityEngine;

namespace AulaFSM
{
    /// <summary>Vida partilhada pelo Player e pelos inimigos.</summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 100f;

        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Normalized => maxHealth > 0 ? Current / maxHealth : 0f;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        /// <summary>(quantidade, posição de quem causou o dano)</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;

        private float _invulnerableUntil;

        private void Awake() => Current = maxHealth;

        public bool TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (IsDead || IsInvulnerable) return false;

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, sourcePosition);
            if (IsDead) Died?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void SetInvulnerable(float duration)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + duration);
        }
    }

    /// <summary>Qualquer personagem com FSM (usado pelo HUD de debug e pelos labels).</summary>
    public interface IStateMachineOwner
    {
        string DisplayName { get; }
        Core.StateMachine StateMachine { get; }
        Transform transform { get; }
    }
}
