using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM.Visuals
{
    // =====================================================================
    //  TEMPO: hit-stop (congela uma fração de segundo no impacto) e câmara lenta
    // =====================================================================
    public class TimeFx : MonoBehaviour
    {
        private static TimeFx _instance;
        private float _hitStopUntil, _slowUntil, _slowScale = 1f;

        private static TimeFx I
        {
            get
            {
                if (_instance == null) _instance = new GameObject("TimeFx").AddComponent<TimeFx>();
                return _instance;
            }
        }

        public static void HitStop(float duration) => I._hitStopUntil = Mathf.Max(I._hitStopUntil, Time.unscaledTime + duration);

        public static void SlowMotion(float scale, float duration)
        {
            I._slowScale = scale;
            I._slowUntil = Time.unscaledTime + duration;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.StateMachine.CurrentState is GamePausedState) return; // a pausa manda no timeScale

            float scale = 1f;
            if (gm.IsPlaying)
            {
                if (Time.unscaledTime < _hitStopUntil) scale = 0.03f;
                else if (Time.unscaledTime < _slowUntil) scale = _slowScale;
            }
            Time.timeScale = scale;
        }

        private void OnDestroy()
        {
            if (_instance == this) Time.timeScale = 1f;
        }
    }

    // =====================================================================
    //  RASTO DO GOLPE: meia-lua branca que aparece e desvanece
    // =====================================================================
    public class SlashTrail : MonoBehaviour
    {
        private static Mesh _mesh;
        private Material _material;
        private float _t;

        public static void Spawn(Vector3 position, Vector3 forward)
        {
            var go = new GameObject("SlashTrail");
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(forward);
            go.AddComponent<MeshFilter>().sharedMesh = ArcMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = go.AddComponent<SlashTrail>();
            trail._material = Gfx.MakeGlow(new Color(1f, 1f, 1f, 0.9f));
            if (trail._material.HasProperty("_Cull")) trail._material.SetFloat("_Cull", 0f); // visível dos dois lados
            r.sharedMaterial = trail._material;
        }

        private static Mesh ArcMesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                const int seg = 24;
                var v = new Vector3[(seg + 1) * 2];
                var tri = new int[seg * 6];
                for (int i = 0; i <= seg; i++)
                {
                    float k = i / (float)seg;
                    float a = Mathf.Lerp(95f, -95f, k) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    float thickness = Mathf.Sin(k * Mathf.PI);            // fino nas pontas: forma de meia-lua
                    v[i * 2] = dir * (1.55f - 0.75f * thickness);
                    v[i * 2 + 1] = dir * 1.7f;
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = i * 2;
                    tri[i * 6 + 0] = a; tri[i * 6 + 1] = a + 1; tri[i * 6 + 2] = a + 3;
                    tri[i * 6 + 3] = a; tri[i * 6 + 4] = a + 3; tri[i * 6 + 5] = a + 2;
                }
                _mesh = new Mesh { name = "SlashArc", vertices = v, triangles = tri };
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
                return _mesh;
            }
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime / 0.22f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, _t);
            var c = new Color(1f, 1f, 1f, 0.9f * (1f - _t) * (1f - _t));
            _material.color = c;
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_t >= 1f)
            {
                Destroy(_material);
                Destroy(gameObject);
            }
        }
    }

    // =====================================================================
    //  OBJETO SOLTO (ex.: a arma que cai da mão do guarda)
    // =====================================================================
    public class Debris : MonoBehaviour
    {
        private Vector3 _velocity, _spin;
        private bool _landed;
        private float _age;

        public void Launch(Vector3 velocity)
        {
            _velocity = velocity;
            _spin = Random.insideUnitSphere * 900f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age > 8f) { Destroy(gameObject); return; }
            if (_landed) return;

            _velocity.y -= 20f * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(_spin * Time.deltaTime, Space.World);
            if (transform.position.y <= 0.08f)
            {
                var p = transform.position; p.y = 0.08f; transform.position = p;
                if (Mathf.Abs(_velocity.y) < 2f) { _landed = true; transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 90f); }
                _velocity = new Vector3(_velocity.x * 0.4f, -_velocity.y * 0.35f, _velocity.z * 0.4f);
                _spin *= 0.4f;
            }
        }
    }

    // =====================================================================
    //  SOM PROCEDIMENTAL (gerado por código, sem ficheiros de áudio)
    // =====================================================================
    public static class Sfx
    {
        private const int Rate = 44100;
        private static AudioSource[] _sources;
        private static int _next;
        private static readonly System.Random Rng = new System.Random(1);

        public static AudioClip Slash => _slash != null ? _slash : (_slash = Make("Slash", 0.22f, Slash_));
        public static AudioClip Stab => _stab != null ? _stab : (_stab = Make("Stab", 0.25f, Stab_));
        public static AudioClip Gun => _gun != null ? _gun : (_gun = Make("Gun", 0.3f, Gun_));
        public static AudioClip Hurt => _hurt != null ? _hurt : (_hurt = Make("Hurt", 0.2f, Hurt_));
        public static AudioClip Pickup => _pickup != null ? _pickup : (_pickup = Make("Pickup", 0.35f, Pickup_));
        public static AudioClip Click => _click != null ? _click : (_click = Make("Click", 0.05f, Click_));
        public static AudioClip Alert => _alert != null ? _alert : (_alert = Make("Alert", 0.25f, Alert_));
        public static AudioClip Huh => _huh != null ? _huh : (_huh = Make("Huh", 0.18f, Huh_));
        public static AudioClip Win => _win != null ? _win : (_win = Make("Win", 1.1f, Win_));
        public static AudioClip Lose => _lose != null ? _lose : (_lose = Make("Lose", 1.0f, Lose_));
        private static AudioClip _slash, _stab, _gun, _hurt, _pickup, _click, _alert, _huh, _win, _lose;

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;
            EnsureSources();
            var s = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            s.PlayOneShot(clip, volume);
        }

        private static void EnsureSources()
        {
            if (_sources != null && _sources[0] != null) return;
            if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            var go = new GameObject("Sfx");
            _sources = new AudioSource[10];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = go.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].spatialBlend = 0f;
            }
        }

        private delegate float Synth(float t, float dur);

        private static AudioClip Make(string name, float duration, Synth synth)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(synth(i / (float)Rate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);
        private static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        private static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);

        // "filtro" simples: média com o valor anterior (som mais abafado)
        private static float _lp;
        private static float LowPass(float x, float k) => _lp = _lp + (x - _lp) * k;

        private static float Slash_(float t, float d) => LowPass(Noise(), Mathf.Lerp(0.9f, 0.08f, t / d)) * Env(t, 0.03f, 0.06f) * 0.9f;
        private static float Stab_(float t, float d) => (Sine(t, Mathf.Lerp(140f, 45f, t / d)) * 0.9f + Noise() * 0.35f * Env(t, 0.001f, 0.02f)) * Env(t, 0.002f, 0.07f);
        private static float Gun_(float t, float d) => (LowPass(Noise(), 0.5f) * 1.1f + Sine(t, Mathf.Lerp(120f, 40f, t / d)) * 0.7f) * Env(t, 0.001f, 0.06f);
        private static float Hurt_(float t, float d) => (Sine(t, Mathf.Lerp(320f, 140f, t / d)) * 0.6f + Noise() * 0.25f) * Env(t, 0.003f, 0.06f);
        private static float Pickup_(float t, float d) => (t < 0.09f ? Sine(t, 1046f) : Sine(t, 1568f)) * Env(t < 0.09f ? t : t - 0.09f, 0.003f, 0.08f) * 0.45f;
        private static float Click_(float t, float d) => Sine(t, 1400f) * Env(t, 0.001f, 0.012f) * 0.4f;
        private static float Alert_(float t, float d) => Mathf.Sign(Sine(t, t < 0.1f ? 660f : 990f)) * Env(t < 0.1f ? t : t - 0.1f, 0.003f, 0.05f) * 0.22f;
        private static float Huh_(float t, float d) => Sine(t, Mathf.Lerp(420f, 620f, t / d)) * Env(t, 0.01f, 0.06f) * 0.3f;

        private static float Win_(float t, float d)
        {
            float[] notes = { 523f, 659f, 784f, 1046f };
            int i = Mathf.Min(3, (int)(t / 0.13f));
            float lt = t - i * 0.13f;
            return (Sine(t, notes[i]) * 0.5f + Sine(t, notes[i] * 2f) * 0.15f) * Env(lt, 0.005f, i == 3 ? 0.35f : 0.1f) * 0.5f;
        }

        private static float Lose_(float t, float d)
        {
            float[] notes = { 392f, 330f, 262f };
            int i = Mathf.Min(2, (int)(t / 0.22f));
            float lt = t - i * 0.22f;
            return Mathf.Sign(Sine(t, notes[i])) * Env(lt, 0.005f, i == 2 ? 0.3f : 0.12f) * 0.18f;
        }
    }

    // =====================================================================
    //  TEXTOS FLUTUANTES ("ELIMINADO!", "+1", ...)
    // =====================================================================
    public static class Popups
    {
        public class Item
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public float Born;
            public int Size;
        }

        public static readonly List<Item> Active = new List<Item>();
        public const float Life = 1.1f;

        public static void Show(Vector3 worldPos, string text, Color color, int size = 30)
        {
            Active.Add(new Item { Position = worldPos, Text = text, Color = color, Born = Time.unscaledTime, Size = size });
        }
    }
}
