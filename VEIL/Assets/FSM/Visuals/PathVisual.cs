using System.Collections.Generic;
using AulaFSM.Player;
using UnityEngine;

namespace AulaFSM.Visuals
{
    /// <summary>
    /// Desenha no chão o trajeto do jogador até ao ponto clicado:
    /// pontos brancos que "fluem" na direção do destino + anel a pulsar no fim.
    /// Quando está a perseguir um guarda, o trajeto fica vermelho e aparece uma mira por baixo do guarda.
    /// </summary>
    public class PathVisual : MonoBehaviour
    {
        public PlayerController player;

        private const int MaxDots = 140;
        private const float Spacing = 0.55f;
        private const float FlowSpeed = 1.6f;   // metros por segundo
        private const float SkipNearPlayer = 0.7f;

        private readonly List<Transform> _dots = new List<Transform>();
        private readonly List<Renderer> _dotRenderers = new List<Renderer>();
        private Transform _endRing, _endInner;
        private Renderer _endRingR, _endInnerR;
        private Material _white, _red, _whiteSoft, _redSoft;
        private readonly List<Vector3> _points = new List<Vector3>();

        private void Start()
        {
            _white = Gfx.MakeGlow(new Color(1f, 1f, 1f, 0.9f));
            _red = Gfx.MakeGlow(new Color(1f, 0.25f, 0.2f, 0.95f));
            _whiteSoft = Gfx.MakeGlow(new Color(1f, 1f, 1f, 0.35f));
            _redSoft = Gfx.MakeGlow(new Color(1f, 0.2f, 0.15f, 0.4f));

            for (int i = 0; i < MaxDots; i++)
            {
                var d = MakeDisc("PathDot", 0.17f, _white);
                d.gameObject.SetActive(false);
                _dots.Add(d);
                _dotRenderers.Add(d.GetComponent<Renderer>());
            }

            _endRing = MakeDisc("PathEndRing", 0.9f, _whiteSoft);
            _endRingR = _endRing.GetComponent<Renderer>();
            _endInner = MakeDisc("PathEndDot", 0.35f, _white);
            _endInnerR = _endInner.GetComponent<Renderer>();
            _endRing.gameObject.SetActive(false);
            _endInner.gameObject.SetActive(false);
        }

        private Transform MakeDisc(string name, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, 0.005f, size);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        private void LateUpdate()
        {
            int used = 0;
            bool show = player != null && player.HasMoveInput && !player.Health.IsDead;
            bool chasing = show && player.ChaseTarget != null;

            if (show)
            {
                // Polilinha: posição atual do jogador + cantos que faltam
                _points.Clear();
                _points.Add(Flat(player.transform.position));
                var corners = player.PathCorners;
                for (int i = player.PathCornerIndex; i < corners.Length; i++) _points.Add(Flat(corners[i]));

                // Comprimento total
                float total = 0f;
                for (int i = 1; i < _points.Count; i++) total += Vector3.Distance(_points[i - 1], _points[i]);

                // Pontos a cada "Spacing" metros, com deslocamento animado (efeito de fluxo)
                float phase = Mathf.Repeat(Time.unscaledTime * FlowSpeed, Spacing);
                for (float dist = SkipNearPlayer + phase; dist < total - 0.4f && used < MaxDots; dist += Spacing)
                {
                    Vector3 p = PointAt(dist);
                    var dot = _dots[used];
                    dot.gameObject.SetActive(true);
                    dot.position = p + Vector3.up * 0.035f;

                    // os pontos aparecem suavemente junto ao jogador
                    float fadeIn = Mathf.Clamp01((dist - SkipNearPlayer) / 0.8f);
                    float size = Mathf.Lerp(0.1f, 0.24f, fadeIn);
                    dot.localScale = new Vector3(size, 0.005f, size);
                    _dotRenderers[used].sharedMaterial = chasing ? _red : _white;
                    used++;
                }

                // Marcador no fim: anel a pulsar (branco) ou mira por baixo do guarda (vermelho)
                Vector3 end = chasing ? Flat(player.ChaseTarget.transform.position) : _points[_points.Count - 1];
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.12f;
                _endRing.gameObject.SetActive(true);
                _endInner.gameObject.SetActive(true);
                _endRing.position = end + Vector3.up * 0.03f;
                _endInner.position = end + Vector3.up * 0.04f;
                float ringSize = (chasing ? 1.5f : 0.9f) * pulse;
                _endRing.localScale = new Vector3(ringSize, 0.005f, ringSize);
                _endInner.localScale = chasing ? new Vector3(0.0f, 0.005f, 0.0f) : new Vector3(0.3f, 0.005f, 0.3f);
                _endRingR.sharedMaterial = chasing ? _redSoft : _whiteSoft;
                _endInnerR.sharedMaterial = chasing ? _red : _white;
            }
            else
            {
                _endRing.gameObject.SetActive(false);
                _endInner.gameObject.SetActive(false);
            }

            for (int i = used; i < _dots.Count; i++)
                if (_dots[i].gameObject.activeSelf) _dots[i].gameObject.SetActive(false);
        }

        private Vector3 PointAt(float distance)
        {
            for (int i = 1; i < _points.Count; i++)
            {
                float seg = Vector3.Distance(_points[i - 1], _points[i]);
                if (distance <= seg) return Vector3.Lerp(_points[i - 1], _points[i], seg > 0f ? distance / seg : 0f);
                distance -= seg;
            }
            return _points[_points.Count - 1];
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
