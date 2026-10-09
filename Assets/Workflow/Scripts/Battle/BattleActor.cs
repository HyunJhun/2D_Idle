using UnityEngine;

namespace IdlePrototype
{
    public sealed class BattleActor : MonoBehaviour
    {
        public SpriteRenderer body;
        public Transform healthFill;
        public CharacterSpriteSet sprites;
        public bool Dead { get; private set; }
        public string AnimationState { get; private set; } = "Idle";
        public float Health { get; private set; }
        float animationClock, hold, flash;
        Sprite[] clip;

        public void ResetActor(CharacterSpriteSet definition, bool enemy, bool boss, Vector3 position)
        {
            sprites = definition; Dead = false; hold = flash = 0;
            gameObject.SetActive(true); transform.localPosition = position;
            float scale = sprites.visualScale * (boss ? 1.45f : 1);
            body.transform.localScale = Vector3.one * scale;
            // All source frames are 100x100 with centered pivots; feet sit at about y=43.
            body.transform.localPosition = new Vector3(0, scale * .07f, 0);
            body.flipX = enemy; body.color = Color.white;
            healthFill.parent.localPosition = new Vector3(0, boss ? 3.35f : 2.65f, 0);
            SetHealth(1,1); SetClip("Idle", sprites.idle, true);
        }

        void SetClip(string state, Sprite[] frames, bool force = false)
        {
            if (!force && AnimationState == state) return;
            AnimationState = state; clip = frames; animationClock = 0;
            if (clip != null && clip.Length > 0) body.sprite = clip[0];
        }

        public void Advance(float dt, float destinationX, bool canMove)
        {
            hold = Mathf.Max(0, hold - dt); flash = Mathf.Max(0, flash - dt);
            if (!Dead && hold <= 0)
            {
                bool walking = canMove && Mathf.Abs(transform.localPosition.x - destinationX) > .02f;
                if (walking)
                {
                    var p = transform.localPosition;
                    p.x = Mathf.MoveTowards(p.x, destinationX, 4.5f * dt); transform.localPosition = p;
                }
                SetClip(walking ? "Walk" : "Idle", walking ? sprites.walk : sprites.idle);
            }
            animationClock += dt;
            if (clip != null && clip.Length > 0)
            {
                int frame = Mathf.FloorToInt(animationClock * sprites.framesPerSecond);
                body.sprite = clip[Dead ? Mathf.Min(frame,clip.Length-1) : frame % clip.Length];
            }
            float alpha = Dead ? Mathf.Clamp01(1 - Mathf.Max(0, animationClock - .35f) * 2.5f) : 1;
            body.color = flash > 0 ? new Color(1,.45f,.4f,alpha) : new Color(1,1,1,alpha);
            if (Dead && animationClock > .8f) gameObject.SetActive(false);
        }

        public void Attack()
        {
            if (Dead) return;
            SetClip("Attack", sprites.attack, true);
            hold = Mathf.Min(.65f, sprites.attack.Length / sprites.framesPerSecond);
        }
        public void Hit() { if (Dead) return; flash = .15f; }
        public void Die() { Dead = true; hold = 1; SetHealth(0,1); SetClip("Death", sprites.death, true); }
        public void SetHealth(float value, float maximum)
        {
            Health = value;
            healthFill.localScale = new Vector3(Mathf.Clamp01(value / Mathf.Max(1,maximum)), 1, 1);
        }
    }
}
