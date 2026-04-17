using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class showEnergy : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI rednum;
    [SerializeField] private TMPro.TextMeshProUGUI bluenum;
    [SerializeField] private TMPro.TextMeshProUGUI greennum;
    [SerializeField] private TMPro.TextMeshProUGUI yellownum;

    [SerializeField] private GamePlay gamePlayOverride;

    private GamePlay gamePlay;

    void Start()
    {
        StartCoroutine(InitializeAfterDelay());
    }

    private IEnumerator InitializeAfterDelay()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        gamePlay = gamePlayOverride != null ? gamePlayOverride : FindObjectOfType<GamePlay>();
        if (gamePlay == null)
        {
            Debug.LogError("[showEnergy] 找不到 GamePlay 組件", this);
            yield break;
        }

        gamePlay.OnPlayerEnergyUpdated += OnEnergyUpdated;

        // 初始化顯示
        RefreshEnergyDisplay(gamePlay.Energy);
    }

    private void OnEnergyUpdated(IReadOnlyList<string> energyList)
    {
        RefreshEnergyDisplay(energyList);
    }

    private void RefreshEnergyDisplay(IReadOnlyList<string> energyList)
    {
        int red = 0, blue = 0, green = 0, yellow = 0;

        if (energyList != null)
        {
            foreach (var color in energyList)
            {
                switch (color.ToLower())
                {
                    case "red":    red++;    break;
                    case "blue":   blue++;   break;
                    case "green":  green++;  break;
                    case "yellow": yellow++; break;
                }
            }
        }

        if (rednum    != null) rednum.text    = red.ToString();
        if (bluenum   != null) bluenum.text   = blue.ToString();
        if (greennum  != null) greennum.text  = green.ToString();
        if (yellownum != null) yellownum.text = yellow.ToString();
    }

    private void OnDestroy()
    {
        if (gamePlay != null)
            gamePlay.OnPlayerEnergyUpdated -= OnEnergyUpdated;
    }
}
