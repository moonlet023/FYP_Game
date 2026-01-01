using UnityEngine;

public class CardIdentity : MonoBehaviour
{
    [SerializeField]
    private string id;

    public string Id
    {
        get => id;
        set => id = value;
    }
}
