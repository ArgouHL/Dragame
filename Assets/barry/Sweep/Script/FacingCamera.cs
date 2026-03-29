using System.Collections.Generic;
using UnityEngine;

public class FacingCamera : MonoBehaviour
{
    // [重點註釋] 儲存自身以外的所有深層子物件 Transform
    private Transform[] childs;

    private void Start()
    {
        // 獲取包含自身與所有層級 (子、孫等) 的 Transform
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        List<Transform> childList = new List<Transform>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            // [重點註釋] 嚴格排除自身，避免整個腳本所在的根節點被強制旋轉，導致物理層或移動方向錯亂
            if (allTransforms[i] != transform)
            {
                childList.Add(allTransforms[i]);
            }
        }
        childs = childList.ToArray();
    }

    private void Update()
    {
        // [重點註釋] 測試用版本：每幀讀取主相機旋轉值賦予所有深層子物件。
        // 已將 Camera.main 提取至迴圈外，避免在迴圈內反覆觸發尋找相機的底層高耗能操作。
        if (Camera.main != null)
        {
            Quaternion camRotation = Camera.main.transform.rotation;
            for (int i = 0; i < childs.Length; i++)
            {
                childs[i].rotation = camRotation;
            }
        }
    }
}