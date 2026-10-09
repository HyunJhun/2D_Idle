using System;
using System.IO;
using System.Linq;
using IdlePrototype;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameSceneGrowthEditor
{
    [MenuItem("Tools/Idle UI/Apply growth buttons and Point sprites")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplyPointFiltering();
        EditorSceneManager.OpenScene(GameScenePlayableEditor.ScenePath);
        var ui=Object.FindFirstObjectByType<IdleGameUI>();
        Configure(ui);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        GameScenePlayableEditor.CapturePreview("Documentation/gameScene-growth-preview.png");
        Debug.Log("GROWTH_UI_APPLIED");
    }

    public static string[] SpritePaths()
    {
        return AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Workflow"}).Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.GetDependencies(GameScenePlayableEditor.ScenePath,true))
            .Where(p=>p.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".aseprite",StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
    }
    static void ApplyPointFiltering()
    {
        int changed=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var path in SpritePaths())
            {
                var importer=AssetImporter.GetAtPath(path);
                bool dirty=false;
                if(importer is TextureImporter texture)
                {
                    dirty=texture.filterMode!=FilterMode.Point || texture.mipmapEnabled || texture.textureCompression!=TextureImporterCompression.Uncompressed;
                    if(dirty) {texture.filterMode=FilterMode.Point;texture.mipmapEnabled=false;texture.textureCompression=TextureImporterCompression.Uncompressed;}
                }
                else if(importer!=null && path.EndsWith(".aseprite"))
                {
                    var serialized=new SerializedObject(importer);
                    var filter=serialized.FindProperty("m_TextureImporterSettings.m_FilterMode");
                    if(filter!=null && filter.intValue!=0) {filter.intValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();dirty=true;}
                }
                if(!dirty) continue;
                AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);changed++;
            }
        }
        finally {AssetDatabase.StopAssetEditing();}
        File.WriteAllText("Documentation/gameScene-point-import.txt",string.Join("\n",SpritePaths()));
        Debug.Log("POINT_IMPORTERS_UPDATED: "+changed);
    }

    public static void Configure(IdleGameUI ui)
    {
        ui.battle=Object.FindFirstObjectByType<StageBattleController>();
        string[] stats={"Attack","Health","Critical"}; int[] amounts={1,10,100};
        var nodes=ui.GetComponentsInChildren<Transform>(true);
        for(int stat=0;stat<3;stat++)
        {
            var row=ui.growthPanel.transform.Find("BG_"+stats[stat]);
            ui.statValues[stat].rectTransform.sizeDelta=new Vector2(258,28);ui.statValues[stat].fontSize=17;
            var level=row.Find("TXT_"+stats[stat]+"Level").GetComponent<Text>();level.fontSize=21;level.rectTransform.sizeDelta=new Vector2(258,30);
            var original=row.Find("BTN_Upgrade"+stats[stat]).gameObject;
            for(int option=0;option<3;option++)
            {
                string name="BTN_Upgrade"+stats[stat]+(option==0?"":amounts[option].ToString());
                var existing=row.Find(name);
                var go=existing ? existing.gameObject : Object.Instantiate(original,row);
                go.name=name;
                var button=go.GetComponent<HoldRepeatButton>();
                if(!button)
                {
                    var previous=go.GetComponent<Button>();
                    var click=previous.onClick;var colors=previous.colors;var navigation=previous.navigation;var graphic=previous.targetGraphic;
                    Object.DestroyImmediate(previous);button=go.AddComponent<HoldRepeatButton>();
                    button.onClick=click;button.colors=colors;button.navigation=navigation;button.targetGraphic=graphic;
                }
                var rect=(RectTransform)go.transform;rect.anchoredPosition=new Vector2(290+option*122,-10);rect.sizeDelta=new Vector2(116,68);
                var label=go.GetComponentInChildren<Text>();label.name="TXT_Upgrade"+stats[stat]+amounts[option];
                label.text="+"+amounts[option]+"\n"+IdleGameUI.UpgradeCost(stat,amounts[option]).ToString("N0")+" G";
                label.fontSize=17;label.rectTransform.anchoredPosition=new Vector2(2,0);label.rectTransform.sizeDelta=new Vector2(112,68);
                var action=go.GetComponent<GrowthUpgradeAction>() ?? go.AddComponent<GrowthUpgradeAction>();
                action.ui=ui;action.statIndex=stat;action.amount=amounts[option];
                var logger=go.GetComponent<UIEventLogger>() ?? go.AddComponent<UIEventLogger>();logger.actionName=name.Substring(4);
                for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)
                {
                    var target=button.onClick.GetPersistentTarget(i);
                    if(target==ui || target is GrowthUpgradeAction || target is UIEventLogger) UnityEventTools.RemovePersistentListener(button.onClick,i);
                }
                UnityEventTools.AddPersistentListener(button.onClick,logger.LogEvent);
                UnityEventTools.AddPersistentListener(button.onClick,action.Execute);
            }
        }
        var content=nodes.First(t=>t.name=="BG_Content");
        var power=content.Find("TXT_Power");if(power)power.gameObject.SetActive(false);
        var reset=content.Find("BTN_DebugResetStats");
        if(!reset)
        {
            var go=new GameObject("BTN_DebugResetStats",typeof(RectTransform),typeof(Image),typeof(Button),typeof(UIEventLogger),typeof(DevelopmentOnly));
            reset=go.transform;reset.SetParent(content,false);
            var rect=(RectTransform)reset;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(492,-18);rect.sizeDelta=new Vector2(204,40);
            var image=go.GetComponent<Image>();image.color=new Color32(99,63,72,255);
            var button=go.GetComponent<Button>();button.targetGraphic=image;
            var logger=go.GetComponent<UIEventLogger>();logger.actionName="DebugResetStats";
            UnityEventTools.AddPersistentListener(button.onClick,logger.LogEvent);UnityEventTools.AddPersistentListener(button.onClick,ui.ResetStats);
            var label=new GameObject("TXT_DebugResetStats",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(reset,false);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=17;label.text="DEV  /  RESET STATS";label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;
        }
        ui.RefreshStats();
    }
}
