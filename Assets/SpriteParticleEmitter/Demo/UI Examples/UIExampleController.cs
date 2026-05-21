using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class UIExampleController : MonoBehaviour
{
    public Toggle maskToggle;
    public Image maskBorder;

    public List<Image> maskImages;
    public Sprite[] windowSprites;
    public Image window;

    public Image playerHealth;
    public Image opponentHealth;
    public Image mana;

    public Slider healthSlider;
    public Slider manaSlider;

    public int maxHealth = 20;
    public float healthContainerHeight = 240f;
    public float manaContainerHeight = 240f;

    private int currentPlayerHealth;
    private int currentOpponentHealth;

    public int PlayerHealthValue
    {
        get => currentPlayerHealth;
        set
        {
            currentPlayerHealth = value;
            SetPlayerHealth(value);
        }
    }

    public int OpponentHealthValue
    {
        get => currentOpponentHealth;
        set
        {
            currentOpponentHealth = value;
            SetOpponentHealth(value);
        }
    }

	// Use this for initialization
	void Start () 
    {
	    maskImages = new List<Image>();

	    Mask[] masks = FindObjectsOfType<Mask>();

	    foreach (Mask mask1 in masks)
	    {
	        maskImages.Add(mask1.GetComponent<Image>());
	    }

    }
    
	// Update is called once per frame
	void Update () 
    {
	
	}

    public void ToggleMask()
    {
        foreach (Image maskImage in maskImages)
        {
            maskImage.enabled = maskToggle.isOn;    
        }
        maskBorder.enabled = maskToggle.isOn;
    }

    public void ChangeWindowType(int i)
    {
        window.sprite = windowSprites[i];
    }

    public void SetHealthBar(Image target, int healthValue)
    {
        if (target == null)
            return;

        int displayedHealth = Mathf.Clamp(healthValue, 0, maxHealth);
        float ratio = (float)displayedHealth / maxHealth;
        target.rectTransform.sizeDelta = new Vector2(target.rectTransform.sizeDelta.x, ratio * healthContainerHeight);
    }

    public void SetPlayerHealth(int healthValue)
    {
        SetHealthBar(playerHealth, healthValue);
    }

    public void SetOpponentHealth(int healthValue)
    {
        SetHealthBar(opponentHealth, healthValue);
    }

    public void UpdateHealthBars(int playerHealthValue, int opponentHealthValue)
    {
        SetPlayerHealth(playerHealthValue);
        SetOpponentHealth(opponentHealthValue);
    }

    public void SetHealth(int healthValue)
    {
        SetPlayerHealth(healthValue);
    }

    public void OnSlidersChanged()
    {
        float healthValue = healthSlider.value;
        if (healthSlider.maxValue <= 1f)
        {
            healthValue *= maxHealth;
        }

        SetHealth(Mathf.RoundToInt(healthValue));
        mana.rectTransform.sizeDelta = new Vector2(mana.rectTransform.sizeDelta.x, manaSlider.value * manaContainerHeight);
    }
}
