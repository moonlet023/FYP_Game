using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class fadeinfadeoutanmation : MonoBehaviour
{
    [SerializeField] private TMP_Text fadeObject;
    // Start is called before the first frame update
    void Start()
    {
        StartFade(true, 0.5f); // 開始淡出，持續時間為 0.5 秒
    }

    // Update is called once per frame
    void Update()
    {
        
    }

     public void StartFade(bool fadeAway, float duration)
    {
        StartCoroutine(FadeSprite(fadeAway, duration));
    }

    private IEnumerator FadeSprite(bool fadeAway, float duration)
    {
        Color startColor = fadeObject.color;
        float startAlpha = startColor.a;
        float targetAlpha = fadeAway ? 0f : 1f; // true 則淡出到 0，false 則淡入到 1

        while(true){
            float time = 0;
                //fade out
                while (time < duration)
                {
                        time += Time.deltaTime;
                        float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                        fadeObject.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);
                        yield return null;
                }
                //fade in
                    while (time > 0)
                    {
                        time -= Time.deltaTime;
                        float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                    fadeObject.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);
                yield return null;
            }
        }
        
    }
}
