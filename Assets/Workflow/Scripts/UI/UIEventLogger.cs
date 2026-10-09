using UnityEngine;

namespace IdlePrototype
{
    public sealed class UIEventLogger : MonoBehaviour
    {
        public string actionName;
        public void LogEvent() => Debug.Log(actionName + " event action", this);
    }
}
