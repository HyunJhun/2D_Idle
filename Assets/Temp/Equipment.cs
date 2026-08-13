using System.Text;
using UnityEngine;



public class Equipment : ScriptableObject
{
    public enum Type
    {
        Head,
        Body,
        Glove,
        Shoes,
        Weapon
    };

    public enum Rank
    {
        Common,
        Uncommon,
        Unique,
        Legendary
    }

    public Type yype;
    public Rank rank;
    public string equipName;
    public float physicsDamage;
    public float magicDamage;
    public float criticalProbablity;
    public float criticalDamage;



}
