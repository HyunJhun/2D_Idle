using UnityEngine;

namespace IdlePrototype
{
    public sealed class GrowthUpgradeAction : MonoBehaviour
    {
        public IdleGameUI ui;
        [Range(0,2)] public int statIndex;
        public int amount = 1;
        public void Execute() => ui.UpgradeBy(statIndex, amount);
    }
}
