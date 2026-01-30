using System;

[Serializable]
public class StoredUserInfo
{
    public string username;
    public string uid;

    public StoredUserInfo(string username, string uid)
    {
        this.username = username ?? string.Empty;
        this.uid = uid ?? string.Empty;
    }
}
