using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM.Visuals
{
    /// <summary>
    /// Efeito de sangue feito por código: gotas que saltam (partículas) + manchas que ficam no chão.
    /// </summary>
    public static class Blood
    {
        private const int MaxSplats = 80;
        private static readonly Queue<GameObject> Splats = new Queue<GameObject>();
        private static Material _splatMaterial;
        private static int _layer;

        private static readonly Color Red = new Color(0.75f, 0.05f, 0.06f);
        private static readonly Color DarkRed = new Color(0.45f, 0.02f, 0.03f);

        /// <param name="position">ponto do impacto</param>
        /// <param name="direction">direção do golpe (o sangue espalha-se para esse lado)</param>
        /// <param name="amount">1 = normal, 2 = muito</param>
        public static void Spawn(Vector3 position, Vector3 direction, float amount = 1f)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Random.insideUnitSphere;
            direction.y = 0f;

            // Gotas
            int drops = Mathf.RoundToInt(10 * amount);
            Gfx.Burst(position + direction * 0.2f, Red, drops, 5f * Mathf.Sqrt(amount), 0.13f);
            Gfx.Burst(position + direction * 0.2f, DarkRed, drops / 2, 3.5f, 0.17f);

            // Manchas no chão: uma grande + várias pequenas na direção do golpe
            Vector3 floor = new Vector3(position.x, 0f, position.z);
            Splat(floor + direction * 0.4f, Random.Range(1.3f, 1.9f) * Mathf.Sqrt(amount));
            int small = Mathf.RoundToInt(Random.Range(2, 5) * amount);
            for (int i = 0; i < small; i++)
            {
                Vector3 offset = direction * Random.Range(0.8f, 2.4f) + Random.insideUnitSphere * 0.5f;
                offset.y = 0f;
                Splat(floor + offset, Random.Range(0.3f, 0.7f));
            }
        }

        private static void Splat(Vector3 position, float size)
        {
            if (_splatMaterial == null)
                _splatMaterial = Gfx.MakeTransparent(new Color(0.6f, 0.02f, 0.04f, 0.92f), ProceduralTextures.BloodSplat);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BloodSplat";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            _layer = (_layer + 1) % 200;
            go.transform.position = position + Vector3.up * (0.012f + _layer * 0.0002f); // evita "z-fighting"
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _splatMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<SplatGrow>();

            Splats.Enqueue(go);
            while (Splats.Count > MaxSplats)
            {
                var old = Splats.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }
    }

    /// <summary>A mancha "cresce" rapidamente quando aparece.</summary>
    public class SplatGrow : MonoBehaviour
    {
        private Vector3 _target;
        private float _t;

        private void Start()
        {
            _target = transform.localScale;
            transform.localScale = _target * 0.2f;
        }

        private void Update()
        {
            _t += Time.deltaTime / 0.18f;
            transform.localScale = Vector3.Lerp(_target * 0.2f, _target, 1f - (1f - _t) * (1f - _t));
            if (_t >= 1f)
            {
                transform.localScale = _target;
                Destroy(this);
            }
        }
    }

    /// <summary>Anel que aparece onde se clicou para mover.</summary>
    public class ClickMarker : MonoBehaviour
    {
        private static Material _material;
        private float _t;
        private Material _instance;
        private Color _color = Color.white;

        public static void Spawn(Vector3 position) => Spawn(position, new Color(1f, 1f, 1f, 0.8f));

        public static void Spawn(Vector3 position, Color color)
        {
            if (_material == null) _material = Gfx.MakeGlow(new Color(1f, 1f, 1f, 0.8f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ClickMarker";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = new Vector3(position.x, 0.03f, position.z);
            go.transform.localScale = new Vector3(0.2f, 0.01f, 0.2f);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var marker = go.AddComponent<ClickMarker>();
            marker._color = color;
            marker._instance = new Material(_material);
            r.sharedMaterial = marker._instance;
        }

        private void Update()
        {
            _t += Time.deltaTime / 0.45f;
            float s = Mathf.Lerp(0.2f, 1.1f, Mathf.Sqrt(_t));
            transform.localScale = new Vector3(s, 0.01f, s);
            var c = new Color(_color.r, _color.g, _color.b, _color.a * (1f - _t));
            _instance.color = c;
            if (_instance.HasProperty("_BaseColor")) _instance.SetColor("_BaseColor", c);
            if (_t >= 1f)
            {
                Destroy(_instance);
                Destroy(gameObject);
            }
        }
    }
}
