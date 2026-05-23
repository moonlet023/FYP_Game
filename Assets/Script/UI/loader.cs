using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class loader : MonoBehaviour
{
    [SerializeField] private RawImage fadeObject;
    [SerializeField] private TMP_Text text;
    [SerializeField] private GameObject nowScreen;
    [SerializeField] private GameObject nextScreen;
     
    // Start is called before the first frame update
    IEnumerator Start()
    {
        if (fadeObject != null)
        {
            var c = fadeObject.color;
            fadeObject.color = new Color(c.r, c.g, c.b, 0f);
        }

        if (text != null)
        {
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, 0f);
        }

        yield return StartCoroutine(FadeIn(3f));

        if (nowScreen != null)
            nowScreen.SetActive(false);
        if (nextScreen != null)
            nextScreen.SetActive(true);
    }

    private IEnumerator FadeIn(float duration)
    {
        if (fadeObject == null && text == null)
            yield break;

        float elapsed = 0f;
        float startAlpha = 0f;
        float targetAlpha = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);

            if (fadeObject != null)
            {
                var c = fadeObject.color;
                fadeObject.color = new Color(c.r, c.g, c.b, alpha);
            }

            if (text != null)
            {
                var c = text.color;
                text.color = new Color(c.r, c.g, c.b, alpha);
            }

            yield return null;
        }

        if (fadeObject != null)
        {
            var c = fadeObject.color;
            fadeObject.color = new Color(c.r, c.g, c.b, targetAlpha);
        }

        if (text != null)
        {
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, targetAlpha);
        }
    }
}

