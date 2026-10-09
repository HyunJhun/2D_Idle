using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IdlePrototype;

public static class GameSceneBuilder
{
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var ui = Object.FindFirstObjectByType<IdleGameUI>();
        var referenceRect = (RectTransform)ui.transform;
        referenceRect.anchorMin = referenceRect.anchorMax = new Vector2(.5f,.5f);
        referenceRect.sizeDelta = new Vector2(720,1280);
        foreach (var nav in ui.navigation) nav.GetComponentInChildren<Text>().fontSize = 16;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        if (ui == null || ui.navigation.Length != 6) throw new System.Exception("Missing UI");
        // Runtime-only persistent listeners normally do not execute in an edit-mode batch check.
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                b.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        ui.navigation[1].onClick.Invoke();
        if (!ui.infoPanel.activeSelf || ui.growthPanel.activeSelf) throw new System.Exception("Navigation failed");
        ui.navigation[0].onClick.Invoke();
        GameObject.Find("BTN_UpgradeAttack").GetComponent<Button>().onClick.Invoke();
        if (ui.goldLabel.text != "123,800" || !ui.attackLabel.text.Contains("121")) throw new System.Exception("Upgrade failed");
        GameObject.Find("BTN_Auto").GetComponent<Button>().onClick.Invoke();
        if (ui.autoLabel.text != "AUTO OFF") throw new System.Exception("Auto failed");
        EditorSceneManager.OpenScene(ScenePath);
        var canvas = Object.FindFirstObjectByType<Canvas>();
        var camera = GameObject.Find("CAM_Main").GetComponent<Camera>();
        var arena = Object.FindFirstObjectByType<BattleArena>();
        if (arena != null) arena.battleCamera.Render();
        var rt = new RenderTexture(720,1280,24);
        camera.targetTexture = rt;
        camera.aspect = 720f / 1280f;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        canvas.scaleFactor = 1;
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var previewRect = (RectTransform)canvas.transform;
        previewRect.sizeDelta = new Vector2(720, 1280);
        previewRect.position = Vector3.zero; previewRect.rotation = Quaternion.identity; previewRect.localScale = Vector3.one;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 640;
        var layout = Object.FindFirstObjectByType<IdleUILayout>();
        layout.enabled = false; layout.transform.localScale = Vector3.one;
        ((RectTransform)layout.transform).anchoredPosition = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        var canvasRect = (RectTransform)canvas.transform;
        layout.transform.localScale = Vector3.one * Mathf.Min(canvasRect.rect.width / 720f, canvasRect.rect.height / 1280f);
        Canvas.ForceUpdateCanvases();
        layout.transform.position = Vector3.zero;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(720,1280,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,720,1280),0,0); image.Apply();
        File.WriteAllBytes("Documentation/gameScene-preview.png",image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; rt.Release();
        Debug.Log("GAME_SCENE_VERIFIED: navigation, upgrade, auto; preview rendered.");
    }
    const string ScenePath = "Assets/Workflow/Scenes/gameScene.unity";
    static Font font;
    static Color ink = Hex("111925"), panel = Hex("1D2938"), muted = Hex("9FAFC3"), gold = Hex("E7BD72"), teal = Hex("66D4BA");
    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    static Image Box(Transform p, string n, float x, float y, float w, float h, Color c)
    { var i = Rect(p, n, x, y, w, h).gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false; return i; }
    static Text Label(Transform p, string n, string value, float x, float y, float w, float h, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var t = Rect(p, n, x, y, w, h).gameObject.AddComponent<Text>(); t.font = font; t.text = value; t.fontSize = size; t.color = c;
        t.alignment = align; t.raycastTarget = false; return t;
    }
    static Button Button(Transform p, string n, string value, float x, float y, float w, float h, Color c)
    {
        var i = Box(p, "BTN_" + n, x, y, w, h, c); i.raycastTarget = true;
        var b = i.gameObject.AddComponent<Button>(); b.targetGraphic = i;
        Label(i.transform, "TXT_" + n, value, 4, 0, w - 8, h, 20, Color.white, TextAnchor.MiddleCenter); return b;
    }
    static void Art(Transform p, string name, string asset, float x, float y, float w, float h)
    {
        var path = AssetDatabase.FindAssets(asset + " t:Sprite").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(a => Path.GetFileNameWithoutExtension(a) == asset);
        if (path == null) return;
        var i = Box(p, "IMG_" + name, x, y, w, h, Color.white); i.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); i.preserveAspect = true;
    }
    [MenuItem("Tools/Idle UI/Create gameScene")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) { Debug.Log("gameScene already exists; preserving edits."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var camera = new GameObject("CAM_Main", typeof(Camera)).GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ink; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        var canvas = new GameObject("CAN_Game", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = .5f;
        new GameObject("SYS_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var root = Rect(canvas.transform, "PNL_Game", 0, 0, 720,1280);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f,.5f);
        root.gameObject.AddComponent<IdleUILayout>();
        var ui = root.gameObject.AddComponent<IdleGameUI>();
        Box(root,"BG_Base",0,0,720,1280,ink);
        var header = Box(root,"BG_Header",0,0,720,156,panel).transform;
        Label(header,"TXT_Level","LV. 128",24,18,130,30,24,gold);
        Label(header,"TXT_Player","WANDERER",24,51,200,32,27,Color.white);
        Label(header,"TXT_Rank","SILVER KNIGHT",24,90,210,24,16,muted);
        Art(header,"Gold","Image_Demo_Coin",257,22,32,32);
        ui.goldLabel = Label(header,"TXT_Gold","125,000",300,16,165,42,26,gold);
        Art(header,"Gem","Image_Demo_Gem01",257,75,32,32);
        Label(header,"TXT_Gem","3,250",300,69,160,42,26,Hex("A6BAFF"));
        UnityEventTools.AddStringPersistentListener(Button(header,"Menu","MENU",573,25,122,76,Hex("334258")).onClick,ui.Notify,"Menu preview");
        Box(header,"BG_Experience",24,137,672,4,Hex("344357")); Box(header,"BAR_Experience",24,137,420,4,teal);
        var battle = Box(root,"BG_Battle",0,156,720,496,Hex("253C42")).transform;
        Label(battle,"TXT_Chapter","CHAPTER 03  /  EMERALD WILDS",24,15,672,30,17,teal,TextAnchor.MiddleCenter);
        Label(battle,"TXT_Stage","3 - 24   Silent Forest",24,51,672,44,30,Color.white,TextAnchor.MiddleCenter);
        Label(battle,"TXT_Wave","WAVE  08 / 10",24,99,672,24,17,muted,TextAnchor.MiddleCenter);
        for(int j=0;j<14;j++) { float h = 70 + (j*43)%115; Box(battle,"BG_Ruins"+j,j*58-10,330-h,35,h,Hex("203239")); }
        Box(battle,"BG_Ground",0,330,720,166,Hex("213337"));
        Box(battle,"BG_Horizon",0,330,720,3,Hex("527466"));
        Art(battle,"Hero","SampleCharacter03",190,205,155,165);
        Art(battle,"Enemy","SampleCharacter_Monster01_s",445,240,120,125);
        Label(battle,"TXT_Damage","24,850!",390,192,220,55,38,gold,TextAnchor.MiddleCenter);
        Box(battle,"BG_Health",212,377,118,6,ink); Box(battle,"BAR_Health",212,377,96,6,teal);
        UnityEventTools.AddStringPersistentListener(Button(battle,"Boss","BOSS  >",534,132,162,48,Hex("875840")).onClick,ui.Notify,"Boss battle preview");
        UnityEventTools.AddStringPersistentListener(Button(battle,"OfflineReward","AFK\nREWARD",24,210,116,84,Hex("354D50")).onClick,ui.Notify,"Offline rewards preview");
        var quest = Box(battle,"BG_Quest",24,413,672,63,Hex("18282E")).transform;
        Label(quest,"TXT_Quest","QUEST  032     Defeat monsters  48 / 50",16,4,490,55,20,Color.white);
        UnityEventTools.AddStringPersistentListener(Button(quest,"Quest",">",574,10,80,43,Hex("45685D")).onClick,ui.Notify,"Defeat 2 more monsters (preview)");
        var skills = Box(root,"BG_Skills",0,652,720,112,Hex("141F2B")).transform;
        string[] names={"Slash","Storm","Flame","Frost","Rush"};
        for(int i=0;i<5;i++) { var b=Button(skills,"Skill"+names[i],names[i].ToUpper(),24+i*111,17,99,77,Hex(i==2?"63443D":"304356")); UnityEventTools.AddStringPersistentListener(b.onClick,ui.Notify,names[i]+" skill preview"); }
        var autoButton=Button(skills,"Auto","AUTO ON",584,17,112,77,Hex("376A60")); ui.autoLabel=autoButton.GetComponentInChildren<Text>(); UnityEventTools.AddPersistentListener(autoButton.onClick,ui.ToggleAuto);
        var content=Box(root,"BG_Content",0,764,720,404,panel).transform;
        ui.panelTitle=Label(content,"TXT_PanelTitle","GROWTH",24,16,300,44,29,gold);
        Label(content,"TXT_Power","POWER  1,284,500",348,21,348,35,22,Color.white,TextAnchor.MiddleRight);
        Box(content,"BG_Divider",24,73,672,2,Hex("3C4A5B"));
        ui.growthPanel=Rect(content,"PNL_Growth",24,91,672,294).gameObject;
        string[] stats={"Attack","Health","Critical"}; string[] levels={"ATK  /  Lv. 120","HP  /  Lv. 80","CRIT  /  Lv. 25"}; string[] values={"12,480  >  12,584","85,200  >  86,265","12.5%  >  13.0%"}; string[] costs={"1,200 G","950 G","2,400 G"};
        ui.statValues = new Text[3];
        for(int i=0;i<3;i++) {
            var row=Box(ui.growthPanel.transform,"BG_"+stats[i],0,i*99,672,88,Hex("263547")).transform;
            Box(row,"BG_Accent",0,0,4,88,i==0?gold:i==1?teal:Hex("AF9AE5"));
            var t=Label(row,"TXT_"+stats[i]+"Level",levels[i],19,10,370,30,23,Color.white);
            ui.statValues[i]=Label(row,"TXT_"+stats[i]+"Value",values[i],19,45,370,28,19,muted);
            if(i==0)ui.attackLabel=t; else if(i==1)ui.healthLabel=t; else ui.criticalLabel=t;
            UnityEventTools.AddIntPersistentListener(Button(row,"Upgrade"+stats[i],"UPGRADE\n"+costs[i],464,10,193,68,Hex("94703B")).onClick,ui.Upgrade,i);
        }
        ui.infoPanel=Rect(content,"PNL_Info",24,91,672,294).gameObject;
        ui.infoText=Label(ui.infoPanel.transform,"TXT_Info","",0,0,672,294,26,muted,TextAnchor.MiddleCenter); ui.infoPanel.SetActive(false);
        var nav=Box(root,"BG_Navigation",0,1168,720,112,Hex("101822")).transform;
        string[] tabs={"Growth","Equipment","Skills","Dungeon","Summon","Shop"}; string[] marks={"+","I","*","III","V","$"}; ui.navigation=new Button[6];
        for(int i=0;i<6;i++) {var b=Button(nav,tabs[i],marks[i]+"\n"+tabs[i].ToUpper(),8+i*119,9,109,91,i==0?Hex("A16F2D"):Hex("1E2736")); b.GetComponentInChildren<Text>().fontSize=16; ui.navigation[i]=b; UnityEventTools.AddIntPersistentListener(b.onClick,ui.SelectTab,i);}
        var toast=Box(root,"BG_Toast",80,580,560,58,Hex("476A5D")); ui.toastLabel=Label(toast.transform,"TXT_Toast","",12,0,536,58,22,Color.white,TextAnchor.MiddleCenter); toast.gameObject.SetActive(false);
        GameSceneStagesEditor.Configure(ui);
        GameSceneGrowthEditor.Configure(ui);
        EditorSceneManager.SaveScene(scene,ScenePath);
        Debug.Log("GAME_SCENE_CREATED: " + ScenePath);
    }
}
