using System.Collections;
using UnityEngine;

public class MatchStartEffect : MonoBehaviour
{
	[Header("PAINEL")]
	public GameObject startEffectPanel;

	public CanvasGroup canvasGroup;

	[Header("TEMPO")]
	public float fadeInTime = 0.2f;

	public float stayTime = 1.2f;

	public float fadeOutTime = 0.3f;

	private void Start()
	{
		startEffectPanel.SetActive(value: false);
		canvasGroup.alpha = 0f;
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
	}

	public void ShowStartEffect()
	{
		StopAllCoroutines();
		StartCoroutine(PlayEffect());
	}

	private IEnumerator PlayEffect()
	{
		startEffectPanel.SetActive(value: true);
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
		float time = 0f;
		while (time < fadeInTime)
		{
			time += Time.deltaTime;
			float t = time / fadeInTime;
			canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
			yield return null;
		}
		canvasGroup.alpha = 1f;
		yield return new WaitForSeconds(stayTime);
		time = 0f;
		while (time < fadeOutTime)
		{
			time += Time.deltaTime;
			float t2 = time / fadeOutTime;
			canvasGroup.alpha = Mathf.Lerp(1f, 0f, t2);
			yield return null;
		}
		canvasGroup.alpha = 0f;
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
		startEffectPanel.SetActive(value: false);
	}
}
