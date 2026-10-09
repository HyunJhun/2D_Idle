using System;
using UnityEngine;

namespace IdlePrototype
{
    [Serializable]
    public sealed class StageDefinition
    {
        public int chapter;
        public int section;
        public string areaName;
        [Min(1)] public float monsterHealth;
        [Min(1)] public float monsterAttack;
        [Min(1)] public int killsToClear = 3;
        [Min(0)] public int goldReward;
        public string Id => chapter + "-" + section;
        public bool IsBoss => section == 5;
    }

    [CreateAssetMenu(menuName = "Idle/Stage Catalog", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [Min(1)] public float bossTimeLimit = 45f;
        public StageDefinition[] stages;

        public static StageDefinition[] CreateSampleStages()
        {
            var result = new StageDefinition[10];
            for (int i = 0; i < result.Length; i++)
            {
                // Boss multipliers remain in subsequent stages, so stats never decrease.
                int bossesReached = (i + 1) / 5;
                result[i] = new StageDefinition
                {
                    chapter = i / 5 + 1, section = i % 5 + 1,
                    areaName = i < 5 ? "Silent Forest" : "Forgotten Ruins",
                    monsterHealth = Mathf.Ceil(12000 * Mathf.Pow(1.3f, i) * Mathf.Pow(4, bossesReached)),
                    monsterAttack = Mathf.Ceil(80 * Mathf.Pow(1.2f, i) * Mathf.Pow(2, bossesReached)),
                    killsToClear = i % 5 == 4 ? 1 : 3,
                    goldReward = (i + 1) * (i % 5 == 4 ? 1000 : 150)
                };
            }
            return result;
        }
    }
}
