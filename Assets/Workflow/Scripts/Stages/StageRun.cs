using System;

namespace IdlePrototype
{
    // Progression rules are independent of UI, frame rate and scene objects.
    public sealed class StageRun
    {
        readonly StageDefinition[] stages;
        readonly float timeLimit;
        public event Action<string, StageDefinition> EventOccurred;
        public int Index { get; private set; }
        public int Kills { get; private set; }
        public int TotalKills { get; private set; }
        public float EnemyHealth { get; private set; }
        double bossRemaining;
        public float BossSeconds => (float)bossRemaining;
        public bool Farming { get; private set; }
        public bool Completed { get; private set; }
        public bool Started { get; private set; }
        public StageDefinition Current => stages[Index];
        public bool CanChallengeBoss => Started && Farming && Current.section == 4;

        public StageRun(StageDefinition[] definitions, float bossTimeLimit = 45f)
        {
            if (definitions == null || definitions.Length != 10)
                throw new ArgumentException("The sample requires two chapters of five sections.");
            if (float.IsNaN(bossTimeLimit) || float.IsInfinity(bossTimeLimit) || bossTimeLimit <= 0)
                throw new ArgumentException("Boss time must be positive and finite.");
            for (int i = 0; i < definitions.Length; i++)
            {
                var d = definitions[i];
                if (d == null || d.chapter != i / 5 + 1 || d.section != i % 5 + 1 ||
                    !Positive(d.monsterHealth) || !Positive(d.monsterAttack) || d.killsToClear < 1 ||
                    d.goldReward < 0 || (d.IsBoss && d.killsToClear != 1))
                    throw new ArgumentException("Invalid stage at index " + i);
                if (i > 0 && (d.monsterHealth <= definitions[i - 1].monsterHealth || d.monsterAttack <= definitions[i - 1].monsterAttack))
                    throw new ArgumentException("Monster HP and ATK must increase at every section.");
            }
            stages = definitions;
            timeLimit = bossTimeLimit;
        }

        static bool Positive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        void Emit(string action) => EventOccurred?.Invoke(action, Current);
        public void Start()
        {
            TotalKills = 0; Completed = false; Started = true;
            Enter(0, false);
        }

        void Enter(int index, bool farming)
        {
            Index = index; Farming = farming; Kills = 0;
            bossRemaining = Current.IsBoss ? timeLimit : 0;
            Emit("StageEnter");
            if (Current.IsBoss) Emit("BossStart");
            if (Farming) Emit("FarmingStart");
            Spawn();
        }

        void Spawn() { EnemyHealth = Current.monsterHealth; Emit("MonsterSpawn"); }

        public void Tick(float seconds)
        {
            if (!Started || !Current.IsBoss || !Positive(seconds)) return;
            bossRemaining = Math.Max(0, bossRemaining - seconds);
            if (bossRemaining <= .000001)
            {
                Emit("BossTimeout");
                Enter(Index - 1, true);
            }
        }

        public void DamageEnemy(float damage)
        {
            if (!Started || !Positive(damage)) return;
            EnemyHealth = Math.Max(0, EnemyHealth - damage);
            if (EnemyHealth > 0) return;
            TotalKills++; Kills++;
            Emit("MonsterDefeated");
            if (Current.IsBoss)
            {
                Emit("BossClear"); Emit("ChapterClear");
                if (Index == stages.Length - 1)
                {
                    Completed = true;
                    Emit("SampleComplete");
                    Enter(Index - 1, true);
                }
                else Enter(Index + 1, false);
            }
            else if (Kills >= Current.killsToClear)
            {
                if (Farming) { Kills = 0; Emit("FarmingCycle"); Spawn(); }
                else { Emit("StageClear"); Enter(Index + 1, false); }
            }
            else Spawn();
        }

        public bool ChallengeBoss()
        {
            if (!CanChallengeBoss) return false;
            Emit("BossRetry");
            Enter(Index + 1, false);
            return true;
        }

        public void PlayerDefeated()
        {
            if (!Started) return;
            Emit("PlayerDefeated");
            if (Current.IsBoss) { Emit("BossFailed"); Enter(Index - 1, true); }
            else Enter(Index, Farming);
        }
    }
}
