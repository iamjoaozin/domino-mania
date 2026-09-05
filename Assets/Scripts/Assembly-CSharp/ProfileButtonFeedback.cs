using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ProfileButtonFeedback : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IPointerExitHandler
{
	private Image targetImage;

	private Vector3 targetScale = Vector3.one;

	private float seed;

	private bool pressed;

	public void Initialize(Image image)
	{
		targetImage = image;
		seed = Random.Range(0f, 8f);
	}

	private void Update()
	{
		float num = (pressed ? 0f : (Mathf.Sin((Time.unscaledTime + seed) * 2f) * 0.01f));
		base.transform.localScale = Vector3.Lerp(base.transform.localScale, targetScale * (1f + num), Time.unscaledDeltaTime * 12f);
		if (targetImage != null && !pressed)
		{
			float num2 = 0.94f + Mathf.Sin((Time.unscaledTime + seed) * 1.9f) * 0.06f;
			targetImage.color = new Color(num2, num2, num2, 1f);
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		pressed = true;
		targetScale = Vector3.one * 0.965f;
		if (targetImage != null)
		{
			targetImage.color = new Color(1f, 0.86f, 1f, 1f);
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		pressed = false;
		targetScale = Vector3.one;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		pressed = false;
		targetScale = Vector3.one;
	}
}
