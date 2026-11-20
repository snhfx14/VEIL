using AulaFSM.Player;
using UnityEngine;

namespace AulaFSM.Enemy
{
    /// <summary>
    /// Desenha no chão o cone de visão do guarda (cortado pelas paredes),
    /// com a cor do estado atual. Só visual — ajuda a perceber a FSM no vídeo.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionConeVisual : MonoBehaviour
    {
        public EnemyAI enemy;
        public int segments = 28;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private MeshRenderer _renderer;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "VisionCone" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().mesh = _mesh;

            _vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            _mesh.vertices = _vertices;
            _mesh.triangles = triangles;
        }

        private void LateUpdate()
        {
            if (enemy == null) { Destroy(gameObject); return; }   // o guarda foi destruído
            if (enemy.StateMachine == null) return;
            _renderer.enabled = !enemy.Health.IsDead;

            // Guarda morto → a lanterna cai e apaga-se aos poucos
            if (enemy.Health.IsDead && enemy.flashlight != null)
            {
                enemy.flashlight.intensity = Mathf.MoveTowards(enemy.flashlight.intensity, 0f, Time.deltaTime * 12f);
                if (enemy.flashlight.intensity <= 0f) enemy.flashlight.enabled = false;
                return;
            }

            // O cone fica sempre plano no chão mesmo que o guarda abane
            transform.position = enemy.transform.position + Vector3.up * 0.03f;
            transform.rotation = Quaternion.Euler(0f, enemy.transform.eulerAngles.y, 0f);

            float half = enemy.viewAngle * 0.5f;
            Vector3 origin = enemy.transform.position + Vector3.up * 1f;
            _vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)segments);
                Vector3 localDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 worldDir = transform.rotation * localDir;
                float dist = ClipDistance(origin, worldDir, enemy.EffectiveViewDistance);
                _vertices[i + 1] = localDir * dist;
            }
            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();

            // Cor da lanterna: amarelo claro a patrulhar, cor do estado nos outros; mais forte quando vê o jogador
            var state = enemy.StateMachine.CurrentState;
            if (state != null)
            {
                Color lamp = new Color(1f, 0.95f, 0.6f);
                Color c = state is EnemyPatrolState ? lamp : Color.Lerp(lamp, state.DebugColor, 0.75f);
                c.a = enemy.CanSeeTarget ? 0.3f : 0.12f; // feixe subtil: quem ilumina o chão é a luz verdadeira
                var mat = _renderer.material;
                mat.color = Color.Lerp(mat.color, c, Time.deltaTime * 8f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);

                // A luz da lanterna também muda de cor com o estado (vermelha quando persegue/dispara)
                if (enemy.flashlight != null)
                {
                    Color lightColor = state is EnemyPatrolState ? new Color(1f, 0.93f, 0.7f) : Color.Lerp(new Color(1f, 0.93f, 0.7f), state.DebugColor, 0.55f);
                    enemy.flashlight.color = Color.Lerp(enemy.flashlight.color, lightColor, Time.deltaTime * 6f);
                    // pisca ligeiramente (lanterna a pilhas)
                    enemy.flashlight.intensity = 9f * (0.94f + 0.06f * Mathf.PerlinNoise(Time.time * 6f, GetInstanceID() * 0.1f));
                }
            }
        }

        private float ClipDistance(Vector3 origin, Vector3 dir, float max)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, _hits, max, ~0, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < count; i++)
            {
                var col = _hits[i].collider;
                if (col.GetComponentInParent<EnemyAI>() != null || col.GetComponentInParent<PlayerController>() != null) continue;
                if (_hits[i].distance < best) best = _hits[i].distance;
            }
            return best;
        }
    }
}
