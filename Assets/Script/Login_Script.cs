using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using ServerLib.RCH_Connection;
using TMPro;
using UnityEngine.UI;

public class Login_Script : MonoBehaviour
{
    [SerializeField] private TMP_InputField username;
    [SerializeField] private TMP_InputField password;
    private Button loginButton;

    void Start()
    {
        TestServer();
        loginButton = GetComponent<Button>();
        loginButton.onClick.AddListener(OnLoginButtonClick);
    }

    void Update()
    {

    }

    public void OnLoginButtonClick()
    {
        var conn = new RCH_Connection();
        StartCoroutine(conn.StartLogin(username.text, password.text, resp =>
        {
            if (resp == null) { Debug.LogError("No response"); return; }
            if (resp.ok) Debug.Log("Login OK");
            else Debug.LogError("Login failed: " + resp.error);
        }));
    }

    public void TestServer()
    {
        var conn = new RCH_Connection();
        
        // 先測試 GET 根路徑
        StartCoroutine(conn.TestServerGet(response => {
            Debug.Log("GET response: " + response);
        }));
        
        // 原本的 POST 測試（保留）
        var reqBody = new RCH_Connection.Req { username = "testuser", password = "testpass" };
        StartCoroutine(conn.testServer(reqBody, resp =>
        {
            if (resp == null) { Debug.LogError("No response from POST"); return; }
            if (resp.ok) Debug.Log("POST test OK");
            else Debug.LogError("POST test failed: " + resp.error);
        }));
    }
}