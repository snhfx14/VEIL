using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM.Enemy
{
    /// <summary>
    /// Coordenação entre guardas ("rádio" partilhado do nível):
    ///  - nível de alerta da ZONA (sobe com cada cadáver descoberto),
    ///  - registo de cadáveres já descobertos (para não repetir reações),
    ///  - pontos de procura reservados (cada guarda procura num sítio diferente),
    ///  - avisos por rádio aos colegas próximos.
    /// É reiniciado sempre que um nível é carregado.
    /// </summary>
    public static class GuardSquad
    {
        public const int MaxZoneAlert = 3;

        /// <summary>0 = calmo … 3 = zona em alerta máximo.</summary>
        public static int ZoneAlert { get; private set; }

        private static readonly HashSet<EnemyAI> DiscoveredCorpses = new HashSet<EnemyAI>();
        private static readonly Dictionary<EnemyAI, Vector3> SearchClaims = new Dictionary<EnemyAI, Vector3>();

        public static void Reset()
        {
            ZoneAlert = 0;
            DiscoveredCorpses.Clear();
            SearchClaims.Clear();
        }

        // ------------------------------------------------------------------ Cadáveres

        public static bool IsDiscovered(EnemyAI corpse) => DiscoveredCorpses.Contains(corpse);

        /// <summary>Regista um cadáver novo. Devolve true se ainda ninguém o tinha descoberto.</summary>
        public static bool RegisterCorpse(EnemyAI corpse)
        {
            if (corpse == null || !DiscoveredCorpses.Add(corpse)) return false;
            ZoneAlert = Mathf.Min(MaxZoneAlert, ZoneAlert + 1);
            return true;
        }

        // ------------------------------------------------------------------ Rádio

        /// <summary>
        /// Avisa os guardas num raio: passam a procurar perto de "position".
        /// Os que já estão em combate ignoram; cada guarda tem um tempo mínimo entre avisos.
        /// </summary>
        public static int Broadcast(EnemyAI sender, Vector3 position, float radius)
        {
            int count = 0;
            foreach (var ally in EnemyAI.All)
            {
                if (ally == null || ally == sender || ally.Health.IsDead) continue;
                if (Vector3.Distance(ally.transform.position, sender.transform.position) > radius) continue;
                if (ally.ReceiveAlert(position)) count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ Procura coordenada

        /// <summary>
        /// Escolhe um ponto de procura perto de "center" que fique longe dos pontos já
        /// reservados pelos outros guardas (assim espalham-se em vez de irem todos ao mesmo sítio).
        /// </summary>
        public static Vector3 ClaimSearchPoint(EnemyAI guard, Vector3 center, float radius, float minSeparation)
        {
            Vector3 best = center;
            float bestScore = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                if (!EnemyAI.RandomPointNear(center, radius, out var p)) continue;
                float nearest = float.MaxValue;
                foreach (var kv in SearchClaims)
                    if (kv.Key != guard && kv.Key != null) nearest = Mathf.Min(nearest, Vector3.Distance(kv.Value, p));
                // preferir pontos afastados dos colegas e não demasiado perto do próprio guarda
                float score = Mathf.Min(nearest, minSeparation * 2f) + Random.value;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            SearchClaims[guard] = best;
            return best;
        }

        public static void ReleaseSearchPoint(EnemyAI guard) => SearchClaims.Remove(guard);
    }
}
