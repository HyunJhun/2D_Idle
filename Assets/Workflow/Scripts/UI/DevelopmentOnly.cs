using UnityEngine;

namespace IdlePrototype
{
    public sealed class DevelopmentOnly : MonoBehaviour
    {
        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            gameObject.SetActive(false);
#endif
        }
    }
}
