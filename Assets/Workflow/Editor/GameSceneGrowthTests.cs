using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GameSceneGrowthTests
{
    const string Key="Idle.GrowthVerification";
    static IEnumerator routine;
    static double deadline;
    static string runtimeError;
    static GameSceneGrowthTests()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+50;
            Application.logMessageReceived+=Log;
            routine=CheckInput();EditorApplication.update+=Poll;
        };
    }
    static void Require(bool value,string message) {if(!value)throw new Exception("GROWTH TEST FAILED: "+message);}
    public static void Run()
    {
        int checkedCount=0;
        foreach(var path in GameSceneGrowthEditor.SpritePaths())
        {
            var importer=AssetImporter.GetAtPath(path);
            if(importer is TextureImporter texture)
                Require(texture.filterMode==FilterMode.Point && !texture.mipmapEnabled && texture.textureCompression==TextureImporterCompression.Uncompressed,"Pixel importer "+path);
            else if(importer!=null && path.EndsWith(".aseprite"))
            {
                var settings=new SerializedObject(importer);var filter=settings.FindProperty("m_TextureImporterSettings.m_FilterMode");
                Require(filter!=null && filter.intValue==0,"Aseprite Point filter "+path);
            }
            checkedCount++;
        }
        Debug.Log("POINT_SETTINGS_VERIFIED: "+checkedCount);
        EditorSceneManager.OpenScene(GameScenePlayableEditor.ScenePath);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeError=message;}
    static void Poll()
    {
        try
        {
            if(runtimeError!=null)throw new Exception(runtimeError);
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Growth input tests timed out");
            if(!routine.MoveNext())Finish(0,"GROWTH_PLAY_MODE_PASSED: 9 quantity buttons and costs, atomic failure, stat reset/vitals, pointer-down single action, 2-second delay, 0.25-second unscaled repeats, release/click deduplication, exit/drag/focus/pause/disable cancellation, touch identity, keyboard submit.");
        }
        catch(Exception error){Finish(1,error.ToString());}
    }
    static PointerEventData Pointer(int id=-1,PointerEventData.InputButton button=PointerEventData.InputButton.Left)
    {return new PointerEventData(EventSystem.current){pointerId=id,button=button,eligibleForClick=true};}
    static void Down(HoldRepeatButton button,PointerEventData data)
    {
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
    }
    static void Up(HoldRepeatButton button,PointerEventData data)
    {
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerable Wait(double seconds)
    {double end=Time.unscaledTimeAsDouble+seconds;while(Time.unscaledTimeAsDouble<end)yield return null;}

    static IEnumerator CheckInput()
    {
        var ui=Object.FindFirstObjectByType<IdleGameUI>();
        while(ui.battle.Run==null)yield return null;
        Time.timeScale=0; // Held UI input must continue even when gameplay is paused.
        ui.AddGold(2000000);
        var buttons=ui.GetComponentsInChildren<HoldRepeatButton>(true);
        Require(buttons.Length==9,"Nine growth buttons");
        foreach(var button in buttons)
        {
            Require(button.HoldDelay==2 && button.RepeatInterval==.25f,"Exact configured input timing");
            ui.ResetStats();var action=button.GetComponent<GrowthUpgradeAction>();int before=ui.Gold;
            var pointer=Pointer();Down(button,pointer);
            int level=action.statIndex==0?ui.AttackLevel:action.statIndex==1?ui.HealthLevel:ui.CriticalLevel;
            int initial=action.statIndex==0?IdleGameUI.InitialAttack:action.statIndex==1?IdleGameUI.InitialHealth:IdleGameUI.InitialCritical;
            Require(level==initial+action.amount && ui.Gold==before-IdleGameUI.UpgradeCost(action.statIndex,action.amount),button.name+" press applies amount and exact price");
            Up(button,pointer);
            Require(ui.Gold==before-IdleGameUI.UpgradeCost(action.statIndex,action.amount),button.name+" release cannot double spend");
        }
        var attack=buttons.First(b=>b.name=="BTN_UpgradeAttack");
        var times=new List<double>();UnityEngine.Events.UnityAction record=()=>times.Add(Time.unscaledTimeAsDouble);
        attack.onClick.AddListener(record);
        ui.ResetStats();times.Clear();var held=Pointer();double began=Time.unscaledTimeAsDouble;Down(attack,held);
        Require(times.Count==1,"Immediate first press");
        foreach(var wait in Wait(1.9))yield return wait;
        Require(times.Count==1,"No repeats before 2 seconds");
        while(Time.unscaledTimeAsDouble<began+2.65)yield return null;
        Require(times.Count==4,"Initial action plus repeats at 2.00, 2.25, 2.50 seconds");
        Require(times[1]-began>=2 && times[1]-began<2.15,"First repeat timing");
        for(int i=2;i<times.Count;i++)Require(Math.Abs(times[i]-times[i-1]-.25)<.12,"Quarter-second cadence");
        Up(attack,held);int count=times.Count;
        foreach(var wait in Wait(.4))yield return wait;
        Require(times.Count==count && !attack.IsHolding,"Release stops repeated actions and click is suppressed");

        // Pointer identity: a second finger cannot release or duplicate the first press.
        var touch=Pointer(42);Down(attack,touch);count=times.Count;
        Down(attack,Pointer(43));Up(attack,Pointer(43));
        Require(attack.IsHolding && times.Count==count,"Second touch ignored");Up(attack,touch);
        Down(attack,Pointer(-2,PointerEventData.InputButton.Right));Require(!attack.IsHolding && times.Count==count,"Right click ignored");
        Down(attack,held);ExecuteEvents.Execute(attack.gameObject,held,ExecuteEvents.pointerExitHandler);Require(!attack.IsHolding,"Pointer exit stops hold");
        Down(attack,held);ExecuteEvents.Execute(attack.gameObject,held,ExecuteEvents.beginDragHandler);Require(!attack.IsHolding,"Dragging stops hold");
        Down(attack,held);attack.SendMessage("OnApplicationFocus",false);Require(!attack.IsHolding,"Focus loss stops hold");
        Down(attack,held);attack.SendMessage("OnApplicationPause",true);Require(!attack.IsHolding,"Application pause stops hold");
        Down(attack,held);ui.growthPanel.SetActive(false);Require(!attack.IsHolding,"Closing growth panel stops hold");ui.growthPanel.SetActive(true);
        Down(attack,held);attack.interactable=false;yield return null;yield return null;Require(!attack.IsHolding,"Non-interactable button stops hold");attack.interactable=true;
        count=times.Count;ExecuteEvents.Execute(attack.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);Require(times.Count==count+1,"Keyboard submit retains one action");

        var health=buttons.First(b=>b.name=="BTN_UpgradeHealth10");Down(attack,held);Down(health,Pointer(54));
        int gold=ui.Gold;string stage=ui.battle.Run.Current.Id;
        GameObject.Find("BTN_DebugResetStats").GetComponent<Button>().onClick.Invoke();
        Require(ui.AttackLevel==120 && ui.HealthLevel==80 && ui.CriticalLevel==25,"Reset all initial stat levels");
        Require(ui.battle.PlayerHealth==ui.MaxHealth && ui.Gold==gold && ui.battle.Run.Current.Id==stage,"Reset restores vitals and preserves currency/stage");
        Require(!attack.IsHolding && !health.IsHolding,"Reset cancels every ongoing hold");
        count=times.Count;foreach(var wait in Wait(.4))yield return wait;Require(times.Count==count,"Reset stays reset after input cancellation");

        ui.UpgradeBy(2,100);gold=ui.Gold;ui.UpgradeBy(2,100);
        Require(ui.CriticalLevel==125 && ui.Gold==gold,"Critical cap rejection is atomic");
        while(ui.Gold>=120000)ui.UpgradeBy(0,100);
        gold=ui.Gold;int attackLevel=ui.AttackLevel;ui.UpgradeBy(0,100);
        Require(ui.Gold==gold && ui.AttackLevel==attackLevel,"Insufficient funds never buy a partial batch");
        ui.UpgradeBy(0,-1);ui.UpgradeBy(0,999);Require(ui.Gold==gold && ui.AttackLevel==attackLevel,"Invalid batch rejected");
        attack.onClick.RemoveListener(record);
        ui.ResetStats();ui.AddGold(125000-ui.Gold);
        GameScenePlayableEditor.CapturePreview("Documentation/gameScene-growth-preview.png");
    }
    static void Finish(int code,string text)
    {
        EditorApplication.update-=Poll;Application.logMessageReceived-=Log;Time.timeScale=1;SessionState.EraseBool(Key);
        File.WriteAllText("Documentation/gameScene-growth-test-results.txt",text);
        if(code==0)Debug.Log(text);else Debug.LogError(text);EditorApplication.Exit(code);
    }
}
