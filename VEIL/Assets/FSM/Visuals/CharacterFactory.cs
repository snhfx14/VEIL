using UnityEngine;

namespace AulaFSM.Visuals
{
    /// <summary>
    /// Constrói personagens "cartoon" vistos de cima, só com primitivas + contorno preto:
    /// cabeça grande, ombros largos, mãos à frente (estilo jogos stealth top-down).
    /// </summary>
    public static class CharacterFactory
    {
        public static readonly Color Skin = new Color(0.96f, 0.76f, 0.60f);
        private static readonly Color Pants = new Color(0.18f, 0.2f, 0.26f);

        public struct Rig
        {
            public Transform Model;      // raiz visual (roda ao morrer)
            public Renderer Torso;       // muda de cor com o estado
            public Transform WeaponPivot;
            public Transform AimLaser;   // só nos guardas
        }

        /// <summary>Corpo base partilhado pelo jogador e pelos guardas.</summary>
        private static Rig BuildBody(Transform root, Color shirt)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root, false);

            // Pernas (quase não se veem de cima, mas dão volume nas sombras)
            Gfx.Part(PrimitiveType.Capsule, "LegL", model, new Vector3(-0.17f, 0.42f, 0f), new Vector3(0.26f, 0.42f, 0.26f), Pants);
            Gfx.Part(PrimitiveType.Capsule, "LegR", model, new Vector3(0.17f, 0.42f, 0f), new Vector3(0.26f, 0.42f, 0.26f), Pants);

