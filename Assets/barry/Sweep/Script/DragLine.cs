using UnityEngine;

public class DragLine : MonoBehaviour
{
    public LineRenderer line;

    [Header("寬度設定")]
    [SerializeField] private float minWidth = 0.05f;   // 最短拖曳時的線寬
    [SerializeField] private float maxWidth = 0.4f;    // 最長拖曳時的線寬
    [SerializeField] private float maxLength = 5f;     // 超過這個長度就不再變更寬度

    private void Awake()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.enabled = false;
    }

    public void ShowLine(Vector2 start, Vector2 end)
    {
        if (!line.enabled) line.enabled = true;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        // 依照長度計算線寬
        float length = Vector2.Distance(start, end);

        // 把長度換成 0~1 的比例
        float t = Mathf.Clamp01(length / maxLength);

        // 內插出實際寬度
        float width = Mathf.Lerp(minWidth, maxWidth, t);

        // 套用到 LineRenderer
        line.startWidth = width;
        line.endWidth = width;     // 如果頭尾要一樣粗就這樣
        // 如果想頭粗尾細，可以改成：
        // line.startWidth = width;
        // line.endWidth   = width * 0.4f;
    }

    public void HideLine()
    {
        line.enabled = false;
    }
}
