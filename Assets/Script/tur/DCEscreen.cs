using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DCEscreen : MonoBehaviour
{
    [SerializeField] private Button Discard;
    [SerializeField] private Button Core;
    [SerializeField] private Button Energy;
    [SerializeField] private GameObject DiscardPanel;
    [SerializeField] private GameObject CorePanel;
    [SerializeField] private GameObject EnergyPanel;
    void Start()
    {
        if (Discard != null)
            Discard.onClick.RemoveAllListeners();
        if (Core != null)
            Core.onClick.RemoveAllListeners();
        if (Energy != null)
            Energy.onClick.RemoveAllListeners();

        Discard.onClick.AddListener(() => {
            DiscardPanel.SetActive(true);
            CorePanel.SetActive(false);
            EnergyPanel.SetActive(false);
        });

        Core.onClick.AddListener(() => {
            DiscardPanel.SetActive(false);
            CorePanel.SetActive(true);
            EnergyPanel.SetActive(false);
        });

        Energy.onClick.AddListener(() => {
            DiscardPanel.SetActive(false);
            CorePanel.SetActive(false);
            EnergyPanel.SetActive(true);
        });

        // 預設顯示棄牌面板
        if (DiscardPanel != null) DiscardPanel.SetActive(true);
        if (CorePanel != null) CorePanel.SetActive(false);
        if (EnergyPanel != null) EnergyPanel.SetActive(false);
    }
}
