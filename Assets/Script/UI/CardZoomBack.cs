using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardZoomBack : MonoBehaviour
{
    public Button backButton;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        backButton.onClick.AddListener(OnBackButtonClicked);
    }

    public void OnBackButtonClicked()
    {
        this.gameObject.SetActive(false);
    }
}
