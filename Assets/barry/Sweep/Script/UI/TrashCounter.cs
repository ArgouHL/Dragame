using System;
using System.Collections.Generic;
using UnityEngine;

public static class TrashCounter
{
    public static int Total { get; private set; }      // y
    public static int Collected { get; private set; }  // x

    // 防止同一顆垃圾重複計數（同一關卡）
    private static readonly HashSet<int> _collectedIds = new HashSet<int>();

    // (x, y)
    public static event Action<int, int> Changed;

    public static void Reset()
    {
        Total = 0;
        Collected = 0;
        _collectedIds.Clear();
        Changed?.Invoke(Collected, Total);
    }

    public static void SetTotal(int total)
    {
        Total = Mathf.Max(0, total);
        Collected = Mathf.Clamp(Collected, 0, Total);
        Changed?.Invoke(Collected, Total);
    }

    public static void MarkCollected(BaseTrash trash)
    {
        if (trash == null) return;

        int id = trash.GetInstanceID();
        if (_collectedIds.Contains(id)) return;

        _collectedIds.Add(id);
        Collected = Mathf.Min(Collected + 1, Total);
        Changed?.Invoke(Collected, Total);
    }
}
