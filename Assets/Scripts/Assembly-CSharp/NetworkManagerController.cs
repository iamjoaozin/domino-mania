using GBTemplates.Domino.Controller;
using GBTemplates.Domino.Model;
using Unity.Netcode;
using UnityEngine;

public class NetworkManagerController : MonoBehaviour
{
	private const int k_popup_disconnect_id = 12;
	private static float suppressDisconnectPopupUntil;
	private bool disconnectPopupOpening;

	public static void SuppressDisconnectPopup(float seconds)
	{
		suppressDisconnectPopupUntil = Mathf.Max(suppressDisconnectPopupUntil, Time.realtimeSinceStartup + Mathf.Max(0f, seconds));
	}

	private static bool IsDisconnectPopupSuppressed()
	{
		return Time.realtimeSinceStartup < suppressDisconnectPopupUntil;
	}

	private void Start()
	{
		if (NetworkManager.Singleton == null)
		{
			return;
		}

		NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
		NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
	}

	private void OnDestroy()
	{
		if (!(NetworkManager.Singleton == null))
		{
			NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
		}
	}

	private void OnClientConnected(ulong clientId)
	{
		if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
		{
			if (NetworkLobbyController.Instance != null)
			{
				NetworkLobbyController.Instance.StartDominoGameMultiplayer();
			}

			DependencyCache.DominoController?.StartDominoGameMultiplayer();
		}
	}

	private void OnClientDisconnected(ulong clientId)
	{
		if (IsDisconnectPopupSuppressed())
		{
			UnityEngine.Debug.Log("[Network] Ignored expected disconnect while switching from online matchmaking to local bot match.");
			return;
		}

		if (DependencyCache.DominoController != null && DependencyCache.DominoController.IsInMatch)
		{
			HandleLocalDisconnection("Client disconnected");
		}
	}

	private async void HandleLocalDisconnection(string message)
	{
		if (disconnectPopupOpening || IsDisconnectPopupSuppressed())
		{
			return;
		}

		disconnectPopupOpening = true;
		var popup = await DependencyCache.PopupHandler.RequestPopupAsync(12);
		if (IsDisconnectPopupSuppressed() || DependencyCache.DominoController == null || !DependencyCache.DominoController.IsInMatch)
		{
			disconnectPopupOpening = false;
			return;
		}

		popup.SetTittle("Game Finish").SetDescription(message).AddCloseButton(delegate
		{
			disconnectPopupOpening = false;
			ServiceLocator.Instance.Get<IMainMenuView>().OpenMainButtons();
			DependencyCache.DominoController.StopDominoEarly();
		})
			.Open();
	}
}
