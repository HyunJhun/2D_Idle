using System;
using System.Collections.Generic;
using System.Linq;
using IdlePrototype;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameSceneStagesEditor
{
    const string ScenePath = "Assets/Workflow/Scenes/gameScene.unity";
    const string CatalogPath = "Assets/Workflow/Data/StageCatalog.asset";

    [MenuItem("Tools/Idle UI/Apply stage progression")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        Configure(Object.FindFirstObjectByType<IdleGameUI>());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("STAGE_SCENE_UPDATED");
    }

    // Also used when the scene builder creates a fresh gameScene.
    public static void Configure(IdleGameUI ui)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Workflow/Data")) AssetDatabase.CreateFolder("Assets/Workflow", "Data");
            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            catalog.stages = StageCatalog.CreateSampleStages();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        // Validate inspector edits before touching the scene.
        _ = new StageRun(catalog.stages, catalog.bossTimeLimit);
        var controller = Object.FindFirstObjectByType<StageBattleController>();
        if (controller == null) controller = new GameObject("SYS_StageBattle").AddComponent<StageBattleController>();
        controller.catalog = catalog; controller.ui = ui;
        var nodes = ui.GetComponentsInChildren<Transform>(true);
        T Find<T>(string name) where T : Component => nodes.First(n => n.name == name).GetComponent<T>();
        var battle = Find<RectTransform>("BG_Battle");
        controller.chapterLabel = Find<Text>("TXT_Chapter");
        controller.stageLabel = Find<Text>("TXT_Stage");
        controller.stageLabel.fontSize = 26;
        controller.waveLabel = Find<Text>("TXT_Wave");
        controller.damageLabel = Find<Text>("TXT_Damage");
        controller.damageLabel.text = "READY";
        controller.questLabel = Find<Text>("TXT_Quest");
        controller.bossButton = Find<Button>("BTN_Boss");
        controller.bossLabel = Find<Text>("TXT_Boss");
        controller.bossLabel.fontSize = 16;
        // Replace the old fixed quest-count preview with neutral UI feedback.
        var questButton = Find<Button>("BTN_Quest");
        for (int i = 0; i < questButton.onClick.GetPersistentEventCount(); i++)
            if (questButton.onClick.GetPersistentTarget(i) == ui && questButton.onClick.GetPersistentMethodName(i) == nameof(IdleGameUI.Notify))
                UnityEventTools.RegisterStringPersistentListener(questButton.onClick, i, ui.Notify, "Quest details preview");
        controller.enemyImage = Find<Image>("IMG_Enemy");
        controller.battleBackground = battle.GetComponent<Image>();
        controller.playerHealthBar = Find<RectTransform>("BAR_Health");
        controller.normalSprite = SpriteNamed("SampleCharacter_Monster01_s");
        controller.bossSprite = SpriteNamed("SampleCharacter_Monster02_s");
        controller.timerLabel = TextNode(battle, "TXT_BossTimer", 24, 132, 492, 48, 18);
        controller.enemyLabel = TextNode(battle, "TXT_EnemyStats", 24, 386, 672, 24, 16);
        ImageNode(battle, "BG_EnemyHealth", 394, 377, 300, 6, new Color32(17,25,37,255));
        controller.enemyHealthBar = ImageNode(battle, "BAR_EnemyHealth", 394, 377, 300, 6, new Color32(238,131,106,255)).rectTransform;

        // Replace only the original boss placeholder; preserve unrelated custom listeners.
        var click = controller.bossButton.onClick;
        for (int i = click.GetPersistentEventCount() - 1; i >= 0; i--)
            if ((click.GetPersistentTarget(i) == ui && click.GetPersistentMethodName(i) == nameof(IdleGameUI.Notify)) ||
                click.GetPersistentTarget(i) == controller)
                UnityEventTools.RemovePersistentListener(click, i);
        UnityEventTools.AddPersistentListener(click, controller.ChallengeBoss);

        foreach (var button in ui.GetComponentsInChildren<Button>(true))
        {
            var logger = button.GetComponent<UIEventLogger>();
            if (logger == null) logger = button.gameObject.AddComponent<UIEventLogger>();
            logger.actionName = button.name.StartsWith("BTN_") ? button.name.Substring(4) : button.name;
            bool exists = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                exists |= button.onClick.GetPersistentTarget(i) == logger && button.onClick.GetPersistentMethodName(i) == nameof(UIEventLogger.LogEvent);
            if (!exists) UnityEventTools.AddPersistentListener(button.onClick, logger.LogEvent);
        }
        controller.BeginRun();
        EditorUtility.SetDirty(controller);
    }

    static Sprite SpriteNamed(string name)
    {
        string path = AssetDatabase.FindAssets(name + " t:Sprite").Select(AssetDatabase.GUIDToAssetPath)
            .First(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
    {
        var node = parent.Find(name) as RectTransform;
        if (node == null) { node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); node.SetParent(parent, false); }
        node.anchorMin = node.anchorMax = node.pivot = new Vector2(0,1);
        node.anchoredPosition = new Vector2(x,-y); node.sizeDelta = new Vector2(w,h);
        return node;
    }

    static Text TextNode(Transform parent, string name, float x, float y, float w, float h, int size)
    {
        var node = Node(parent,name,x,y,w,h);
        var text = node.GetComponent<Text>() ?? node.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
        return text;
    }
    static Image ImageNode(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var node = Node(parent,name,x,y,w,h);
        var image = node.GetComponent<Image>() ?? node.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception("STAGE TEST FAILED: " + message); }
    static void ClearTo(StageRun run, int target)
    {
        int limit = 50;
        while (run.Index != target && limit-- > 0) run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Index == target, "Could not reach stage " + target);
    }

    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var controller = Object.FindFirstObjectByType<StageBattleController>();
        Require(controller != null && controller.catalog != null, "Scene wiring");
        var catalog = controller.catalog;
        // This suite tests combat rules without travel/animation delays; the playable
        // suite separately exercises the real arena and its serialized actor pool.
        controller.arena = null;
        Require(catalog.stages.Length == 10 && catalog.bossTimeLimit == 45, "10 stages and 45 second bosses");
        var events = new List<string>();
        var run = new StageRun(catalog.stages, catalog.bossTimeLimit);
        run.EventOccurred += (action, stage) => events.Add(action + " " + stage.Id);
        run.Start();
        Require(run.Current.Id == "1-1" && !run.CanChallengeBoss, "Start and locked boss");
        float health = run.EnemyHealth;
        run.DamageEnemy(-1); run.DamageEnemy(float.NaN); run.DamageEnemy(float.PositiveInfinity);
        Require(run.EnemyHealth == health, "Invalid damage ignored");
        run.DamageEnemy(1);
        Require(run.Index == 0 && run.Kills == 0, "Partial damage cannot clear stage");
        run.Tick(100);
        Require(run.Index == 0, "No normal stage deadline");
        ClearTo(run,4);
        Require(run.Current.Id == "1-5" && run.BossSeconds == 45 && run.TotalKills == 12, "First boss entry");
        run.Tick(44);
        Require(run.Current.IsBoss && run.BossSeconds == 1, "Boss active before deadline");
        run.Tick(1);
        Require(run.Current.Id == "1-4" && run.Farming && run.CanChallengeBoss, "First boss timeout fallback");
        for (int i = 0; i < 30; i++) run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Current.Id == "1-4" && run.Farming, "Farm never automatically re-enters boss");
        Require(run.ChallengeBoss() && run.BossSeconds == 45 && run.EnemyHealth == run.Current.monsterHealth, "Fresh retry");
        run.Tick(44.9f); run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Current.Id == "2-1" && !run.Farming, "First chapter clear before deadline");
        ClearTo(run,9); run.Tick(90);
        Require(run.Current.Id == "2-4" && run.Farming, "Second boss timeout, large frame");
        run.ChallengeBoss(); run.PlayerDefeated();
        Require(run.Current.Id == "2-4" && run.Farming, "Boss player defeat");
        run.ChallengeBoss(); run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Completed && run.Current.Id == "2-4" && run.Farming, "Final clear safe loop");
        for (int i = 0; i < 12; i++) run.DamageEnemy(run.Current.monsterHealth);
        Require(run.Index == 8 && run.Completed, "No nonexistent stage after final clear");
        Require(events.Contains("BossTimeout 1-5") && events.Contains("BossTimeout 2-5") && events.Contains("SampleComplete 2-5"), "Lifecycle events");

        // Verify the actual serialized UnityEvent listeners, not just the handler methods.
        var logs = new List<string>();
        void Capture(string message, string stack, LogType type) { if (type == LogType.Log) logs.Add(message); }
        Application.logMessageReceived += Capture;
        try
        {
            controller.BeginRun();
            var buttons = controller.ui.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                var logger = button.GetComponent<UIEventLogger>();
                Require(logger != null, button.name + " logger");
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
                logs.Clear(); button.onClick.Invoke();
                Require(logs.Count(l => l == logger.actionName + " event action") == 1, button.name + " exactly one action log");
            }
            // AUTO affects attacks while the boss deadline keeps running.
            if (controller.ui.AutoEnabled) controller.ui.ToggleAuto();
            controller.BeginRun(); ClearTo(controller.Run,4);
            float enemyHp = controller.Run.EnemyHealth;
            controller.Simulate(1);
            Require(controller.Run.EnemyHealth == enemyHp && controller.Run.BossSeconds == 44, "AUTO off keeps timer, stops player attacks");
            controller.Simulate(44);
            Require(controller.Run.Current.Id == "1-4" && controller.Run.Farming, "Integrated timeout");
            Require(logs.Contains("BossTimeout 1-5 event action"), "Timeout is not substituted by player defeat");
            controller.ChallengeBoss(); controller.ui.ToggleAuto();
            controller.Simulate(1);
            Require(controller.Run.EnemyHealth < controller.Run.Current.monsterHealth, "AUTO deals actual damage");
            controller.Simulate(30);
            Require(controller.Run.Current.chapter == 2, "Integrated boss clear and next chapter");
            ClearTo(controller.Run,9); logs.Clear();
            controller.Simulate(45);
            Require(controller.Run.Current.Id == "2-4" && controller.Run.Farming && logs.Contains("BossTimeout 2-5 event action"), "Second boss survives to its exact deadline with sample stats");
            Debug.Log("STAGE_TESTS_PASSED: progression, scaling, both timeouts, farming, retry, final clear, AUTO combat, all " + buttons.Length + " button logs.");
        }
        finally { Application.logMessageReceived -= Capture; }
        // Discard test state and retain the saved initial scene.
        EditorSceneManager.OpenScene(ScenePath);
    }

    public static void ApplyAndVerify()
    {
        Apply();
        Verify();
        GameSceneBuilder.Verify();
    }
}
