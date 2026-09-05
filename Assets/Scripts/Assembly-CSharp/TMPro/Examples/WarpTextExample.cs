using System.Collections;
using UnityEngine;

namespace TMPro.Examples
{
	public class WarpTextExample : MonoBehaviour
	{
		private TMP_Text m_TextComponent;

		public AnimationCurve VertexCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 2f), new Keyframe(0.5f, 0f), new Keyframe(0.75f, 2f), new Keyframe(1f, 0f));

		public float AngleMultiplier = 1f;

		public float SpeedMultiplier = 1f;

		public float CurveScale = 1f;

		private void Awake()
		{
			m_TextComponent = base.gameObject.GetComponent<TMP_Text>();
		}

		private void Start()
		{
			StartCoroutine(WarpText());
		}

		private AnimationCurve CopyAnimationCurve(AnimationCurve curve)
		{
			AnimationCurve animationCurve = new AnimationCurve();
			animationCurve.keys = curve.keys;
			return animationCurve;
		}

		private IEnumerator WarpText()
		{
			VertexCurve.preWrapMode = WrapMode.Once;
			VertexCurve.postWrapMode = WrapMode.Once;
			m_TextComponent.havePropertiesChanged = true;
			CurveScale *= 10f;
			float old_CurveScale = CurveScale;
			AnimationCurve old_curve = CopyAnimationCurve(VertexCurve);
			while (true)
			{
				if (!m_TextComponent.havePropertiesChanged && old_CurveScale == CurveScale && old_curve.keys[1].value == VertexCurve.keys[1].value)
				{
					yield return null;
					continue;
				}
				old_CurveScale = CurveScale;
				old_curve = CopyAnimationCurve(VertexCurve);
				m_TextComponent.ForceMeshUpdate();
				TMP_TextInfo textInfo = m_TextComponent.textInfo;
				int characterCount = textInfo.characterCount;
				if (characterCount == 0)
				{
					continue;
				}
				float boundsMinX = m_TextComponent.bounds.min.x;
				float boundsMaxX = m_TextComponent.bounds.max.x;
				for (int i = 0; i < characterCount; i++)
				{
					if (textInfo.characterInfo[i].isVisible)
					{
						int vertexIndex = textInfo.characterInfo[i].vertexIndex;
						int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
						Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
						Vector3 offsetToMidBaseline = new Vector2((vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f, textInfo.characterInfo[i].baseLine);
						vertices[vertexIndex] += -offsetToMidBaseline;
						vertices[vertexIndex + 1] += -offsetToMidBaseline;
						vertices[vertexIndex + 2] += -offsetToMidBaseline;
						vertices[vertexIndex + 3] += -offsetToMidBaseline;
						float x0 = (offsetToMidBaseline.x - boundsMinX) / (boundsMaxX - boundsMinX);
						float x1 = x0 + 0.0001f;
						float y0 = VertexCurve.Evaluate(x0) * CurveScale;
						float y1 = VertexCurve.Evaluate(x1) * CurveScale;
						Vector3 horizontal = new Vector3(1f, 0f, 0f);
						Vector3 tangent = new Vector3(x1 * (boundsMaxX - boundsMinX) + boundsMinX, y1) - new Vector3(offsetToMidBaseline.x, y0);
						float dot = Mathf.Acos(Vector3.Dot(horizontal, tangent.normalized)) * 57.29578f;
						Matrix4x4 matrix = Matrix4x4.TRS(q: Quaternion.Euler(0f, 0f, (Vector3.Cross(horizontal, tangent).z > 0f) ? dot : (360f - dot)), pos: new Vector3(0f, y0, 0f), s: Vector3.one);
						vertices[vertexIndex] = matrix.MultiplyPoint3x4(vertices[vertexIndex]);
						vertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);
						vertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);
						vertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);
						vertices[vertexIndex] += offsetToMidBaseline;
						vertices[vertexIndex + 1] += offsetToMidBaseline;
						vertices[vertexIndex + 2] += offsetToMidBaseline;
						vertices[vertexIndex + 3] += offsetToMidBaseline;
					}
				}
				m_TextComponent.UpdateVertexData();
				yield return new WaitForSeconds(0.025f);
			}
		}
	}
}
