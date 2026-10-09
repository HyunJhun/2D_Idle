using UnityEngine;
using UnityEngine.UI;

namespace IdlePrototype
{
    public sealed class StageBattleController : MonoBehaviour
    {
        public StageCatalog catalog;
        public IdleGameUI ui;
        public Text chapterLabel, stageLabel, waveLabel, timerLabel, enemyLabel, damageLabel, questLabel, bossLabel;
        public Image enemyImage, battleBackground;
        public RectTransform enemyHealthBar, playerHealthBar;
        public Button bossButton;
        public Sprite normalSprite, bossSprite;
        public BattleArena arena;
        public Button[] skillButtons;
        public Text[] skillLabels;
        static readonly string[] SkillNames = { "SLASH", "STORM", "FLAME", "FROST", "RUSH" };
        static readonly float[] SkillDamage = { 2.4f, 3.5f, 5f, 1.8f, 2.8f };
        static readonly float[] SkillPeriods = { 4, 7, 10, 8, 8 };
        readonly float[] skillCooldowns = new float[5];
        [Min(.1f)] public float playerAttackInterval = 1f;
        [Min(.1f)] public float monsterAttackInterval = 1f;
        public StageRun Run { get; private set; }
        public float PlayerHealth { get; private set; }
        float playerClock, monsterClock, manualCooldown, stun;
        public float SkillCooldown(int index) => skillCooldowns[index];

        void Start() => BeginRun();
        public void BeginRun()
        {
            if (Run != null) Run.EventOccurred -= OnStageEvent;
            System.Array.Clear(skillCooldowns,0,skillCooldowns.Length);
            manualCooldown = stun = 0;
            if (arena != null) arena.Initialize();
            Run = new StageRun(catalog.stages, catalog.bossTimeLimit);
            Run.EventOccurred += OnStageEvent;
            Run.Start();
            RefreshView();
        }

        void Update() => Simulate(Time.deltaTime);

