using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// 點擊角色顯示對話，支援逐字、翻頁、迴圈，以及每 N 次點擊顯示隱藏訊息（顯示卡型號）
public class talk : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 元件")]
    public GameObject dialoguePanel; // 對話面板（包含背景）
    public TMP_Text dialogueText; // 顯示文字的 UI Text

    [Header("對話內容")]
    [TextArea]
    public string[] messages; // 多條訊息

    [Header("設定")]
    public float typingSpeed = 0.02f; // 逐字速度
    public bool loopDialog = true; // 完成最後一條後是否回到第一條

    [Header("隱藏對話")]
    public bool enableHiddenMessage = true;
    [Tooltip("每多少次點擊會顯示額外隱藏文字（預設 100）")]
    public int clicksToTrigger = 100;
    [TextArea]
    public string specialHiddenMessage = "73 65 63 72 65 74 20 6c 30 45 2b 28 36";

    [Header("顯卡顯示")]
    [Range(0f, 1f)]
    public float gpuChance = 0.1f;
    [TextArea]
    public string hiddenMessagePrefix = "你發現了一個隱藏的對話！你的顯示卡：";

    int clickCounter = 0;
    bool isShowingHidden = false;
    bool isShowingGpu = false;

    int current = 0;
    Coroutine typingCoroutine;

    void Start()
    {
        // 若沒有在 Inspector 指定 dialogueText，嘗試自動抓取
        if (dialogueText == null && dialoguePanel != null)
            dialogueText = dialoguePanel.GetComponentInChildren<TMP_Text>();

        // 保持面板在 Inspector 中的預設狀態，不強制隱藏或顯示
    }

    void OnMouseDown()
    {
        HandleClick();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick();
    }

    void HandleClick()
    {
        if (dialoguePanel == null || dialogueText == null)
        {
            Debug.LogWarning("talk: dialoguePanel 或 dialogueText 未設定");
            return;
        }

        // 計次：每次點擊都累計
        clickCounter++;

        // 如果不是正在顯示 hidden/gpu，先檢查是否要觸發特殊輸出
        if (!isShowingHidden && !isShowingGpu && TryTriggerSpecialOutput())
            return;

        if (!dialoguePanel.activeSelf)
        {
            isShowingHidden = false;
            isShowingGpu = false;
            current = 0;
            dialoguePanel.SetActive(true);
            if (messages != null && messages.Length > 0)
                StartTyping(messages[current]);
            else
                dialogueText.text = "";
        }
        else
        {
            // 若正在逐字顯示，點擊會直接顯示全文；否則顯示下一句或關閉
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
                if (isShowingHidden)
                    dialogueText.text = specialHiddenMessage;
                else if (isShowingGpu)
                    dialogueText.text = hiddenMessagePrefix + SystemInfo.graphicsDeviceName;
                else if (messages != null && messages.Length > 0)
                    dialogueText.text = messages[current];
            }
            else
            {
                // 如果剛剛顯示的是隱藏對話或顯卡訊息，點擊後改為繼續一般對話，不關閉面板
                if (isShowingHidden || isShowingGpu)
                {
                    isShowingHidden = false;
                    isShowingGpu = false;

                    if (messages == null || messages.Length == 0)
                    {
                        return;
                    }

                    current = 0;
                    StartTyping(messages[current]);
                    return;
                }

                if (messages == null || messages.Length == 0)
                {
                    dialoguePanel.SetActive(false);
                    return;
                }

                current++;
                if (current < messages.Length)
                {
                    StartTyping(messages[current]);
                }
                else
                {
                    if (loopDialog)
                    {
                        current = 0;
                        StartTyping(messages[current]);
                    }
                    else
                    {
                        dialoguePanel.SetActive(false);
                    }
                }
            }
        }
    }

    bool TryTriggerSpecialOutput()
    {
        if (!enableHiddenMessage)
            return false;

        if (clicksToTrigger > 0 && clickCounter % clicksToTrigger == 0)
        {
            isShowingHidden = true;
            isShowingGpu = false;
            dialoguePanel.SetActive(true);
            StartTyping(specialHiddenMessage);
            return true;
        }

        if (Random.value < gpuChance)
        {
            isShowingHidden = false;
            isShowingGpu = true;
            dialoguePanel.SetActive(true);
            string gpu = SystemInfo.graphicsDeviceName;
            StartTyping(hiddenMessagePrefix + gpu);
            return true;
        }

        return false;
    }

    void StartTyping(string msg)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeText(msg));
    }

    IEnumerator TypeText(string msg)
    {
        dialogueText.text = "";
        foreach (char c in msg)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
        typingCoroutine = null;
    }

    // 供外部呼叫顯示指定訊息索引（非隱藏訊息）
    public void ShowMessage(int index)
    {
        if (dialoguePanel == null || dialogueText == null) return;
        if (messages == null || index < 0 || index >= messages.Length) return;
        isShowingHidden = false;
        dialoguePanel.SetActive(true);
        current = index;
        StartTyping(messages[current]);
    }

    // 關閉對話面板
    public void Close()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }
}
