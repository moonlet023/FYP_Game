using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OnlineOrAI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject chooseModeMenu;

     void Awake()
    {
        button.onClick.AddListener(OnButtonClick);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        button.onClick.AddListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        mainMenu.SetActive(false);
        chooseModeMenu.SetActive(true);
    }
}
