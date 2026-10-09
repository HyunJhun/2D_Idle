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

public static class GameScenePlayableEditor
{
    public const string ScenePath="Assets/Workflow/Scenes/gameScene.unity";
    const string VisualPath="Assets/Workflow/Data/Characters/";
    const string Pack1="Assets/Workflow/Sprites/Tiny RPG Character Asset Pack 01 v2.0 -Full 22 Characters/Characters(100x100 split)/";
    const string Pack2="Assets/Workflow/Sprites/Tiny RPG Character Asset Pack 02 -Full 20 Characters/Characters(100x100 split)/";
    static int layer;
    static Material material;
    static Sprite white;

    [MenuItem("Tools/Idle UI/Build playable waves")]
    public static void Build()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        var battle=Object.FindFirstObjectByType<StageBattleController>();
        var old=Object.FindFirstObjectByType<BattleArena>();
        if(old != null) { Debug.Log("Playable arena already exists; keeping scene edits."); return; }
        layer=LayerMask.NameToLayer("BattleActors");
        if(layer<0)
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            for(int i=8;i<32;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) {layer=i;layers.GetArrayElementAtIndex(i).stringValue="BattleActors";break;}
            if(layer<0) throw new Exception("No free BattleActors layer.");
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Workflow/Data/BattleSprite.mat");
        if(!material)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material,"Assets/Workflow/Data/BattleSprite.mat");
        }
        const string whitePath="Assets/Workflow/Data/BattlePixel.png";
        if(!File.Exists(whitePath))
        {
            var texture=new Texture2D(2,2); texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); texture.Apply();
            File.WriteAllBytes(whitePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(whitePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(whitePath); importer.textureType=TextureImporterType.Sprite;
            importer.spritePixelsPerUnit=2; importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        white=AssetDatabase.LoadAssetAtPath<Sprite>(whitePath);
        var hero=Profile("Swordsman",false);
        var forest=new[]{Profile("Slime",false),Profile("Skeleton",false),Profile("Orc",false)};
        var ruins=new[]{Profile("Demon_A",true),Profile("Hellhound",true),Profile("Armored Skeleton",false)};
        var boss1=Profile("Elite Orc",false); var boss2=Profile("Minotaur",true);
        var arena=Node("GRP_BattleArena",null).AddComponent<BattleArena>();
        battle.arena=arena; arena.heroSprites=hero; arena.forestMonsters=forest; arena.ruinsMonsters=ruins; arena.forestBoss=boss1; arena.ruinsBoss=boss2;
        arena.damageFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); arena.effectSprite=white; arena.spriteMaterial=material;
        var groundGroup=Node("GRP_Environment",arena.transform).transform;
        var tiles=AssetDatabase.LoadAllAssetsAtPath("Assets/Workflow/Sprites/Backgrounds/Tileset/Tiles.png").OfType<Sprite>().ToArray();
        arena.grassTile=tiles.First(s=>s.name=="Gress 4"); arena.stoneTile=tiles.First(s=>s.name=="Soil 4");
        arena.ground=Tile("BG_Terrain",groundGroup,arena.grassTile,new Vector2(0,0),new Vector2(24,8),-20);
        arena.path=Tile("BG_Path",groundGroup,arena.stoneTile,new Vector2(0,-1.3f),new Vector2(24,1.5f),-10);
        for(int i=0;i<12;i++)
        {
            var patch=Tile("BG_GrassPatch"+i,groundGroup,arena.grassTile,new Vector2(-10+i*1.8f,i%2==0?1.4f:-2.45f),new Vector2(.65f,.35f),-5);
            patch.color=new Color(.25f,.40f,.27f);
        }
        var playerTemplate=ActorTemplate(hero,false);
        PrefabUtility.SaveAsPrefabAsset(playerTemplate.gameObject,"Assets/Workflow/Prefabs/Battle/Player.prefab");
        Object.DestroyImmediate(playerTemplate.gameObject);
        var enemyTemplate=ActorTemplate(forest[1],true);
        PrefabUtility.SaveAsPrefabAsset(enemyTemplate.gameObject,"Assets/Workflow/Prefabs/Battle/Monster.prefab");
        Object.DestroyImmediate(enemyTemplate.gameObject);
        arena.hero=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Workflow/Prefabs/Battle/Player.prefab"),arena.transform)).GetComponent<BattleActor>();
        arena.hero.name="CHR_Player";
        var enemyRoot=Node("GRP_MonsterWave",arena.transform).transform;
        arena.enemies=new BattleActor[3];
        for(int i=0;i<3;i++) arena.enemies[i]=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Workflow/Prefabs/Battle/Monster.prefab"),enemyRoot)).GetComponent<BattleActor>();
        arena.effectsRoot=Node("GRP_CombatEffects",arena.transform).transform;
        arena.battleCamera=Node("CAM_Battle",arena.transform).AddComponent<Camera>();
        arena.battleCamera.transform.localPosition=new Vector3(0,0,-10); arena.battleCamera.orthographic=true;
        arena.battleCamera.orthographicSize=2.8f; arena.battleCamera.clearFlags=CameraClearFlags.SolidColor; arena.battleCamera.cullingMask=1<<layer;
        arena.battleCamera.depth=-10;
        var renderTexture=new RenderTexture(720,190,24,RenderTextureFormat.ARGB32) {name="BattleViewport",filterMode=FilterMode.Point,antiAliasing=1};
        AssetDatabase.CreateAsset(renderTexture,"Assets/Workflow/Data/BattleViewport.renderTexture");
        arena.battleCamera.targetTexture=renderTexture;
        var mainCamera=GameObject.Find("CAM_Main").GetComponent<Camera>(); mainCamera.cullingMask &= ~(1<<layer);
        var panel=battle.battleBackground.transform;
        foreach(Transform child in panel)
            if(child.name=="IMG_Hero"||child.name=="IMG_Enemy"||child.name=="BG_Ground"||child.name=="BG_Horizon"||child.name.StartsWith("BG_Ruins")) child.gameObject.SetActive(false);
        var viewport=new GameObject("IMG_BattleViewport",typeof(RectTransform),typeof(RawImage),typeof(BattleTapAttack));
        var rect=viewport.GetComponent<RectTransform>(); rect.SetParent(panel,false); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(0,-180); rect.sizeDelta=new Vector2(720,190); rect.SetSiblingIndex(1);
        viewport.GetComponent<RawImage>().texture=renderTexture; viewport.GetComponent<BattleTapAttack>().battle=battle;
        // Give the battle viewport the full lane; the AFK button sits beside the timer.
        var afk=panel.Find("BTN_OfflineReward") as RectTransform; afk.anchoredPosition=new Vector2(390,-132); afk.sizeDelta=new Vector2(130,48);
        var afkText=afk.GetComponentInChildren<Text>(); afkText.text="AFK REWARD"; afkText.fontSize=15; afkText.rectTransform.sizeDelta=new Vector2(122,48);
        battle.timerLabel.rectTransform.sizeDelta=new Vector2(360,48); battle.timerLabel.fontSize=15;
        battle.damageLabel.gameObject.SetActive(false); // Damage now follows world-space actors.
        battle.skillButtons=new Button[5]; battle.skillLabels=new Text[5];
        string[] names={"Slash","Storm","Flame","Frost","Rush"};
        for(int i=0;i<5;i++)
        {
            var button=battle.ui.GetComponentsInChildren<Button>(true).First(b=>b.name=="BTN_Skill"+names[i]);
            for(int n=button.onClick.GetPersistentEventCount()-1;n>=0;n--)
                if(button.onClick.GetPersistentTarget(n)==battle.ui) UnityEventTools.RemovePersistentListener(button.onClick,n);
            UnityEventTools.AddIntPersistentListener(button.onClick,battle.UseSkill,i);
            battle.skillButtons[i]=button; battle.skillLabels[i]=button.GetComponentInChildren<Text>(); battle.skillLabels[i].fontSize=17;
        }
        // A short playable demo: basic attacks alone reach both chapters; skills/upgrades beat the last boss.
        float[] hp={24000,32000,40000,50000,180000,200000,230000,265000,300000,850000};
        for(int i=0;i<10;i++) battle.catalog.stages[i].monsterHealth=hp[i];
        EditorUtility.SetDirty(battle.catalog);
        battle.BeginRun();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        CapturePreview("Documentation/gameScene-playable-forest.png");
        Debug.Log("PLAYABLE_WAVES_BUILT");
    }

    static GameObject Node(string name,Transform parent)
    { var go=new GameObject(name); go.layer=layer; go.transform.SetParent(parent,false); return go; }
    static SpriteRenderer Tile(string name,Transform parent,Sprite sprite,Vector2 position,Vector2 size,int order)
    {
        var renderer=Node(name,parent).AddComponent<SpriteRenderer>(); renderer.sprite=sprite; renderer.sharedMaterial=material;
        renderer.drawMode=SpriteDrawMode.Tiled; renderer.size=size; renderer.sortingOrder=order; renderer.transform.localPosition=position; return renderer;
    }
    static BattleActor ActorTemplate(CharacterSpriteSet definition,bool enemy)
    {
        var actor=Node(enemy?"MON_Monster":"CHR_Player",null).AddComponent<BattleActor>();
        actor.body=Node("SPR_Body",actor.transform).AddComponent<SpriteRenderer>(); actor.body.sharedMaterial=material; actor.body.sortingOrder=20;
        var shadow=Node("BG_Shadow",actor.transform).AddComponent<SpriteRenderer>(); shadow.sprite=white; shadow.sharedMaterial=material; shadow.color=new Color(0,0,0,.25f); shadow.sortingOrder=5; shadow.transform.localScale=new Vector3(1.2f,.15f,1);
        var health=Node("BG_ActorHealth",actor.transform); health.transform.localScale=new Vector3(1.7f,.10f,1);
        var back=health.AddComponent<SpriteRenderer>(); back.sprite=white;back.sharedMaterial=material;back.color=new Color(.07f,.09f,.12f);back.sortingOrder=45;
        actor.healthFill=Node("BAR_ActorHealth",health.transform).transform; actor.healthFill.localPosition=new Vector3(-.5f,0,0);
        var fill=Node("SPR_HealthFill",actor.healthFill).AddComponent<SpriteRenderer>(); fill.sprite=white;fill.sharedMaterial=material;fill.color=enemy?new Color(1,.42f,.32f):new Color(.3f,.9f,.65f);fill.sortingOrder=46;fill.transform.localPosition=new Vector3(.5f,0,0);fill.transform.localScale=new Vector3(1,.65f,1);
        actor.ResetActor(definition,enemy,false,Vector3.zero); return actor;
    }

    static CharacterSpriteSet Profile(string name,bool secondPack)
    {
        var path=VisualPath+name.Replace(" ","")+".asset";
        var result=AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path);
        if(result) return result;
        result=ScriptableObject.CreateInstance<CharacterSpriteSet>(); result.characterName=name;
        AssetDatabase.CreateAsset(result,path);
        string directory=(secondPack?Pack2:Pack1)+name+"/"+name+"/";
        result.idle=Frames(result,directory+name+"_Idle.png");
        result.walk=Frames(result,directory+name+"_Walk.png");
        result.attack=Frames(result,directory+name+"_Attack01.png");
        result.hurt=Frames(result,directory+name+"_Hurt.png");
        result.death=Frames(result,directory+name+"_Death.png");
        EditorUtility.SetDirty(result); return result;
    }
    static Sprite[] Frames(CharacterSpriteSet owner,string path)
    {
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(!texture) throw new Exception("Missing Workflow sprite sheet: "+path);
        var imported=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Where(s=>s.rect.width==100 && s.rect.height==100 && Mathf.Approximately(s.pixelsPerUnit,100)).OrderBy(s=>s.rect.x).ToArray();
        if(imported.Length==texture.width/100) return imported;
        var frames=new Sprite[texture.width/100];
        for(int i=0;i<frames.Length;i++)
        {
            frames[i]=Sprite.Create(texture,new Rect(i*100,0,100,100),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            frames[i].name=Path.GetFileNameWithoutExtension(path)+"_"+i;
            AssetDatabase.AddObjectToAsset(frames[i],owner);
        }
        return frames;
    }

    public static void Polish()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var arena=Object.FindFirstObjectByType<BattleArena>();
        var tiles=AssetDatabase.LoadAllAssetsAtPath("Assets/Workflow/Sprites/Backgrounds/Tileset/Tiles.png").OfType<Sprite>().ToArray();
        arena.grassTile=IsolatedTile(tiles.First(s=>s.name=="Gress 4"),"BattleGrass");
        arena.stoneTile=IsolatedTile(tiles.First(s=>s.name=="Soil 4"),"BattleStone");
        arena.path.sprite=arena.stoneTile;
        foreach(var patch in arena.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("BG_GrassPatch")).ToArray()) Object.DestroyImmediate(patch.gameObject);
        var battle=Object.FindFirstObjectByType<StageBattleController>();battle.BeginRun();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        CapturePreview("Documentation/gameScene-playable-forest.png");
    }

    static Sprite IsolatedTile(Sprite source,string name)
    {
        // Extract the existing 16px tile into its own repeating texture so the sheet's
        // transparent gutters cannot bleed into the tiled SpriteRenderer at camera edges.
        string path="Assets/Workflow/Data/"+name+".png";
        if(!File.Exists(path))
        {
            var previous=RenderTexture.active;
            var temporary=RenderTexture.GetTemporary(source.texture.width,source.texture.height,0,RenderTextureFormat.ARGB32);
            var copy=new Texture2D((int)source.rect.width,(int)source.rect.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(source.texture,temporary);RenderTexture.active=temporary;
                copy.ReadPixels(source.rect,0,0);copy.Apply();File.WriteAllBytes(path,copy.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);Object.DestroyImmediate(copy);}
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spritePixelsPerUnit=16;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void CapturePreview(string path)
    {
        var arena=Object.FindFirstObjectByType<BattleArena>(); if(arena) arena.battleCamera.Render();
        var canvas=Object.FindFirstObjectByType<Canvas>(); var camera=GameObject.Find("CAM_Main").GetComponent<Camera>();
        var scaler=canvas.GetComponent<CanvasScaler>(); var layout=Object.FindFirstObjectByType<IdleUILayout>();
        var canvasRect=(RectTransform)canvas.transform; var root=(RectTransform)layout.transform;
        var mode=canvas.renderMode; var worldCamera=canvas.worldCamera; var scale=canvas.scaleFactor;
        var canvasSize=canvasRect.sizeDelta; var canvasPosition=canvasRect.position; var canvasScale=canvasRect.localScale;
        var rootPosition=root.position; var rootScale=root.localScale; bool layoutEnabled=layout.enabled,scalerEnabled=scaler.enabled;
        var oldTarget=camera.targetTexture; var oldPosition=camera.transform.position; float oldSize=camera.orthographicSize,oldAspect=camera.aspect;
        var previous=RenderTexture.active; var rt=new RenderTexture(720,1280,24); var image=new Texture2D(720,1280,TextureFormat.RGB24,false);
        try
        {
            layout.enabled=false;scaler.enabled=false;canvas.scaleFactor=1;canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            canvasRect.sizeDelta=new Vector2(720,1280);canvasRect.position=Vector3.zero;canvasRect.localScale=Vector3.one;
            root.localScale=Vector3.one;root.position=Vector3.zero;
            camera.targetTexture=rt;camera.aspect=720f/1280;camera.orthographicSize=640;camera.transform.position=new Vector3(0,0,-10);
            foreach(var text in canvas.GetComponentsInChildren<Text>(true))text.SetAllDirty();
            Canvas.ForceUpdateCanvases();root.position=Vector3.zero;camera.Render();
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,1280),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.transform.position=oldPosition;camera.orthographicSize=oldSize;camera.aspect=oldAspect;
            canvas.renderMode=mode;canvas.worldCamera=worldCamera;canvas.scaleFactor=scale;canvasRect.sizeDelta=canvasSize;canvasRect.position=canvasPosition;canvasRect.localScale=canvasScale;
            root.position=rootPosition;root.localScale=rootScale;layout.enabled=layoutEnabled;scaler.enabled=scalerEnabled;
            rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        }
    }
}
