using System.Collections;
using UnityEngine;

public class HintCircle : MonoBehaviour
{
    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private Color startColor;
    [SerializeField] private Color endColor;
    [SerializeField] private float fadeTime ;
    public GameObject Perfab;
    // 2. 需要碰撞器來定義「圈內」的範圍
    [SerializeField] private CircleCollider2D circleCollider;
    [SerializeField] private LayerMask playerLayer;
    private IEnumerator FadeCircle()
    {
        float t = 0;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            circleRenderer.color = Color.Lerp(startColor, endColor, t / fadeTime);
            yield return null;
        }
        Instantiate(Perfab, transform.position, Quaternion.identity);
        Destroy(gameObject);
        
    }
    private void Start()
    {
     StartCoroutine(FadeCircle());
        
    }
    private void CheckForPlayer()
    {
        if (circleCollider == null) return; // 如果沒有碰撞器，就跳過

        // 6. 進行物理檢測：
        // 在「這個物件的位置」，用「碰撞器的半徑」，只針對「playerLayer」進行重疊圓形檢測
        Collider2D playerHit = Physics2D.OverlapCircle(transform.position, circleCollider.radius, playerLayer);

        // 7. 判斷是否打中玩家
        if (playerHit != null)
        {
            // 有打中東西 (且因為 LayerMask 的關係，我們確定這就是玩家)
            Debug.Log("玩家在圈內！");

            TestPlayerController.instance.ApplyStun();
        }
    }
    private void Update()
    {
        CheckForPlayer();
    }

}
