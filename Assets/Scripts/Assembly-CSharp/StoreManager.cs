using GBTemplates.Domino.Services;
using TMPro;
using UnityEngine;

public class StoreManager : MonoBehaviour
{
	[Header("PAINEIS")]
	public GameObject storePanel;

	[Header("MOEDAS")]
	public TMP_Text[] coinsTexts;

	private int coins;

	private float nextBalanceRefreshAt;

	private void Start()
	{
		coins = PlayerPrefs.GetInt("Coins", 6000);
		UpdateCoinsUI();
		if (storePanel != null)
		{
			storePanel.SetActive(value: false);
		}
	}

	public void OpenStore()
	{
		ExternalStoreBridgeService.TryOpenStore();
	}

	public void CloseStore()
	{
		if (storePanel != null)
		{
			storePanel.SetActive(value: false);
		}
	}

	public void AddCoins(int amount)
	{
		coins += amount;
		PlayerPrefs.SetInt("Coins", coins);
		PlayerPrefs.Save();
		UpdateCoinsUI();
	}

	public bool SpendCoins(int amount)
	{
		if (coins >= amount)
		{
			coins -= amount;
			PlayerPrefs.SetInt("Coins", coins);
			PlayerPrefs.Save();
			UpdateCoinsUI();
			return true;
		}
		Debug.Log("Moedas insuficientes");
		return false;
	}

	private void UpdateCoinsUI()
	{
		TMP_Text[] array = coinsTexts;
		foreach (TMP_Text tMP_Text in array)
		{
			if (tMP_Text != null)
			{
				tMP_Text.text = coins.ToString("N0");
			}
		}
	}

	private void Update()
	{
		if (!(Time.unscaledTime < nextBalanceRefreshAt))
		{
			nextBalanceRefreshAt = Time.unscaledTime + 0.5f;
			int num = PlayerPrefs.GetInt("Coins", 6000);
			if (num != coins)
			{
				coins = num;
				UpdateCoinsUI();
			}
		}
	}

	public void BuySmallPack()
	{
		ExternalStoreBridgeService.TryOpenStore();
	}

	public void BuyMediumPack()
	{
		ExternalStoreBridgeService.TryOpenStore();
	}

	public void BuyBigPack()
	{
		ExternalStoreBridgeService.TryOpenStore();
	}
}
