using UnityEngine;

[CreateAssetMenu(fileName = "Type", menuName = "Scriptable Objects/Type")]
public class itemData : ScriptableObject
{
    public string typeName; // 類型名稱
    public GameObject prefab; // 對應的預製體
}
