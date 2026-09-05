using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
	[Header("Animation")]
	public CanvasGroup fadeCanvas;

	public float fadeDuration = 0.5f;

	public void OpenMainMenu()
	{
		StartCoroutine(LoadSceneRoutine());
	}

	private IEnumerator LoadSceneRoutine()
	{
		float t = 0f;
		while (t < fadeDuration)
		{
			t += Time.deltaTime;
			fadeCanvas.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
			yield return null;
		}
		SceneManager.LoadScene("DominoTemplate");
	}
}
