using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text trashCounterText; // 拖你的 UI 文字進來

    private void OnEnable()
    {
        TrashCounter.Changed += OnTrashCounterChanged;
    }

    private void OnDisable()
    {
        TrashCounter.Changed -= OnTrashCounterChanged;
    }

    private void Start()
    {
        Refresh(TrashCounter.Collected, TrashCounter.Total);
    }

    private void OnTrashCounterChanged(int collected, int total)
    {
        Refresh(collected, total);
    }

    private void Refresh(int collected, int total)
    {
        if (trashCounterText == null) return;
        trashCounterText.text = $"{collected}/{total}";
    }
}
