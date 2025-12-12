using System.Collections.Generic;
using UnityEngine;

// �U������
public enum TrashType
{
    Banana,
    Can,
    Paper,
    // �i�X�R...
}

// ���l����
[System.Serializable]
public class TrashPoolEntry : BasePoolEntry<TrashType, BaseTrash> { }


// �U�������
public class TrashPool : BasePool<TrashType, BaseTrash>
{
    [SerializeField]
    // public List<BaseTrash> ActiveTrashList { get; private set; } = new List<BaseTrash>();
    public List<BaseTrash> ActiveTrashList = new List<BaseTrash>();
    public static TrashPool Instance { get; private set; }

    [SerializeField]
    private List<TrashPoolEntry> trashEntries;

    protected virtual void Awake()
    {
        // �]�m Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ��l�Ʀ��l
        InitializePool(trashEntries);
    }
    private void FixedUpdate()
    {
        // �C�V���غ���
        SpatialGridManager.Instance.UpdateGrid(ActiveTrashList);
    }

    public BaseTrash GetTrash(TrashType type, Vector3 position)
    {
        BaseTrash trash = Get(type);

        if (trash != null)
        {
            trash.transform.position = position;  // �K�[�o��ӳ]�w��m
            trash.gameObject.SetActive(true);
            ActiveTrashList.Add(trash); // <--- �s�W�G�[�J���D�M��
            return trash;
        }
        return trash;
    }

    public void ReturnTrash(BaseTrash trash)
    {
        if (trash == null) return;
        ActiveTrashList.Remove(trash); // <--- �s�W�G���X���D�M��
        Return(trash.trashType, trash);
    }
}
