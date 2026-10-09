using UnityEngine;

namespace IdlePrototype
{
    [CreateAssetMenu(menuName = "Idle/Character Sprites")]
    public sealed class CharacterSpriteSet : ScriptableObject
    {
        public string characterName;
        public Sprite[] idle, walk, attack, hurt, death;
        public float framesPerSecond = 12;
        public float visualScale = 12;
    }
}
