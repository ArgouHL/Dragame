using UnityEngine;
using System.Collections;
public class Droppeditem : MonoBehaviour
{
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private float fadeDuration ; 
    [SerializeField] private bool canPick;

    private void Start()
    {
      StartCoroutine(FadeAndDestroy());
    }
    private IEnumerator FadeAndDestroy()
    {
        float elapsed = 0f;
        Color startColor = render.color;
        float startAlpha = startColor.a;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float a = Mathf.Lerp(startAlpha, 0f, t);
            render.color = new Color(startColor.r, startColor.g, startColor.b, a);
            yield return null;
        }

   
        render.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

        Destroy(gameObject);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canPick) return;
        if (other.CompareTag("Player"))
        {
            
            Debug.Log("Caught a droplet!");

         
            TestPlayerController.instance.UpdateDropletCount();
         

         
            Destroy(gameObject);
        }
    }


}
