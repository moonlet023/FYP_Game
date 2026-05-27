using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class leave : MonoBehaviour
{

    public GameObject leavePanel;
    public Button leaveButton;
    

    void Start()
    {
       leaveButton.onClick.AddListener(
            () =>
            {
                Application.Quit();
            }
       );
    }

    //right click to leave panel esc to open leave panel
    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            leavePanel.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            leavePanel.SetActive(true);
        }
    }
    
    

   
}
