using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class openDCEinfo : MonoBehaviour
{
    [SerializeField] private GameObject DCEinfoPanel; // DCE info 面板
    [SerializeField] private Button showButton; // 顯示 DCE info 按鈕
    [SerializeField] private Button backButton; // 返回按鈕
    void Start()
    {
        if (showButton != null)
            showButton.onClick.RemoveAllListeners();
        if (backButton != null)
            backButton.onClick.RemoveAllListeners();

        showButton.onClick.AddListener(() => {
            DCEinfoPanel.SetActive(true);
        });
        backButton.onClick.AddListener(() => {
            DCEinfoPanel.SetActive(false);
        });
    }
}
