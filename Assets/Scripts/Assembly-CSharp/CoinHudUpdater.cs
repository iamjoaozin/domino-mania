using UnityEngine;
using TMPro;

public class CoinHudUpdater : MonoBehaviour
{
    private TextMeshProUGUI coinText;

    void Start()
    {
        // Tenta achar o texto dentro do bg moeda
        coinText = GetComponentInChildren<TextMeshProUGUI>();
        UpdateCoins();
    }

    void Update()
    {
        UpdateCoins();
    }

    private void UpdateCoins()
    {
        if (coinText != null)
        {
            // Puxa o saldo exato do sistema nativo de PlayerPrefs
            coinText.text = PlayerPrefs.GetInt("Coins", 0).ToString();
        }
    }
}
