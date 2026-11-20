using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM.Visuals
{
    /// <summary>
    /// Ajudas gráficas: materiais, contorno preto estilo cartoon (técnica "inverted hull"),
    /// material transparente e partículas simples.
    /// </summary>
    public static class Gfx
    {
        public const string TransparentResource = "FSM_Transparent";
        public const string OpaqueResource = "FSM_Opaque";
        public const string GlowResource = "FSM_TransparentUnlit";

        private static Material _glowTemplate;

        /// <summary>
        /// Material transparente que NÃO é afetado pela luz (brilha sozinho):
        /// usado no trajeto, cone da lanterna, laser, rasto do golpe — para se verem no escuro.
        /// </summary>
        public static Material MakeGlow(Color color, Texture texture = null)
        {
            if (_glowTemplate == null)
            {
                _glowTemplate = Resources.Load<Material>(GlowResource);
                if (_glowTemplate == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) return MakeTransparent(color, texture);
                    _glowTemplate = new Material(shader);
                    ConfigureTransparent(_glowTemplate);
                }
            }
            var mat = new Material(_glowTemplate) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            }
            return mat;
        }

        /// <summary>Clarão de luz rápido (disparo, impacto).</summary>
        public static void Flash(Vector3 position, Color color, float range, float intensity, float duration)
        {
            var go = new GameObject("Flash");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<LightFlash>().Init(intensity, duration);
        }

        private static Material _base, _outline, _transparentTemplate;
        private static readonly Dictionary<Color, Material> FxMaterials = new Dictionary<Color, Material>();

        /// <summary>Material Lit por defeito do URP (copiado de uma primitiva).</summary>
        public static Material BaseMaterial
        {
            get
            {
                // 1) Material guardado em Resources (garante que o shader URP/Lit vai para a Build)
                if (_base == null) _base = Resources.Load<Material>(OpaqueResource);
                // 2) Recurso: material por defeito de uma primitiva (funciona no Editor)
                if (_base == null)
                {
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _base = temp.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(temp);
                }
                return _base;
            }
        }

        public static Material Make(Color color, Texture texture = null, Vector2? tiling = null, float smoothness = 0.12f)
        {
            var mat = new Material(BaseMaterial) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (tiling.HasValue)
                {
                    mat.mainTextureScale = tiling.Value;
                    if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling.Value);
                }
            }
            return mat;
        }

        /// <summary>Material preto que só desenha as faces de trás → contorno à volta do objeto.</summary>
        public static Material OutlineMaterial
        {
            get
            {
                if (_outline == null)
                {
                    _outline = Make(new Color(0.06f, 0.06f, 0.08f), smoothness: 0f);
                    if (_outline.HasProperty("_Cull")) _outline.SetFloat("_Cull", 1f); // Cull Front
                }
                return _outline;
            }
        }

        // ------------------------------------------------------------------ Transparente (cone de visão)

        public static Material MakeTransparent(Color color, Texture texture = null)
        {
            if (_transparentTemplate == null)
            {
                _transparentTemplate = Resources.Load<Material>(TransparentResource);
                if (_transparentTemplate == null)
                {
                    _transparentTemplate = new Material(BaseMaterial);
                    ConfigureTransparent(_transparentTemplate);
                }
            }
            var mat = new Material(_transparentTemplate) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            }
            return mat;
        }

        /// <summary>Configura um material URP/Lit como transparente (alpha blend).</summary>
        public static void ConfigureTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_ReceiveShadows", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ Peças (primitivas com contorno)

        /// <summary>
        /// Cria uma peça visual (sem colisor) com contorno preto opcional.
        /// </summary>
        public static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, bool outline = true, Vector3? euler = null, Texture texture = null, float outlineWidth = 0.05f,
            float smoothness = 0.12f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().material = Make(color, texture, smoothness: smoothness);

            if (outline) AddOutline(go, type, outlineWidth);
            return go;
        }

        public static void AddOutline(GameObject target, PrimitiveType type, float width)
        {
            var o = GameObject.CreatePrimitive(type);
            o.name = "Outline";
            Object.DestroyImmediate(o.GetComponent<Collider>());
            o.transform.SetParent(target.transform, false);
            Vector3 s = target.transform.lossyScale;
            o.transform.localScale = new Vector3(1f + width * 2f / Mathf.Max(0.01f, s.x),
                                                 1f + width * 2f / Mathf.Max(0.01f, s.y),
                                                 1f + width * 2f / Mathf.Max(0.01f, s.z));
            var r = o.GetComponent<Renderer>();
            r.sharedMaterial = OutlineMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ Partículas

        /// <summary>Explosão de pequenos cubos (impactos, mortes, apanhar cristais).</summary>
        public static void Burst(Vector3 position, Color color, int count = 10, float speed = 5f, float size = 0.18f)
        {
            if (!FxMaterials.TryGetValue(color, out var mat) || mat == null)
            {
                mat = Make(color, smoothness: 0.3f);
                FxMaterials[color] = mat;
            }

            for (int i = 0; i < count; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "FX";
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.position = position;
                p.transform.rotation = Random.rotation;
                p.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.2f);
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                p.AddComponent<FxParticle>().Init(dir.normalized * speed * Random.Range(0.6f, 1.2f), Random.Range(0.4f, 0.7f));
            }
        }
    }

    public class LightFlash : MonoBehaviour
    {
        private Light _light;
        private float _intensity, _duration, _t;

        public void Init(float intensity, float duration)
        {
            _light = GetComponent<Light>();
            _intensity = intensity;
            _duration = duration;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(_t / _duration);
            _light.intensity = _intensity * k * k;
            if (_t >= _duration) Destroy(gameObject);
        }
    }

    public class FxParticle : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _life, _age;
        private Vector3 _startScale;
        private Vector3 _spin;

        public void Init(Vector3 velocity, float life)
        {
            _velocity = velocity;
            _life = life;
            _startScale = transform.localScale;
            _spin = Random.insideUnitSphere * 720f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            _velocity.y -= 18f * dt;
            transform.position += _velocity * dt;
            if (transform.position.y < 0.05f)
            {
                var p = transform.position; p.y = 0.05f; transform.position = p;
                _velocity = new Vector3(_velocity.x * 0.5f, -_velocity.y * 0.3f, _velocity.z * 0.5f);
            }
            transform.Rotate(_spin * dt);
            transform.localScale = _startScale * (1f - Mathf.Clamp01(_age / _life));
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
