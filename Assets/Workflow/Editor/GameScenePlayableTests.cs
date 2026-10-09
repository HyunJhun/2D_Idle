using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GameScenePlayableTests
{
    const string Running="Idle.PlayableVerification";
    static double started;
    static StageBattleController live;
    static readonly HashSet<string> visited=new HashSet<string>();
    static readonly HashSet<string> screenshots=new HashSet<string>();
    static string runtimeError;
    static bool attackSeen,deathSeen,skillSeen;
    static float firstGameTime;
    static GameScenePlayableTests()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running,false))
            {
                started=EditorApplication.timeSinceStartup;
                Application.logMessageReceived+=CaptureLog;
                EditorApplication.update+=Poll;
            }
        };
    }
    static void Require(bool value,string message) { if(!value) throw new Exception("PLAYABLE TEST FAILED: "+message); }
    static void ClearTo(StageRun run,int index)
    {
        int count=0;
        while(run.Index!=index && count++<40) run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Index==index,"Test setup target stage");
    }
    static StageBattleController Open()
    {
        EditorSceneManager.OpenScene(GameScenePlayableEditor.ScenePath);
        return Object.FindFirstObjectByType<StageBattleController>();
    }

    public static void Verify()
    {
        var battle=Open(); var arena=battle.arena;
        Require(arena && arena.hero && arena.enemies.Length==3,"Actor pool and hero references");
        var definitions=new[]{arena.heroSprites,arena.forestBoss,arena.ruinsBoss}.Concat(arena.forestMonsters).Concat(arena.ruinsMonsters);
        foreach(var definition in definitions)
            foreach(var frames in new[]{definition.idle,definition.walk,definition.attack,definition.hurt,definition.death})
                Require(frames.Length>1 && frames.All(s=>s && AssetDatabase.GetAssetPath(s.texture).StartsWith("Assets/Workflow/Sprites/")),"Workflow animation frames for "+definition.characterName);
        bool logging=Debug.unityLogger.logEnabled;
        var random=UnityEngine.Random.state;
        try
        {
            Debug.unityLogger.logEnabled=false;
            UnityEngine.Random.InitState(219);
            battle.BeginRun();
            Require(arena.ActiveCount==3 && arena.enemies.Select(e=>e.sprites.characterName).Distinct().Count()==3,"Three distinct monsters in first wave");
            float hp=battle.Run.EnemyHealth;
            battle.Simulate(.1f);Require(battle.Run.EnemyHealth==hp && !arena.Ready,"No remote attacks during approach");
            if(battle.ui.AutoEnabled) battle.ui.ToggleAuto();
            battle.Simulate(1);
            Require(arena.Ready && arena.hero.transform.localPosition.x>-3.5f,"Actors move into range");
            battle.UseSkill(0);
            Require(battle.Run.TotalKills==1 && arena.enemies[0].Dead && arena.enemies[0].AnimationState=="Death","Skill damage kills actor and starts death clip");
            battle.UseSkill(0);Require(battle.Run.TotalKills==1 && battle.SkillCooldown(0)>0,"Cooldown prevents repeat cast");
            battle.Simulate(1.5f);
            Require(!arena.enemies[0].gameObject.activeSelf && arena.Target==arena.enemies[1] && arena.Ready,"Dead monster removed and next monster approaches");
            hp=battle.Run.EnemyHealth;battle.ManualAttack();
            Require(battle.Run.EnemyHealth<hp,"Manual tap deals damage with AUTO off");
            hp=battle.Run.EnemyHealth;battle.ManualAttack();Require(battle.Run.EnemyHealth==hp,"Manual attack rate limit");

            battle.BeginRun();ClearTo(battle.Run,4);battle.Simulate(2);
            Require(arena.ActiveCount==1 && arena.Target.sprites==arena.forestBoss,"Forest boss uses one real boss actor");
            float playerHp=battle.PlayerHealth;battle.UseSkill(3);battle.Simulate(2);
            Require(battle.PlayerHealth==playerHp,"Frost blocks monster attacks");
            var stageEvents=new List<string>();battle.Run.EventOccurred+=(action,stage)=>stageEvents.Add(action+" "+stage.Id);
            battle.Simulate(41);
            Require(battle.Run.Current.Id=="1-4" && battle.Run.Farming && stageEvents.Contains("BossTimeout 1-5"),"Live arena 45-second timeout");
            battle.Simulate(2);Require(arena.ActiveCount==3,"Normal wave restored on boss failure");
            for(int i=0;i<9;i++)battle.Run.DamageEnemy(battle.Run.Current.monsterHealth);
            Require(battle.Run.Current.Id=="1-4" && battle.Run.Farming,"Farm cannot advance itself");
            battle.ChallengeBoss();Require(battle.Run.BossSeconds==45,"Retry resets timer");
            battle.Simulate(2);Require(arena.ActiveCount==1 && arena.Ready,"Retry replaces all normal enemies");
            ClearTo(battle.Run,9);battle.Simulate(45);
            Require(battle.Run.Current.Id=="2-4" && battle.Run.Farming && stageEvents.Contains("BossTimeout 2-5"),"Second boss timeout fallback");

            // Complete both chapters using only normal simulation and real skill handlers.
            battle.BeginRun();if(!battle.ui.AutoEnabled)battle.ui.ToggleAuto();
            var stages=new HashSet<string>();battle.Run.EventOccurred+=(action,stage)=>{if(action=="StageEnter")stages.Add(stage.Id);};stages.Add("1-1");
            float time=0;
            while(!battle.Run.Completed && time<400)
            {
                battle.Simulate(.05f);time+=.05f;
                for(int i=0;i<5;i++)if(arena.Ready && battle.SkillCooldown(i)<=0)battle.UseSkill(i);
            }
            Require(battle.Run.Completed && stages.Count==10 && battle.Run.TotalKills>=26,"Full two-chapter completion without forced kills");
            Debug.unityLogger.logEnabled=logging;
            Debug.Log("PLAYABLE_TESTS_PASSED: source assets, approach, 3-monster waves, death/replacement, manual attack, skill cooldown, frost, both boss timeouts, farming/retry; all 10 sections cleared in "+time.ToString("F1")+" simulated seconds.");
        }
        finally { Debug.unityLogger.logEnabled=logging;UnityEngine.Random.state=random; }
        Open();
    }

    public static void VerifyPlayMode()
    {
        Verify();
        SessionState.SetBool(Running,true);
        EditorApplication.EnterPlaymode();
    }
    public static void PolishAndVerifyPlayMode()
    {
        GameScenePlayableEditor.Polish();
        VerifyPlayMode();
    }
    static void CaptureLog(string text,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)runtimeError=text;
        if(text=="PlayerAttack event action")attackSeen=true;
        if(text.StartsWith("MonsterDefeated "))deathSeen=true;
        if(text.Contains(" Cast event action"))skillSeen=true;
    }
    static void Poll()
    {
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup-started>140)throw new Exception("Play-mode verification timed out.");
            if(live==null)
            {
                live=Object.FindFirstObjectByType<StageBattleController>();
                if(live==null || live.Run==null) {live=null;return;}
                firstGameTime=Time.time;
                Time.timeScale=8;
                visited.Add(live.Run.Current.Id);
                live.Run.EventOccurred+=(action,stage)=>{if(action=="StageEnter")visited.Add(stage.Id);};
            }
            if(live.arena.Ready)
            {
                string key=live.Run.Current.IsBoss?"boss-"+live.Run.Current.chapter:"chapter-"+live.Run.Current.chapter;
                bool readyForCapture=!live.Run.Current.IsBoss || live.Run.BossSeconds<40;
                if(readyForCapture && screenshots.Add(key))GameScenePlayableEditor.CapturePreview("Documentation/gameScene-playable-"+key+".png");
                for(int i=0;i<5;i++)if(live.arena.Ready && live.skillButtons[i].interactable)live.skillButtons[i].onClick.Invoke();
            }
            if(live.Run.Completed)
            {
                Require(visited.Count==10 && attackSeen && deathSeen && skillSeen,"Real Play-mode events for all 10 sections");
                string result="PLAY_MODE_PASSED: all 10 sections, both bosses, real Update movement/animation/attacks, serialized skill buttons; "+live.Run.TotalKills+" kills; "+(Time.time-firstGameTime).ToString("F1")+" game seconds. Verification ran at 8x; normal Play uses 1x.";
                File.WriteAllText("Documentation/gameScene-playable-test-results.txt",result);
                Finish(0,result);
            }
        }
        catch(Exception error) { Finish(1,"PLAY_MODE_FAILED: "+error); }
    }
    static void Finish(int code,string result)
    {
        EditorApplication.update-=Poll;Application.logMessageReceived-=CaptureLog;
        Time.timeScale=1;SessionState.EraseBool(Running);
        if(code==0)Debug.Log(result);else Debug.LogError(result);
        EditorApplication.Exit(code);
    }
}
