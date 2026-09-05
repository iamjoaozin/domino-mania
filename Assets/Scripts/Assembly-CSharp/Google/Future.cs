using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Google
{
	public class Future<T>
	{
		private FutureAPIImpl<T> apiImpl;

		public bool Pending => apiImpl.Pending;

		private GoogleSignInStatusCode Status => apiImpl.Status;

		private T Result => apiImpl.Result;

		internal Future(FutureAPIImpl<T> impl)
		{
			apiImpl = impl;
		}

		internal IEnumerator WaitForResult(TaskCompletionSource<T> tcs)
		{
			yield return new WaitUntil(() => !Pending);
			if (Status == GoogleSignInStatusCode.Canceled)
			{
				tcs.SetCanceled();
			}
			else if (Status == GoogleSignInStatusCode.Success || Status == GoogleSignInStatusCode.SuccessCached)
			{
				tcs.SetResult(Result);
			}
			else
			{
				tcs.SetException(new GoogleSignIn.SignInException(Status));
			}
		}
	}
}
