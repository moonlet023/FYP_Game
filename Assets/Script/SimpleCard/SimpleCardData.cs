using UnityEngine;

// 讓每張卡片帶有一個識別用的 cardId（可用於手牌記錄）
public class SimpleCardData : MonoBehaviour
{
    public string cardId; // 例如 "Fireball#001"；若空，會使用物件名稱記錄
}
