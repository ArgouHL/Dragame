using UnityEngine;
using  System.Collections;
public class BaseTrash : BasePoolItem
{
    [SerializeField] protected float absorbEffectDuration;
    [SerializeField] protected float rotationSpeed;

    [Header("縮小(消退)速率曲線")]
    [SerializeField] protected AnimationCurve scaleCurve;

    [Header("移動設定速率曲線")]
    [SerializeField] protected AnimationCurve moveCurve;
    private bool isAbsorbing = false;
    protected Rigidbody2D rb;
    [SerializeField] protected float playerKnockbackForce;
    [SerializeField] protected float trashKnockbackForce;
  [SerializeField] public TrashType trashType;
    [SerializeField] float randomForceStrength;

    [Header("反彈設定")]
    [Tooltip("反彈的總力道")]
    public float totalBounceStrength ; 

    [Tooltip("隨機偏移的強度 (0=完美反彈, 0.2=一點點偏移)")]
    public float randomnessStrength ;
    protected void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    private void  OnTriggerEnter2D(Collider2D other)
    {
      
        if (other.transform.CompareTag("blackHole"))
        {
          
            OnEnterBlackHole(other.transform.position);
            
        }
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {

        string tag = collision.transform.tag;

        if (tag == "player")
        {
            ApplyKnockback(collision, playerKnockbackForce);
        }
         if (tag == "trash")
        {
            ApplyKnockback(collision, trashKnockbackForce);
        }

        if (collision.gameObject.CompareTag("airWall"))
        {
            
            Vector2 surfaceNormal = collision.contacts[0].normal;
            Vector2 incomingDirection = -collision.relativeVelocity.normalized;
            Vector2 reflectionDirection = Vector2.Reflect(incomingDirection, surfaceNormal).normalized; // 確保為 1

           
            Vector2 randomNudge = Random.insideUnitCircle * randomnessStrength;

            
            Vector2 finalDirection = (reflectionDirection + randomNudge).normalized;
            

            rb.AddForce(finalDirection * totalBounceStrength, ForceMode2D.Impulse);
        }
    }
   
    private void ApplyKnockback(Collision2D collision, float force)
    {
        if (isAbsorbing) return;
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        Vector2 pushDirection = PlayerController.instance.rb.linearVelocity.normalized;
        rb.AddForce(pushDirection * force, ForceMode2D.Impulse);
    }
    //可能有些被吸了會觸發效果 所以用 virtual 讓子類別覆寫
    protected virtual void OnEnterBlackHole(Vector3 targetPosition)
    {
        if (!isAbsorbing)
        {
            isAbsorbing = true;        
            StartCoroutine(AbsorbEffect(targetPosition));
            
        }
    }


    protected virtual IEnumerator AbsorbEffect(Vector3 target)
    {
     
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic; // 變為運動學，不受力
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        // ------------------------------------

        Vector3 initialPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < absorbEffectDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / absorbEffectDuration;
            float scale_t = scaleCurve.Evaluate(t);
            float move_t = moveCurve.Evaluate(t);

            // 自轉
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            // 縮小
            transform.localScale = Vector3.LerpUnclamped(initialScale, Vector3.zero, scale_t);
            // 靠近
            transform.position = Vector2.LerpUnclamped(initialPosition, target, move_t);

            yield return null;
        }

       

        // 歸還物件池
        isAbsorbing = false; 
        TrashPool.Instance.ReturnTrash(this); // 在這裡呼叫！
    }
    public override void ResetState()
    {
      base.ResetState();
        // 重置物理狀態
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        else 
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
       
        isAbsorbing = false;
    }
}
