using UnityEngine;

namespace IdlePrototype
{
    [ExecuteAlways]
    public class IdleUILayout : MonoBehaviour
    {
        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (!parent || Screen.width == 0 || Screen.height == 0) return;
            var reference = (RectTransform)transform;
            reference.anchorMin = reference.anchorMax = new Vector2(.5f, .5f);
            reference.sizeDelta = new Vector2(720, 1280);
            var safe = Screen.safeArea;
            float scale = Mathf.Min(parent.rect.width * safe.width / Screen.width / 720f, parent.rect.height * safe.height / Screen.height / 1280f);
            transform.localScale = Vector3.one * scale;
            ((RectTransform)transform).anchoredPosition = new Vector2((safe.center.x / Screen.width - .5f) * parent.rect.width, (safe.center.y / Screen.height - .5f) * parent.rect.height);
        }
    }
}
