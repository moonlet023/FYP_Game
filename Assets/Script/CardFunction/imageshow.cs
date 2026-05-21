using System;
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
    private GamePlay _gamePlay;
    private CardEvent _cardEvent;
    private string _lastAppliedPath;
    private int _lastShownAtk = int.MinValue;
    private int _lastShownDef = int.MinValue;

    void Start()
    {
        ResolveBindings();
        _gamePlay = FindObjectOfType<GamePlay>();
        _cardEvent = FindObjectOfType<CardEvent>();
        TryRefresh();
    }

    void Update()
    {
        if (_cardData == null)
        {
            ResolveBindings();
            if (_cardData == null) return;
        }

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
        if (_cardData == null)
            ResolveBindings();

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

    public void BindCardInfo(GameObject sourceCard)
    {
        if (sourceCard != null)
            cardinfo = sourceCard;

        ResolveBindings();
        TryRefresh();
    }

    private void ResolveBindings()
    {
        if (cardinfo == null)
        {
            _cardData = null;
            _cardClickData = null;
            return;
        }

        _cardData = cardinfo.GetComponent<CardData>();
        _cardClickData = cardinfo.GetComponent<leftRightClickCard>();

        if (_cardData == null)
            Debug.LogWarning("[imageshow] CardData not found on cardinfo");
    }

    private int ResolveCurrentAtk()
    {
        if (_cardData != null && !string.IsNullOrWhiteSpace(_cardData.id))
        {
            if (_cardEvent == null) _cardEvent = FindObjectOfType<CardEvent>();
            if (_gamePlay == null) _gamePlay = FindObjectOfType<GamePlay>();

            if (_cardEvent != null && _cardEvent.TryGetCardById(_cardData.id.Trim(), out var data) && data != null)
            {
                int baseAtk = Mathf.Max(0, data.Atk);
                if (_gamePlay != null)
                    return _gamePlay.GetPlayerAttackWithBuff(_cardData.id.Trim(), baseAtk);
                return baseAtk;
            }
        }

        if (_cardClickData != null && _cardClickData.selectedAttackDamage > 0)
            return Mathf.Max(0, _cardClickData.selectedAttackDamage);

        return _cardData != null ? Mathf.Max(0, _cardData.Atk) : 0;
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

}