            // Tronco largo (ombros) — esta peça é pintada com a cor do estado
            var torso = Gfx.Part(PrimitiveType.Sphere, "Torso", model, new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 0.75f, 0.6f), shirt);

            // Braços esticados para a frente
            Gfx.Part(PrimitiveType.Capsule, "ArmL", model, new Vector3(-0.4f, 1.12f, 0.18f), new Vector3(0.22f, 0.26f, 0.22f), shirt, euler: new Vector3(75f, 0f, 0f));
            Gfx.Part(PrimitiveType.Capsule, "ArmR", model, new Vector3(0.4f, 1.12f, 0.18f), new Vector3(0.22f, 0.26f, 0.22f), shirt, euler: new Vector3(75f, 0f, 0f));
            Gfx.Part(PrimitiveType.Sphere, "HandL", model, new Vector3(-0.4f, 1.12f, 0.45f), Vector3.one * 0.24f, Skin);
            Gfx.Part(PrimitiveType.Sphere, "HandR", model, new Vector3(0.4f, 1.12f, 0.45f), Vector3.one * 0.24f, Skin);

            // Cabeça grande
            Gfx.Part(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 1.6f, 0.02f), Vector3.one * 0.55f, Skin);

            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0f, 1.12f, 0f);

            return new Rig { Model = model, Torso = torso.GetComponent<Renderer>(), WeaponPivot = pivot };
        }

        /// <summary>Jogador: assassino com gorro, bandana e faca na mão direita.</summary>
        public static Rig BuildPlayer(Transform root)
        {
            var rig = BuildBody(root, new Color(0.15f, 0.55f, 0.85f));

            // Gorro escuro + bandana vermelha
            Gfx.Part(PrimitiveType.Sphere, "Beanie", rig.Model, new Vector3(0f, 1.7f, -0.04f), new Vector3(0.56f, 0.36f, 0.54f), new Color(0.12f, 0.12f, 0.15f));
            Gfx.Part(PrimitiveType.Cylinder, "Bandana", rig.Model, new Vector3(0f, 1.62f, 0.01f), new Vector3(0.58f, 0.04f, 0.58f), new Color(0.85f, 0.15f, 0.15f), outline: false);
            // Mochila
            Gfx.Part(PrimitiveType.Cube, "Backpack", rig.Model, new Vector3(0f, 1.12f, -0.36f), new Vector3(0.55f, 0.5f, 0.25f), new Color(0.35f, 0.25f, 0.18f));

            // Faca de combate na mão direita
            BuildKnife(rig.WeaponPivot, new Vector3(0.4f, 0.02f, 0.47f));
            return rig;
        }

        /// <summary>
        /// Faca de combate: lâmina larga com ponta, gume brilhante, canal (fuller), guarda, cabo com fita e pomo.
        /// O eixo Z local aponta para a ponta.
        /// </summary>
        public static Transform BuildKnife(Transform parent, Vector3 localPos)
        {
            var knife = new GameObject("Knife").transform;
            knife.SetParent(parent, false);
            knife.localPosition = localPos;
            knife.localScale = Vector3.one * 1.35f;

            Color steel = new Color(0.80f, 0.84f, 0.90f);
            Color edge = new Color(0.97f, 0.98f, 1f);
            Color dark = new Color(0.22f, 0.23f, 0.27f);
            Color leather = new Color(0.20f, 0.13f, 0.08f);
            Color wrap = new Color(0.10f, 0.08f, 0.06f);
            const float ow = 0.012f;

            // Cabo
            Gfx.Part(PrimitiveType.Sphere, "Pommel", knife, new Vector3(0f, 0f, -0.2f), new Vector3(0.1f, 0.08f, 0.08f), dark, outlineWidth: ow, smoothness: 0.6f);
            Gfx.Part(PrimitiveType.Capsule, "Handle", knife, new Vector3(0f, 0f, -0.07f), new Vector3(0.085f, 0.12f, 0.07f), leather, euler: new Vector3(90f, 0f, 0f), outlineWidth: ow);
            for (int i = 0; i < 3; i++)
                Gfx.Part(PrimitiveType.Cube, "Wrap", knife, new Vector3(0f, 0f, -0.14f + i * 0.055f), new Vector3(0.092f, 0.078f, 0.018f), wrap, outline: false);

            // Guarda
            Gfx.Part(PrimitiveType.Cube, "Guard", knife, new Vector3(0f, 0f, 0.06f), new Vector3(0.26f, 0.06f, 0.05f), dark, outlineWidth: ow, smoothness: 0.6f);

            // Lâmina (corpo + ponta em diamante)
            Gfx.Part(PrimitiveType.Cube, "Blade", knife, new Vector3(0.005f, 0f, 0.31f), new Vector3(0.13f, 0.028f, 0.46f), steel, outlineWidth: ow, smoothness: 0.9f);
            Gfx.Part(PrimitiveType.Cube, "Tip", knife, new Vector3(0.005f, 0f, 0.54f), new Vector3(0.092f, 0.028f, 0.092f), steel, euler: new Vector3(0f, 45f, 0f), outlineWidth: ow, smoothness: 0.9f);

            // Gume brilhante (lado de baixo), canal escuro e serrilha na costa
            Gfx.Part(PrimitiveType.Cube, "Edge", knife, new Vector3(-0.055f, 0f, 0.31f), new Vector3(0.025f, 0.031f, 0.44f), edge, outline: false, smoothness: 1f);
            Gfx.Part(PrimitiveType.Cube, "Fuller", knife, new Vector3(0.022f, 0f, 0.27f), new Vector3(0.018f, 0.032f, 0.32f), new Color(0.55f, 0.58f, 0.63f), outline: false);
            for (int i = 0; i < 4; i++)
                Gfx.Part(PrimitiveType.Cube, "Serration", knife, new Vector3(0.07f, 0f, 0.14f + i * 0.045f), new Vector3(0.025f, 0.026f, 0.025f), steel,
                    euler: new Vector3(0f, 45f, 0f), outline: false, smoothness: 0.9f);

            return knife;
        }

        /// <summary>Guarda: capacete, óculos escuros e arma segura com as duas mãos.</summary>
        /// <summary>
        /// Guarda: capacete, óculos escuros e arma segura com as duas mãos.
        /// Cada tipo tem um pormenor que o identifica: Pesado (maior, ombreiras e colete grosso),
        /// Atirador (espingarda comprida com mira), Rápido (mais pequeno, boné, sem colete).
        /// </summary>
        public static Rig BuildGuard(Transform root, AulaFSM.Enemy.GuardType type, AulaFSM.Enemy.GuardProfile profile)
        {
            var rig = BuildBody(root, new Color(0.3f, 0.85f, 0.35f));
            rig.Model.localScale = Vector3.one * profile.modelScale;
            Color dark = new Color(0.12f, 0.12f, 0.14f);

            // Capacete (cor do tipo) + óculos
            if (type == AulaFSM.Enemy.GuardType.Rapido)
            {
                Gfx.Part(PrimitiveType.Sphere, "Cap", rig.Model, new Vector3(0f, 1.72f, -0.03f), new Vector3(0.56f, 0.3f, 0.56f), profile.helmetColor);
                Gfx.Part(PrimitiveType.Cube, "CapBrim", rig.Model, new Vector3(0f, 1.68f, 0.3f), new Vector3(0.4f, 0.05f, 0.25f), profile.helmetColor);
            }
            else
                Gfx.Part(PrimitiveType.Sphere, "Helmet", rig.Model, new Vector3(0f, 1.7f, -0.02f), new Vector3(0.6f, 0.38f, 0.6f), profile.helmetColor);
            Gfx.Part(PrimitiveType.Cube, "Glasses", rig.Model, new Vector3(0f, 1.6f, 0.24f), new Vector3(0.42f, 0.1f, 0.1f), new Color(0.05f, 0.05f, 0.06f), outline: false);

            // Colete (o pesado tem colete grosso e ombreiras; o rápido não usa colete)
            if (type == AulaFSM.Enemy.GuardType.Pesado)
            {
                Gfx.Part(PrimitiveType.Cube, "HeavyVest", rig.Model, new Vector3(0f, 1.15f, 0.02f), new Vector3(0.78f, 0.62f, 0.66f), new Color(0.16f, 0.17f, 0.2f));
                Gfx.Part(PrimitiveType.Sphere, "PadL", rig.Model, new Vector3(-0.42f, 1.38f, 0f), new Vector3(0.34f, 0.22f, 0.34f), profile.helmetColor);
                Gfx.Part(PrimitiveType.Sphere, "PadR", rig.Model, new Vector3(0.42f, 1.38f, 0f), new Vector3(0.34f, 0.22f, 0.34f), profile.helmetColor);
            }
            else if (type != AulaFSM.Enemy.GuardType.Rapido)
                Gfx.Part(PrimitiveType.Cube, "Vest", rig.Model, new Vector3(0f, 1.15f, 0.05f), new Vector3(0.6f, 0.55f, 0.55f), new Color(0.2f, 0.22f, 0.25f), outline: false);

            // Arma à frente, entre as mãos (o atirador tem uma espingarda comprida com mira)
            if (type == AulaFSM.Enemy.GuardType.Atirador)
            {
                Gfx.Part(PrimitiveType.Cube, "Rifle", rig.WeaponPivot, new Vector3(0.15f, 0.05f, 0.85f), new Vector3(0.11f, 0.13f, 1.05f), dark, outlineWidth: 0.025f);
                Gfx.Part(PrimitiveType.Cylinder, "Scope", rig.WeaponPivot, new Vector3(0.15f, 0.16f, 0.75f), new Vector3(0.07f, 0.16f, 0.07f), new Color(0.25f, 0.25f, 0.28f), euler: new Vector3(90f, 0f, 0f), outlineWidth: 0.015f);
            }
            else
                Gfx.Part(PrimitiveType.Cube, "Gun", rig.WeaponPivot, new Vector3(0.15f, 0.05f, 0.7f), new Vector3(0.14f, 0.16f, 0.7f), dark, outlineWidth: 0.025f);
            Gfx.Part(PrimitiveType.Cube, "Flashlight", rig.WeaponPivot, new Vector3(0.15f, 0.05f, 1.07f), new Vector3(0.1f, 0.1f, 0.06f), new Color(1f, 0.95f, 0.6f), outline: false);

            // Laser de mira (o pai é escalado em Z para mudar o comprimento)
            var laser = new GameObject("AimLaser").transform;
            laser.SetParent(root, false);
            laser.localPosition = new Vector3(0.15f, 1.17f, 1.1f);
            var beam = Gfx.Part(PrimitiveType.Cube, "Beam", laser, new Vector3(0f, 0f, 0.5f), new Vector3(0.05f, 0.05f, 1f), new Color(1f, 0.1f, 0.1f), outline: false);
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rig.AimLaser = laser;
            return rig;
        }

        /// <summary>Cristal (diamante): dois "cones" feitos de cubos rodados.</summary>
        public static GameObject BuildGem(Transform parent, Vector3 position)
        {
            var gem = new GameObject("Crystal");
            gem.transform.SetParent(parent, false);
            gem.transform.position = position;
            var color = new Color(0.25f, 0.95f, 0.95f);
            Gfx.Part(PrimitiveType.Cube, "Core", gem.transform, Vector3.zero, Vector3.one * 0.5f, color, euler: new Vector3(45f, 0f, 45f));
            Gfx.Part(PrimitiveType.Cube, "Shine", gem.transform, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.22f, new Color(0.85f, 1f, 1f), outline: false, euler: new Vector3(45f, 45f, 0f));
            return gem;
        }
    }
}
