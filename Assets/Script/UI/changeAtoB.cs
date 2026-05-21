using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class changeAtoB : MonoBehaviour
{
    [SerializeField] private GameObject[] ATK;
    [SerializeField] private GameObject[] BENCH;
    [SerializeField] private Button changebtn;
    [SerializeField] private float swapDuration = 0.5f;
    [SerializeField] private AnimationCurve swapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool isSwapping;

    void Start()
    {
        if (changebtn != null)
            changebtn.onClick.AddListener(OnChangeButtonClicked);
    }

    private void OnDestroy()
    {
        if (changebtn != null)
            changebtn.onClick.RemoveListener(OnChangeButtonClicked);
    }

    public void OnChangeButtonClicked()
    {
        if (!isSwapping)
            StartCoroutine(ChangeCoroutine());
    }

    private IEnumerator ChangeCoroutine()
    {
        if (ATK == null || BENCH == null || ATK.Length == 0 || BENCH.Length == 0)
        {
            Debug.LogWarning("changeAtoB: ATK or BENCH references not assigned.");
            yield break;
        }

        int count = Mathf.Min(ATK.Length, BENCH.Length);
        if (count == 0)
        {
            Debug.LogWarning("changeAtoB: No objects available to swap.");
            yield break;
        }

        isSwapping = true;
        if (changebtn != null)
            changebtn.interactable = false;

        var atkStartPositions = new Vector3[count];
        var benchStartPositions = new Vector3[count];
        var atkTargetPositions = new Vector3[count];
        var benchTargetPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            if (ATK[i] == null || BENCH[i] == null)
                continue;

            atkStartPositions[i] = ATK[i].transform.position;
            benchStartPositions[i] = BENCH[i].transform.position;
            atkTargetPositions[i] = benchStartPositions[i];
            benchTargetPositions[i] = atkStartPositions[i];
        }

        float elapsed = 0f;
        while (elapsed < swapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swapDuration);
            float eased = swapCurve.Evaluate(t);

            for (int i = 0; i < count; i++)
            {
                if (ATK[i] != null)
                    ATK[i].transform.position = Vector3.Lerp(atkStartPositions[i], atkTargetPositions[i], eased);
                if (BENCH[i] != null)
                    BENCH[i].transform.position = Vector3.Lerp(benchStartPositions[i], benchTargetPositions[i], eased);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (ATK[i] != null)
                ATK[i].transform.position = atkTargetPositions[i];
            if (BENCH[i] != null)
                BENCH[i].transform.position = benchTargetPositions[i];
        }

        if (changebtn != null)
            changebtn.interactable = true;

        isSwapping = false;
    }
}
