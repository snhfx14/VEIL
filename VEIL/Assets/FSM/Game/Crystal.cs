using AulaFSM.Player;
using UnityEngine;

namespace AulaFSM
{
    /// <summary>Objetivo do jogo: apanhar todos os cristais sem ser apanhado pelos guardas.</summary>
    public class Crystal : MonoBehaviour
    {
        public float pickupRadius = 1.2f;
        private PlayerController _player;
        private Vector3 _startPos;

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _startPos = transform.position;
        }

        private void Update()
        {
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            transform.position = _startPos + Vector3.up * (Mathf.Sin(Time.time * 2f + _startPos.x) * 0.2f);

            if (_player == null || _player.Health.IsDead) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            Vector3 d = _player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= pickupRadius)
            {
                GameManager.Instance?.CollectCrystal();
                Visuals.Gfx.Burst(transform.position, new Color(0.3f, 1f, 1f), 14, 5f, 0.15f);
                Visuals.Sfx.Play(Visuals.Sfx.Pickup, 0.6f);
                Visuals.Popups.Show(transform.position + Vector3.up * 1.2f, "+1", new Color(0.4f, 1f, 1f), 32);
                Destroy(gameObject);
            }
        }
    }
}
