using System.Collections.Generic;
using UnityEngine;

namespace IdlePrototype
{
    public sealed class BattleArena : MonoBehaviour
    {
        public BattleActor hero;
        public BattleActor[] enemies;
        public CharacterSpriteSet heroSprites;
        public CharacterSpriteSet[] forestMonsters, ruinsMonsters;
        public CharacterSpriteSet forestBoss, ruinsBoss;
        public Camera battleCamera;
        public SpriteRenderer ground, path;
        public Sprite grassTile, stoneTile;
        public Transform effectsRoot;
        public Font damageFont;
        public Sprite effectSprite;
        public Material spriteMaterial;
        public BattleActor Target => pending == null && targetIndex >= 0 && targetIndex < enemies.Length && enemies[targetIndex].gameObject.activeSelf ? enemies[targetIndex] : null;
        public bool Ready => Target != null && !Target.Dead && !hero.Dead && Mathf.Abs(Target.transform.localPosition.x - hero.transform.localPosition.x) <= 2.85f;
        public int ActiveCount => activeCount;
        StageDefinition pending;
        StageDefinition displayedStage;
        int targetIndex, activeCount;
        float transition;
        readonly List<FloatingEffect> effects = new List<FloatingEffect>();
        sealed class FloatingEffect { public GameObject node; public float life; public Color color; public TextMesh text; public SpriteRenderer sprite; }

        public void Initialize()
        {
            // Importers persist Point filtering; also guard dynamically supplied frames.
            var profiles = new List<CharacterSpriteSet> { heroSprites, forestBoss, ruinsBoss };
            profiles.AddRange(forestMonsters); profiles.AddRange(ruinsMonsters);
            foreach (var profile in profiles)
                foreach (var frames in new[] { profile.idle, profile.walk, profile.attack, profile.hurt, profile.death })
                    foreach (var frame in frames) frame.texture.filterMode = FilterMode.Point;
            pending = displayedStage = null; transition = 0; targetIndex = 0;
            hero.ResetActor(heroSprites,false,false,new Vector3(-4,-1.25f,0));
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (Transform child in effectsRoot) Remove(child.gameObject);
            effects.Clear();
        }
        static void Remove(GameObject node) { if (Application.isPlaying) Destroy(node); else DestroyImmediate(node); }

        public void EnterStage(StageDefinition stage)
        {
            pending = stage; transition = displayedStage == null ? 0 : .8f; targetIndex = 0;
            if (transition == 0) BuildWave();
        }

        void BuildWave()
        {
            displayedStage = pending; pending = null;
            bool boss = displayedStage.IsBoss;
            activeCount = boss ? 1 : Mathf.Min(enemies.Length, displayedStage.killsToClear);
            hero.ResetActor(heroSprites,false,false,new Vector3(-3.5f,-1.25f,0));
            var list = displayedStage.chapter == 1 ? forestMonsters : ruinsMonsters;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].gameObject.SetActive(i < activeCount);
                if (i >= activeCount) continue;
                var definition = boss ? (displayedStage.chapter == 1 ? forestBoss : ruinsBoss) : list[(i + displayedStage.section - 1) % list.Length];
                enemies[i].name = (boss ? "BOSS_" : "MON_") + definition.characterName.Replace(" ", "") + "_" + (i+1).ToString("00");
                enemies[i].ResetActor(definition,true,boss,new Vector3(3.5f+i*3.1f,-1.25f + (i%2)*.12f,0));
            }
            ground.sprite = displayedStage.chapter == 1 ? grassTile : stoneTile;
            ground.color = displayedStage.chapter == 1 ? new Color(.44f,.60f,.42f) : new Color(.40f,.37f,.48f);
            path.color = displayedStage.chapter == 1 ? new Color(.64f,.65f,.48f) : new Color(.52f,.45f,.55f);
            battleCamera.backgroundColor = displayedStage.chapter == 1 ? new Color(.09f,.19f,.18f) : new Color(.15f,.12f,.21f);
        }

        public void SpawnCurrent(int kills)
        {
            if (pending == null && kills > 0 && kills % Mathf.Max(1,activeCount) == 0)
                EnterStage(displayedStage);
            targetIndex = kills % Mathf.Max(1,activeCount);
        }
        public void DefeatTarget() { if (Target != null) Target.Die(); }

        public void Advance(float dt)
        {
            if (pending != null)
            {
                transition -= dt;
                if (transition <= 0) BuildWave();
            }
            hero.Advance(dt,-2.2f,pending == null);
            for (int i=0;i<enemies.Length;i++)
                if (enemies[i].gameObject.activeSelf)
                    enemies[i].Advance(dt,.5f + Mathf.Max(0,i-targetIndex)*3.1f,pending == null && i >= targetIndex);
            for (int i=effects.Count-1;i>=0;i--)
            {
                var effect = effects[i]; effect.life -= dt;
                if (effect.life <= 0 || effect.node == null) { if (effect.node) Remove(effect.node); effects.RemoveAt(i); continue; }
                if (effect.text)
                {
                    effect.node.transform.localPosition += Vector3.up * dt * 1.3f;
                    effect.text.color = new Color(effect.color.r,effect.color.g,effect.color.b,Mathf.Min(1,effect.life*3));
                }
                if (effect.sprite) effect.sprite.color = new Color(effect.color.r,effect.color.g,effect.color.b,effect.life*3);
            }
        }

        public void PlayerStrike(float damage, bool critical, int skill = -1)
        {
            hero.Attack();
            if (Target == null) return;
            Target.Hit();
            var position = Target.transform.localPosition;
            DamageText(damage.ToString("N0")+(critical?"!":""), position+new Vector3(0,2.5f,0), critical ? Color.yellow : Color.white);
            if (skill >= 0)
            {
                var colors = new [] { Color.white, new Color(.6f,.6f,1), new Color(1,.4f,.15f), Color.cyan, new Color(.5f,1,.6f) };
                for (int i=0;i<3;i++)
                {
                    var node = new GameObject("FX_SkillSlash"); node.layer = gameObject.layer; node.transform.SetParent(effectsRoot,false);
                    node.transform.localPosition=position+new Vector3((i-1)*.4f,1.2f,0); node.transform.localScale=new Vector3(.12f,3.5f,1); node.transform.localRotation=Quaternion.Euler(0,0,-40+i*30);
                    var sprite=node.AddComponent<SpriteRenderer>(); sprite.sprite=effectSprite; sprite.sharedMaterial=spriteMaterial; sprite.sortingOrder=80; sprite.color=colors[skill];
                    effects.Add(new FloatingEffect { node=node,life=.3f,sprite=sprite,color=colors[skill] });
                }
            }
        }
        public void MonsterStrike(float damage)
        {
            if (Target != null) Target.Attack();
            hero.Hit(); DamageText("-"+damage.ToString("N0"),hero.transform.localPosition+new Vector3(0,2.6f,0),new Color(1,.5f,.45f));
        }
        void DamageText(string value,Vector3 position,Color color)
        {
            var node=new GameObject("TXT_DamageFloat"); node.layer=gameObject.layer; node.transform.SetParent(effectsRoot,false); node.transform.localPosition=position;
            var text=node.AddComponent<TextMesh>(); text.font=damageFont; text.fontSize=48; text.characterSize=.08f; text.anchor=TextAnchor.MiddleCenter; text.text=value; text.color=color;
            var renderer=text.GetComponent<MeshRenderer>(); renderer.sharedMaterial=damageFont.material; renderer.sortingOrder=90;
            effects.Add(new FloatingEffect {node=node,life=.9f,color=color,text=text});
        }
    }
}
