using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Handcontroller : MonoBehaviour
{
    public HandData handData;
    public DeckData deckData;
    [Header("動畫與版面設定")]
    public Transform deckSpawnPoint;        // 抽牌起始位置（例如牌堆頂）
    public Transform handContainer;          // 承載手牌的父物件（其座標系為版面基準）
    public float handSpacing = 1.2f;         // 手牌水平間距（localX）
    public float drawDuration = 0.4f;        // 抽牌飛入時間
    public AnimationCurve drawCurve;         // 動畫曲線（0→1）

    // 目前在手上的卡牌對應的 Transform（若 handData 內含物件，可在外部同步填入）
    public List<Transform> handCardTransforms = new List<Transform>();

    void Start()
    {
        handData = new HandData();
        deckData = new DeckData();
        if (drawCurve == null)
        {
            // 預設使用緩入緩出
            drawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    void Update()
    {
        
    }

    public void init()
    {
        deckData.suffleDeck();
        deckData.drawCard(handData, 5);
        handData.PrintHandLog();
        deckData.PrintDeckLog();

        // 若已有對應的卡牌 GameObject，可於外部填入 handCardTransforms 後置中
        CenterHand();
    }

    // 將目前手牌在 handContainer 下以等距方式水平置中排布
    public void CenterHand()
    {
        if (handContainer == null) return;
        var count = handCardTransforms.Count;
        if (count == 0) return;

        // 以 handContainer 的 local 空間為基準，沿 X 排列並置中
        float startX = -(count - 1) * 0.5f * handSpacing;
        for (int i = 0; i < count; i++)
        {
            var t = handCardTransforms[i];
            if (t == null) continue;
            t.SetParent(handContainer, worldPositionStays: false);
            var targetLocal = new Vector3(startX + i * handSpacing, 0f, 0f);
            t.localPosition = targetLocal;
        }
    }

    // 將某張牌動畫地移動到置中位置（相對於 handContainer 的 local 原點）
    public void CenterSelectedCard(Transform card)
    {
        if (card == null || handContainer == null) return;
        StartCoroutine(AnimateToLocal(card, Vector3.zero, drawDuration));
    }

    // 抽牌動畫：將卡牌從 deckSpawnPoint 世界座標飛到 handContainer 內對應索引的置中排布位置
    public void PlayDrawCardAnimation(Transform cardTransform, int targetIndex)
    {
        if (cardTransform == null || handContainer == null || deckSpawnPoint == null) return;

        // 計算目標 local 位置（把該牌視為已加入 hand，重算置中座標）
        int count = Mathf.Max(handCardTransforms.Count, 0) + 1;
        float startX = -(count - 1) * 0.5f * handSpacing;
        targetIndex = Mathf.Clamp(targetIndex, 0, count - 1);
        Vector3 targetLocal = new Vector3(startX + targetIndex * handSpacing, 0f, 0f);

        // 設置父子關係到 handContainer，並將起始位置放在 deckSpawnPoint（世界座標）
        cardTransform.SetParent(handContainer, worldPositionStays: true);
        cardTransform.position = deckSpawnPoint.position;

        // 啟動動畫，飛入目標 local 位置
        StartCoroutine(AnimateToLocal(cardTransform, targetLocal, drawDuration, onComplete: () =>
        {
            // 動畫完成後，真正加入手牌清單並重新置中一次
            handCardTransforms.Insert(Mathf.Clamp(targetIndex, 0, handCardTransforms.Count), cardTransform);
            CenterHand();
        }));
    }

    // 協程：將目標 Transform 由目前 localPosition 動畫到目標 localPosition
    private IEnumerator AnimateToLocal(Transform t, Vector3 targetLocal, float duration, System.Action onComplete = null)
    {
        if (t == null) yield break;
        if (duration <= 0f)
        {
            t.localPosition = targetLocal;
            onComplete?.Invoke();
            yield break;
        }

        Vector3 startLocal = t.parent != null ? t.localPosition : t.position;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / duration);
            float k = drawCurve != null ? drawCurve.Evaluate(p) : p;
            Vector3 cur = Vector3.Lerp(startLocal, targetLocal, k);
            if (t.parent != null)
                t.localPosition = cur;
            else
                t.position = cur; // 若無父物件就以世界座標移動
            yield return null;
        }
        if (t.parent != null)
            t.localPosition = targetLocal;
        else
            t.position = targetLocal;
        onComplete?.Invoke();
    }
}