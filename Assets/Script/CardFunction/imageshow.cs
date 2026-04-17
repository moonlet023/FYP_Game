using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class imageshow : MonoBehaviour
{
    public RawImage image;
    public GameObject cardinfo;
    public string imagepath;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;

    private CardData _cardData;
    private leftRightClickCard _cardClickData;
    private string _lastAppliedPath;
    private int _lastShownAtk = int.MinValue;
    private int _lastShownDef = int.MinValue;

    void Start()
    {
        if (cardinfo == null)
        {
            Debug.LogWarning("[imageshow] cardinfo not assigned");
            return;
        }

        _cardData = cardinfo.GetComponent<CardData>();
        if (_cardData == null)
        {
            Debug.LogWarning("[imageshow] CardData not found on cardinfo");
            return;
        }

        _cardClickData = cardinfo.GetComponent<leftRightClickCard>();

        TryRefresh();
    }

    void Update()
    {
        if (_cardData == null) return;
        int currentAtk = ResolveCurrentAtk();
        int currentDef = _cardData.Def;
        bool needsRefresh = _cardData.imagePath != _lastAppliedPath
            || currentAtk != _lastShownAtk
            || currentDef != _lastShownDef;

        if (needsRefresh)
            TryRefresh();
    }

    public void TryRefresh()
    {
        if (_cardData == null) return;

        imagepath = _cardData.imagePath;

        if (!string.IsNullOrWhiteSpace(imagepath))
        {
            ApplyImage(imagepath);
            _lastAppliedPath = imagepath;
        }

        int atk = ResolveCurrentAtk();
        int def = _cardData.Def;
        if (atkText != null) atkText.text = atk.ToString();
        if (defText != null) defText.text = def.ToString();
        _lastShownAtk = atk;
        _lastShownDef = def;
    }

    private int ResolveCurrentAtk()
    {
        if (_cardClickData != null && _cardClickData.selectedAttackDamage > 0)
            return _cardClickData.selectedAttackDamage;

        return _cardData != null ? _cardData.Atk : 0;
    }

    private void ApplyImage(string path)
    {
        if (image == null || string.IsNullOrWhiteSpace(path)) return;

        string normalized = path.Replace('\\', '/').Trim();
        string fullPath = normalized.StartsWith("Assets/")
            ? Path.Combine(Application.dataPath, normalized.Substring("Assets/".Length))
            : Path.Combine(Application.dataPath, normalized);

        fullPath = fullPath.Replace('\\', '/');
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[imageshow] Image not found: {fullPath}");
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
                image.texture = tex;
            else
            {
                Destroy(tex);
                Debug.LogWarning($"[imageshow] Failed to decode image: {fullPath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[imageshow] ApplyImage failed: {e.Message}");
        }
    }

    // Update is called once per frame
}
