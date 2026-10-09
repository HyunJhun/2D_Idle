using UnityEngine;
using UnityEngine.EventSystems;

namespace IdlePrototype
{
    public sealed class BattleTapAttack : MonoBehaviour, IPointerClickHandler
    {
        public StageBattleController battle;
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) battle.ManualAttack(); }
    }
}
