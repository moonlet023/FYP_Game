using System;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string username;
    public string password;
    
    public PlayerData(string username, string password)
    {
        this.username = username;
        this.password = password;
    }
}

[System.Serializable]
public class LoginResponse
{
    public string _id;
    public string username;
    public string password;
}

[System.Serializable] 
public class ApiResponse
{
    public bool success;
    public string message;
}