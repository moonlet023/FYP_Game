using System;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string username;
    public string password;
    public string uid;
    
    public PlayerData(string username, string password,string uid)
    {
        this.username = username;
        this.password = password;
        this.uid = uid;
    }

    public PlayerData(string username, string password)
    {
        this.username = username;
        this.password = password;
        this.uid = "";
    }
}

[System.Serializable]
public class LoginResponse
{
    public string uid;
    public string username;
    public string password;
}

[System.Serializable] 
public class ApiResponse
{
    public bool success;
    public string message;
}