using UnityEngine;
using UnityEngine.UI;
using Discord.Sdk;
using System.Linq;
using TMPro;

public class DiscordManager : MonoBehaviour
{
    [SerializeField] 
    private ulong clientId; // Set this in the Unity Inspector from the dev portal
    
    [SerializeField]
    private Button loginButton; 
    
    [SerializeField] 
    private TMP_Text statusText;

    private Client client;
    private string codeVerifier;

    void Start()
    {
        client = new Client();

        // Modifying LoggingSeverity will show you more or less logging information
        client.AddLogCallback(OnLog, LoggingSeverity.Error);
        client.SetStatusChangedCallback(OnStatusChanged);

        // Make sure the button has a listener
        if (loginButton != null)
        {
            //loginButton.onClick.AddListener(StartOAuthFlow);
        }
        else
        {
            Debug.LogError("Login button reference is missing, connect it in the inspector!");
        }

        // Set initial status text
        if (statusText != null)
        {
            statusText.text = "Ready to login";
        }
        else
        {
            Debug.LogError("Status text reference is missing, connect it in the inspector!");
        }
    }

    private void OnLog(string message, LoggingSeverity severity)
    {
        Debug.Log($"Log: {severity} - {message}");
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        Debug.Log($"Status changed: {status}");
        statusText.text = status.ToString();
        if(error != Client.Error.None)
        {
            Debug.LogError($"Error: {error}, code: {errorCode}");
        }
    }
}
