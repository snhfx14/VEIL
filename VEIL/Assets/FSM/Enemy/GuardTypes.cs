using System;
using UnityEngine;

namespace AulaFSM.Enemy
{
    public enum GuardType
    {
        Normal,    // equilibrado
        Pesado,    // mais resistente e mais lento
        Atirador,  // maior alcance e precisão (antecipa o movimento do jogador)
        Rapido     // mais rápido a andar e a reagir, menos resistente
    }

    /// <summary>
    /// Atributos de um tipo de guarda. São MULTIPLICADORES/BÓNUS aplicados por cima dos valores base
    /// do EnemyAI — os parâmetros de disparo (cadência, dano, velocidade da bala) NÃO são alterados.
    /// </summary>
    [Serializable]
    public class GuardProfile
    {
        public string displayName = "Guarda";

        [Header("Resistência")]
        public float maxHealth = 100f;

        [Header("Movimento")]
        [Tooltip("Multiplica as velocidades de patrulha, investigação, procura e perseguição.")]
        public float speedMultiplier = 1f;

        [Header("Perceção")]
        public float viewDistanceMultiplier = 1f;
        [Range(20f, 180f)] public float viewAngle = 90f;
        public float hearingRadius = 6f;
        [Tooltip(">1 deteta e reage mais depressa; <1 mais devagar.")]
        public float reactionMultiplier = 1f;

        [Header("Combate (bónus, não altera o sistema de disparo)")]
        [Tooltip("Metros somados ao alcance de ataque base.")]
        public float attackRangeBonus = 0f;
        [Range(0f, 1f), Tooltip("0 = aponta para onde o jogador está; 1 = antecipa o movimento (mais preciso).")]
        public float aimLead = 0f;

        [Header("Visual")]
        public float modelScale = 1f;
        public Color helmetColor = new Color(0.22f, 0.25f, 0.3f);

        public GuardProfile Clone() => (GuardProfile)MemberwiseClone();

        /// <summary>Valores por omissão de cada tipo (usados se o asset não existir).</summary>
        public static GuardProfile Default(GuardType type)
        {
            switch (type)
            {
                case GuardType.Pesado:
                    return new GuardProfile
                    {
                        displayName = "Pesado", maxHealth = 220f, speedMultiplier = 0.75f, viewDistanceMultiplier = 0.95f,
                        viewAngle = 80f, hearingRadius = 5f, reactionMultiplier = 0.85f, modelScale = 1.18f,
                        helmetColor = new Color(0.12f, 0.13f, 0.15f)
                    };
                case GuardType.Atirador:
                    return new GuardProfile
                    {
                        displayName = "Atirador", maxHealth = 90f, speedMultiplier = 0.9f, viewDistanceMultiplier = 1.35f,
                        viewAngle = 60f, hearingRadius = 5f, reactionMultiplier = 1f, attackRangeBonus = 4f, aimLead = 1f,
                        modelScale = 1f, helmetColor = new Color(0.35f, 0.28f, 0.15f)
                    };
                case GuardType.Rapido:
                    return new GuardProfile
                    {
                        displayName = "Rápido", maxHealth = 70f, speedMultiplier = 1.35f, viewDistanceMultiplier = 0.9f,
                        viewAngle = 100f, hearingRadius = 7f, reactionMultiplier = 1.4f, modelScale = 0.9f,
                        helmetColor = new Color(0.55f, 0.12f, 0.12f)
                    };
                default:
                    return new GuardProfile { displayName = "Normal" };
            }
        }
    }
}
