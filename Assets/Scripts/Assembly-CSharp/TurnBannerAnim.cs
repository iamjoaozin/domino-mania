using DG.Tweening;
using UnityEngine;

public class TurnBannerAnim : MonoBehaviour
{
	public CanvasGroup canvasGroup;

	private void OnEnable()
	{
		PrepareNonBlocking();
		PlayAnim();
	}

	public void PlayAnim()
	{
		PrepareNonBlocking();
		base.transform.localScale = Vector3.zero;
		canvasGroup.alpha = 0f;
		Sequence sequence = DOTween.Sequence();
		sequence.Append(base.transform.DOScale(1.15f, 0.25f).SetEase(Ease.OutBack));
		sequence.Join(canvasGroup.DOFade(1f, 0.2f));
		sequence.Append(base.transform.DOScale(1f, 0.15f));
		sequence.Append(base.transform.DOScale(1.05f, 0.5f).SetLoops(2, LoopType.Yoyo));
		sequence.AppendInterval(0.6f);
		sequence.Append(canvasGroup.DOFade(0f, 0.3f));
		sequence.Join(base.transform.DOScale(0.8f, 0.3f));
		sequence.OnComplete(delegate
		{
			PrepareNonBlocking();
			base.gameObject.SetActive(value: false);
		});
	}

	private void PrepareNonBlocking()
	{
		if (!(canvasGroup == null))
		{
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}
	}
}
