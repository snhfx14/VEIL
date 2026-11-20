using UnityEngine;

namespace AulaFSM.Enemy
{
    /// <summary>
    /// Asset editável no Inspector (Assets/FSM/Resources/FSM_GuardTypes.asset) com os 4 tipos de guarda.
    /// Para afinar um tipo: seleciona o asset na janela Project e muda os valores.
    /// (Está num ficheiro próprio porque o Unity exige que um ScriptableObject tenha o nome do ficheiro.)
    /// </summary>
    [CreateAssetMenu(fileName = "FSM_GuardTypes", menuName = "FSM/Tipos de Guarda")]
    public class GuardTypeSettings : ScriptableObject
    {
        public const string ResourceName = "FSM_GuardTypes";

        public GuardProfile normal = GuardProfile.Default(GuardType.Normal);
        public GuardProfile pesado = GuardProfile.Default(GuardType.Pesado);
        public GuardProfile atirador = GuardProfile.Default(GuardType.Atirador);
        public GuardProfile rapido = GuardProfile.Default(GuardType.Rapido);

        public GuardProfile Get(GuardType type)
        {
            switch (type)
            {
                case GuardType.Pesado: return pesado;
                case GuardType.Atirador: return atirador;
                case GuardType.Rapido: return rapido;
                default: return normal;
            }
        }

        private static GuardTypeSettings _cached;

        /// <summary>Perfil do tipo (cópia), vindo do asset se existir ou dos valores por omissão.</summary>
        public static GuardProfile ProfileFor(GuardType type)
        {
            if (_cached == null) _cached = Resources.Load<GuardTypeSettings>(ResourceName);
            return (_cached != null ? _cached.Get(type) : GuardProfile.Default(type)).Clone();
        }
    }
}
