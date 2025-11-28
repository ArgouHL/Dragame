using UnityEngine;

public class DragLine : MonoBehaviour
{
    public LineRenderer line;

    private void Awake()
    {
        // 沒指定的話自動抓
        if (line == null)
            line = GetComponent<LineRenderer>();

        // 自動初始化 LineRenderer 設定
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.enabled = false;
    }

    public void ShowLine(Vector2 start, Vector2 end)
    {
        if (!line.enabled) line.enabled = true;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    public void HideLine()
    {
        line.enabled = false;
    }
}
