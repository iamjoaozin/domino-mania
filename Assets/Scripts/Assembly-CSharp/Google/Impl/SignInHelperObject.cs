using UnityEngine;

namespace Google.Impl
{
	public class SignInHelperObject : MonoBehaviour
	{
		private static SignInHelperObject instance;

		internal static SignInHelperObject Instance
		{
			get
			{
				if (Application.isPlaying)
				{
					GameObject gameObject = new GameObject("GoogleSignInHelperObject");
					Object.DontDestroyOnLoad(gameObject);
					instance = gameObject.AddComponent<SignInHelperObject>();
				}
				else
				{
					instance = new SignInHelperObject();
				}
				return instance;
			}
		}
	}
}
