using UnityEngine;

public class DealerView : MonoBehaviour
{
    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) Debug.LogError("[DealerView] RectTransform not found on dealer prefab!");
    }

    public void SetDealerPosition(Vector2 anchorPoint)
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogError("[DealerView] RectTransform missing; cannot set position.");
            return;
        }

        // clamp to avoid anchors out of range
        anchorPoint.x = Mathf.Clamp01(anchorPoint.x);
        anchorPoint.y = Mathf.Clamp01(anchorPoint.y);

        // set anchor to a single point
        rt.anchorMin = anchorPoint;
        rt.anchorMax = anchorPoint;

        // ensure pivot is centered (or whaAever you want)
        rt.pivot = new Vector2(0.5f, 0.5f);

        // place the UI element exactly at that anchor
        rt.anchoredPosition = Vector2.zero;
    }
}
