using UnityEngine;
using UnityEngine.UI;

namespace IdlePrototype
{
    // Session-only demo stats, shared by the growth UI and sample battle.
    public class IdleGameUI : MonoBehaviour
    {
        public Text goldLabel, attackLabel, healthLabel, criticalLabel, toastLabel, panelTitle, autoLabel;
        public GameObject growthPanel, infoPanel;
        public Text infoText;
        public Button[] navigation;
        public Text[] statValues;
        public const int InitialAttack = 120, InitialHealth = 80, InitialCritical = 25;
        public StageBattleController battle;
        int gold = 125000, attack = InitialAttack, health = InitialHealth, critical = InitialCritical;
        bool auto = true;
        float toastUntil;
        public bool AutoEnabled => auto;
        public int Gold => gold;
        public int AttackLevel => attack;
        public int HealthLevel => health;
        public int CriticalLevel => critical;
        public float AttackDamage => attack * 104f;
        public float MaxHealth => health * 1065f;
        public float CriticalChance => Mathf.Clamp01(critical * .005f);
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            gold += amount;
            goldLabel.text = gold.ToString("N0");
            Debug.Log("GoldReward event action", this);
        }
        public void SelectTab(int index)
        {
            if (index < 0 || index >= navigation.Length) return;
            string[] titles = { "GROWTH", "EQUIPMENT", "SKILLS", "DUNGEON", "SUMMON", "SHOP" };
            panelTitle.text = titles[index];
            growthPanel.SetActive(index == 0);
            infoPanel.SetActive(index != 0);
            infoText.text = titles[index] + "\n\nContent preview\nThis system will be connected next.";
            for (int i = 0; i < navigation.Length; i++)
                navigation[i].GetComponent<Image>().color = i == index ? new Color32(161, 111, 45, 255) : new Color32(30, 39, 54, 255);
        }
        public void Upgrade(int index)
        {
            UpgradeBy(index, 1);
        }
        public static int UpgradeCost(int index, int amount) => (index == 0 ? 1200 : index == 1 ? 950 : 2400) * amount;
        public void UpgradeBy(int index, int amount)
        {
            if (index < 0 || index > 2 || (amount != 1 && amount != 10 && amount != 100)) return;
            int level = index == 0 ? attack : index == 1 ? health : critical;
            if (level > int.MaxValue - amount || (index == 2 && critical + amount > 200))
            {
                Debug.Log("UpgradeFailed event action", this);
                Notify(index == 2 ? "Critical max: Lv.200 (100%). Choose a smaller upgrade." : "Maximum level reached"); return;
            }
            int cost = UpgradeCost(index, amount);
            if (gold < cost) { Debug.Log("UpgradeFailed event action", this); Notify("Not enough gold for +" + amount); return; }
            gold -= cost;
            if (index == 0) attack += amount;
            else if (index == 1) health += amount;
            else critical += amount;
            RefreshStats();
            if (battle != null) battle.RefreshView();
            Notify("Upgrade +" + amount + " complete");
            Debug.Log("UpgradeComplete event action", this);
        }
        public void RefreshStats()
        {
            goldLabel.text = gold.ToString("N0");
            attackLabel.text = "ATK  /  Lv. " + attack;
            healthLabel.text = "HP  /  Lv. " + health;
            criticalLabel.text = "CRIT  /  Lv. " + critical;
            statValues[0].text = (attack * 104f).ToString("N0") + "  >  " + ((attack + 1f) * 104f).ToString("N0");
            statValues[1].text = (health * 1065f).ToString("N0") + "  >  " + ((health + 1f) * 1065f).ToString("N0");
            statValues[2].text = (critical * .5f).ToString("F1") + "%  >  " + Mathf.Min(100,(critical + 1) * .5f).ToString("F1") + "%";
        }
        public void ResetStats()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var button in GetComponentsInChildren<HoldRepeatButton>(true)) button.CancelHold();
            attack = InitialAttack; health = InitialHealth; critical = InitialCritical;
            RefreshStats();
            if (battle != null) battle.ResetPlayerVitals();
            Notify("Stats reset to initial values");
#endif
        }
        public void ToggleAuto() { auto = !auto; autoLabel.text = auto ? "AUTO ON" : "AUTO OFF"; }
        public void Notify(string message) { toastLabel.text = message; toastLabel.transform.parent.gameObject.SetActive(true); toastUntil = Time.unscaledTime + 2.5f; }
        void Update() { if (toastLabel.transform.parent.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastLabel.transform.parent.gameObject.SetActive(false); }
    }
}
