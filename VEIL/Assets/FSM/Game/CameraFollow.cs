using UnityEngine;

namespace AulaFSM
{
    /// <summary>Câmara de cima a seguir o jogador, com "screen shake".</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 17f, -11f);
        public float smooth = 6f;

        private static float _shakeTime, _shakeStrength, _punch;

        /// <summary>Zoom rápido (0.1 = aproxima 10%).</summary>
        public static void Punch(float amount) => _punch = Mathf.Max(_punch, amount);
        private Vector3 _basePos;

        public static void Shake(float duration, float strength)
        {
            _shakeTime = Mathf.Max(_shakeTime, duration);
            _shakeStrength = Mathf.Max(_shakeStrength, strength);
        }

        private void Start()
        {
            if (target != null) _basePos = target.position + offset;
            else _basePos = transform.position;
        }

        private void LateUpdate()
        {
            if (target != null)
                _basePos = Vector3.Lerp(_basePos, target.position + offset, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));

            Vector3 shake = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.unscaledDeltaTime;
                shake = Random.insideUnitSphere * _shakeStrength;
                if (_shakeTime <= 0f) _shakeStrength = 0f;
            }

            // "Punch": aproxima a câmara por um instante no impacto e volta suavemente
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 0.6f);
            transform.position = _basePos + shake - offset * _punch;
            transform.rotation = Quaternion.LookRotation(-offset.normalized + Vector3.zero, Vector3.up);
        }
    }
}