        public void Simulate(float seconds)
        {
            if (Run == null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            // Small deterministic simulation steps also drive movement and sprite animation.
            // The deadline is processed before attacks at the same timestamp.
            double remaining = seconds;
            while (remaining > .00000001)
            {
                float playerPeriod = Mathf.Max(.1f, playerAttackInterval);
                float monsterPeriod = Mathf.Max(.1f, monsterAttackInterval);
                float step = (float)System.Math.Min(remaining, 1.0 / 60.0);
                if (Run.Current.IsBoss) step = Mathf.Min(step, Run.BossSeconds);
                int previous = Run.Index;
                for (int i=0;i<skillCooldowns.Length;i++) skillCooldowns[i]=Mathf.Max(0,skillCooldowns[i]-step);
                manualCooldown = Mathf.Max(0,manualCooldown-step);
                stun = Mathf.Max(0,stun-step);
                Run.Tick(step);
                remaining -= step;
                if (arena != null) arena.Advance(step);
                if (Run.Index != previous) continue;
                if (arena != null && !arena.Ready) { playerClock = monsterClock = 0; continue; }
                playerClock = ui.AutoEnabled ? playerClock + step : 0;
                if (stun <= 0) monsterClock += step;

                if (ui.AutoEnabled && playerClock >= playerPeriod - .00001f)
                {
                    playerClock = 0;
                    float damage = ui.AttackDamage;
                    bool critical = Random.value < ui.CriticalChance;
                    if (critical) damage *= 2;
                    Debug.Log("PlayerAttack event action", this);
                    HitEnemy(damage,critical);
                    if (Run.Index != previous) continue;
                }
                if (monsterClock >= monsterPeriod - .00001f)
                {
                    monsterClock = 0;
                    PlayerHealth = Mathf.Max(0, PlayerHealth - Run.Current.monsterAttack);
                    if (arena != null) arena.MonsterStrike(Run.Current.monsterAttack);
                    Debug.Log("MonsterAttack event action", this);
                    if (PlayerHealth <= 0) Run.PlayerDefeated();
                }
            }
            RefreshView();
        }

        void HitEnemy(float damage,bool critical,int skill=-1)
        {
            damageLabel.text=damage.ToString("N0")+(critical?"!":"");
            if (arena != null) arena.PlayerStrike(damage,critical,skill);
            Run.DamageEnemy(damage);
        }

        public void ManualAttack()
        {
            if (Run == null || manualCooldown > 0 || (arena != null && !arena.Ready)) return;
            manualCooldown=.35f;
            Debug.Log("ManualAttack event action",this);
            HitEnemy(ui.AttackDamage*.65f,false); RefreshView();
        }

        public void UseSkill(int index)
        {
            if (Run == null || index < 0 || index >= skillCooldowns.Length || skillCooldowns[index] > 0) return;
            if (arena != null && !arena.Ready) { ui.Notify("Enemies are approaching..."); return; }
            skillCooldowns[index]=SkillPeriods[index];
            if(index==3) stun=3;
            if(index==4) PlayerHealth=Mathf.Min(ui.MaxHealth,PlayerHealth+ui.MaxHealth*.2f);
            Debug.Log(SkillNames[index]+" Cast event action",this);
            HitEnemy(ui.AttackDamage*SkillDamage[index],false,index); RefreshView();
        }

        void OnStageEvent(string action, StageDefinition stage)
        {
            Debug.Log(action + " " + stage.Id + " event action", this);
            if (action == "StageEnter")
            {
                PlayerHealth = ui.MaxHealth;
                playerClock = monsterClock = stun = 0;
                if (arena != null) arena.EnterStage(stage);
            }
            if (action == "FarmingCycle" && arena != null) arena.EnterStage(stage);
            if (action == "MonsterSpawn")
            {
                playerClock = monsterClock = stun = 0;
                if (arena != null) arena.SpawnCurrent(Run.Kills);
            }
            if (action == "MonsterDefeated")
            {
                if (arena != null) arena.DefeatTarget();
                ui.AddGold(stage.goldReward);
                PlayerHealth = Mathf.Min(ui.MaxHealth, PlayerHealth + ui.MaxHealth * .15f);
            }
            if (action == "PlayerDefeated" && arena != null) arena.hero.Die();
            if (action == "BossTimeout") ui.Notify("Time over! Returning to " + stage.chapter + "-4");
            if (action == "BossFailed") ui.Notify("Defeated! Returning to " + stage.chapter + "-4");
            if (action == "BossClear") ui.Notify("Boss cleared: " + stage.Id);
            if (action == "SampleComplete") ui.Notify("All 2 chapters cleared! Repeat hunt unlocked.");
        }

        public void ChallengeBoss()
        {
            if (Run == null || !Run.ChallengeBoss()) return;
            RefreshView();
        }

        public void ResetPlayerVitals()
        {
            PlayerHealth = ui.MaxHealth;
            RefreshView();
        }

        public void RefreshView()
        {
            if (Run == null) return;
            var stage = Run.Current;
            if(arena != null)
            {
                arena.hero.SetHealth(PlayerHealth,ui.MaxHealth);
                if(arena.Target != null && !arena.Target.Dead) arena.Target.SetHealth(Run.EnemyHealth,stage.monsterHealth);
            }
            if(skillButtons != null && skillLabels != null)
                for(int i=0;i<Mathf.Min(5,skillButtons.Length);i++)
                {
                    skillButtons[i].interactable=skillCooldowns[i]<=0;
                    skillLabels[i].text=SkillNames[i]+(skillCooldowns[i]>0?"\n"+skillCooldowns[i].ToString("F1")+"s":"\nREADY");
                }
            chapterLabel.text = "CHAPTER " + stage.chapter.ToString("00") + " / " + (stage.chapter == 1 ? "EMERALD WILDS" : "ANCIENT RUINS");
            stageLabel.text = stage.Id + "  " + stage.areaName + (stage.IsBoss ? " / BOSS" : "");
            waveLabel.text = stage.IsBoss ? "BOSS CHALLENGE" : (Run.Farming ? "REPEAT HUNT  " : "MONSTERS  ") + Run.Kills + " / " + stage.killsToClear;
            timerLabel.text = stage.IsBoss ? "TIME LEFT  " + Run.BossSeconds.ToString("F1") + "s" : Run.Completed ? "ALL CHAPTERS CLEARED" : Run.Farming ? "FARMING / RETRY WHEN READY" : "AUTO STAGE PROGRESSION";
            timerLabel.color = stage.IsBoss && Run.BossSeconds <= 10 ? new Color32(255,110,100,255) : new Color32(231,189,114,255);
            enemyLabel.text = (stage.IsBoss ? "BOSS" : "MONSTER") + "  HP " + Run.EnemyHealth.ToString("N0") + " / " + stage.monsterHealth.ToString("N0") + "  ATK " + stage.monsterAttack.ToString("N0");
            enemyHealthBar.sizeDelta = new Vector2(300 * Run.EnemyHealth / stage.monsterHealth, 6);
            playerHealthBar.sizeDelta = new Vector2(118 * PlayerHealth / ui.MaxHealth, 6);
            questLabel.text = "HUNT  " + stage.Id + "    Total defeated: " + Run.TotalKills;
            bossButton.interactable = Run.CanChallengeBoss;
            bossLabel.text = stage.IsBoss ? "IN BATTLE" : Run.CanChallengeBoss ? "RETRY BOSS" : "BOSS AT " + stage.chapter + "-5";
            if (arena == null && enemyImage != null)
            {
                enemyImage.sprite = stage.IsBoss ? bossSprite : normalSprite;
                enemyImage.rectTransform.sizeDelta = stage.IsBoss ? new Vector2(160,155) : new Vector2(120,125);
                enemyImage.rectTransform.anchoredPosition = stage.IsBoss ? new Vector2(425,-210) : new Vector2(445,-240);
            }
            battleBackground.color = stage.chapter == 1 ? new Color32(37,60,66,255) : new Color32(53,43,65,255);
        }
    }
}
