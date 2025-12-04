using UnityEngine;
using System.Collections;
public class Rock : MonoBehaviour
{
    [SerializeField] private SpriteRenderer renderer;
    [SerializeField] private float fadeDuration = 5f; // 淡出時間，預設 1 秒
    private void Start()
    {
        StartCoroutine(FadeAndDestroy());
    }
    private IEnumerator FadeAndDestroy()
    {

        float elapsed = 0f;
        Color startColor = renderer.color;
        float startAlpha = startColor.a;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float a = Mathf.Lerp(startAlpha, 0f, t);
            renderer.color = new Color(startColor.r, startColor.g, startColor.b, a);
            yield return null;
        }

        // 確保完全透明
        renderer.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

        Destroy(gameObject);
    }
}
