using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class prassanytoenter : MonoBehaviour
{
    [SerializeField] private GameObject login;
    [SerializeField] private GameObject thisscreen;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // press any screen or key to enter
        if (Input.anyKeyDown)
        {
            login.SetActive(true);
            thisscreen.SetActive(false);
        }
    }
}
