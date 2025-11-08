using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;

public class server_event : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ConnectToServer();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void ConnectToServer()
    {
        ConnectToServerAsync();
    }

    async void ConnectToServerAsync()
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                var connectTask = client.ConnectAsync("58.152.254.9", 6660);
                var timeoutTask = Task.Delay(5000); // 5 秒超時
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed == connectTask && client.Connected)
                {
                    Debug.Log("Connected to server (no data sent).");
                }
                else
                {
                    Debug.Log("Connection attempt timed out or failed.");
                }
                // 不傳送任何資料，using 會自動關閉連線
            }
        }
        catch (Exception ex)
        {
            Debug.Log("Connect failed: " + ex.Message);
        }
    }
}
