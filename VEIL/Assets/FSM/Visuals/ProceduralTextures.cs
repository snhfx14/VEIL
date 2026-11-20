using UnityEngine;

namespace AulaFSM.Visuals
{
    /// <summary>
    /// Texturas geradas por código (não é preciso importar imagens):
    /// caixa de madeira, chão de azulejos e parede de tijolo.
    /// </summary>
    public static class ProceduralTextures
    {
        private const int Size = 128;
        private static Texture2D _crate, _crateDark, _floor, _wall;

        public static Texture2D Crate => _crate != null ? _crate : (_crate = MakeCrate(new Color(0.80f, 0.56f, 0.30f)));
        public static Texture2D CrateDark => _crateDark != null ? _crateDark : (_crateDark = MakeCrate(new Color(0.66f, 0.44f, 0.24f)));
        public static Texture2D Floor => _floor != null ? _floor : (_floor = MakeFloor());
        public static Texture2D Wall => _wall != null ? _wall : (_wall = MakeWall());

        // ------------------------------------------------------------------ Caixa de madeira
        private static Texture2D MakeCrate(Color wood)
        {
            var tex = NewTexture("Crate");
            var px = new Color[Size * Size];
            Color dark = wood * 0.45f; dark.a = 1f;
            Color frame = wood * 0.78f; frame.a = 1f;
            const int border = 13;
            const int brace = 8;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // veios da madeira (esticados na horizontal)
                float grain = Mathf.PerlinNoise(x * 0.03f, y * 0.55f) * 0.22f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.06f;
                Color c = wood * (0.86f + grain);

                // tábuas horizontais
                if (y % 26 < 2) c = Color.Lerp(c, dark, 0.7f);

                bool inBorder = x < border || y < border || x >= Size - border || y >= Size - border;
                int diag = Mathf.Abs(x - y);
                bool inBrace = !inBorder && diag <= brace;

                if (inBorder || inBrace)
                {
                    float g2 = Mathf.PerlinNoise(x * 0.4f, y * 0.06f) * 0.2f;
                    c = frame * (0.9f + g2);
                    // contornos das ripas
                    bool edge = x == border - 1 || y == border - 1 || x == Size - border || y == Size - border ||
                                (inBrace && diag >= brace - 1);
                    if (edge) c = dark;
                }

                // borda exterior escura
                if (x < 2 || y < 2 || x >= Size - 2 || y >= Size - 2) c = dark * 0.8f;

                // pregos nos cantos
                if (IsNail(x, y)) c = new Color(0.25f, 0.25f, 0.27f);

                c.a = 1f;
                px[y * Size + x] = c;
            }
            return Finish(tex, px);
        }

        private static bool IsNail(int x, int y)
        {
            int[] p = { 6, Size - 7 };
            foreach (int nx in p)
            foreach (int ny in p)
                if ((x - nx) * (x - nx) + (y - ny) * (y - ny) <= 4) return true;
            return false;
        }

        // ------------------------------------------------------------------ Chão de azulejos (2x2 por textura)
        private static Texture2D MakeFloor()
        {
            var tex = NewTexture("Floor");
            var px = new Color[Size * Size];
            Color tileA = new Color(0.80f, 0.80f, 0.76f);
            Color tileB = new Color(0.74f, 0.75f, 0.72f);
            Color grout = new Color(0.55f, 0.56f, 0.55f);
            const int tile = Size / 2;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int tx = x / tile, ty = y / tile;
                Color c = ((tx + ty) % 2 == 0) ? tileA : tileB;
                c *= 0.95f + Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.08f;

                int lx = x % tile, ly = y % tile;
                if (lx < 2 || ly < 2) c = grout;
                else if (lx < 4 || ly < 4) c *= 0.93f;            // pequena sombra junto à junta
                else if (lx > tile - 3 || ly > tile - 3) c *= 1.04f; // brilho no lado oposto

                c.a = 1f;
                px[y * Size + x] = c;
            }
            return Finish(tex, px);
        }

        // ------------------------------------------------------------------ Parede de tijolo
        private static Texture2D MakeWall()
        {
            var tex = NewTexture("Wall");
            var px = new Color[Size * Size];
            Color brick = new Color(0.40f, 0.43f, 0.50f);
            Color mortar = new Color(0.25f, 0.27f, 0.31f);
            const int rowH = 32, brickW = 64;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int row = y / rowH;
                int offset = (row % 2) * (brickW / 2);
                int lx = (x + offset) % brickW, ly = y % rowH;
                int brickId = ((x + offset) / brickW) * 7 + row * 13;

                Color c = brick * (0.9f + (brickId % 5) * 0.035f);
                c *= 0.94f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.1f;
                if (lx < 3 || ly < 3) c = mortar;
                else if (ly > rowH - 4) c *= 0.85f;

                c.a = 1f;
                px[y * Size + x] = c;
            }
            return Finish(tex, px);
        }

        // ------------------------------------------------------------------ Mancha de sangue (com transparência)
        private static Texture2D _blood;
        public static Texture2D BloodSplat => _blood != null ? _blood : (_blood = MakeBloodSplat());

        private static Texture2D MakeBloodSplat()
        {
            var tex = NewTexture("BloodSplat");
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[Size * Size];
            var rng = new System.Random(7);

            // gotas satélite à volta da mancha principal
            var drops = new Vector3[9];
            for (int i = 0; i < drops.Length; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = 0.55f + (float)rng.NextDouble() * 0.35f;
                drops[i] = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.04f + (float)rng.NextDouble() * 0.07f);
            }

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (Size - 1f) * 2f - 1f;
                float v = y / (Size - 1f) * 2f - 1f;
                float dist = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);

                // contorno irregular (ruído em função do ângulo)
                float edge = 0.42f + Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + 3f, Mathf.Sin(angle) * 1.6f + 3f) * 0.32f;
                float a1 = Mathf.Clamp01((edge - dist) * 25f);

                float a2 = 0f;
                foreach (var dr in drops)
                {
                    float dd = Mathf.Sqrt((u - dr.x) * (u - dr.x) + (v - dr.y) * (v - dr.y));
                    a2 = Mathf.Max(a2, Mathf.Clamp01((dr.z - dd) * 40f));
                }

                float alpha = Mathf.Max(a1, a2);
                float shade = 0.8f + Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.35f - dist * 0.15f; // mais escuro no centro
                px[y * Size + x] = new Color(shade, shade, shade, alpha);
            }
            return Finish(tex, px);
        }

        // ------------------------------------------------------------------ Helpers
        private static Texture2D NewTexture(string name)
        {
            return new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        private static Texture2D Finish(Texture2D tex, Color[] px)
        {
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
