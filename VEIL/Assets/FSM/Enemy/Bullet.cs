using AulaFSM.Player;
using AulaFSM.Visuals;
using UnityEngine;

namespace AulaFSM.Enemy
{
    /// <summary>Bala disparada pelos guardas. Pára nas caixas; fere o jogador.</summary>
    public class Bullet : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed, _damage, _life = 2f;
        private Vector3 _origin;
        private readonly RaycastHit[] _hits = new RaycastHit[6];

        public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage)
        {
            var go = Gfx.Part(PrimitiveType.Capsule, "Bullet", null, position, new Vector3(0.13f, 0.22f, 0.13f),
                new Color(1f, 0.85f, 0.25f), outline: false);
            go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = go.AddComponent<Bullet>();
            b._direction = direction;
            b._speed = speed;
            b._damage = damage;
            b._origin = position;
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            _life -= Time.deltaTime;

            int count = Physics.RaycastNonAlloc(transform.position, _direction, _hits, step, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; bestIndex = i; }
            }

            if (bestIndex >= 0)
            {
                var hit = _hits[bestIndex];
                var player = hit.collider.GetComponentInParent<PlayerController>();
                if (player != null)
                    player.Health.TakeDamage(_damage, _origin); // o estado "Ferido" trata do sangue
                else
                    Gfx.Burst(hit.point, new Color(0.9f, 0.75f, 0.5f), 5, 3f, 0.08f); // lascas de madeira
                Destroy(gameObject);
                return;
            }

            transform.position += _direction * step;
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
