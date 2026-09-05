using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Google.Impl;

namespace Google
{
	public class GoogleSignIn
	{
		[Serializable]
		public class SignInException : Exception
		{
			public GoogleSignInStatusCode Status { get; internal set; }

			internal SignInException(GoogleSignInStatusCode status)
			{
				Status = status;
			}

			public SignInException(GoogleSignInStatusCode status, string message)
				: base(message)
			{
				Status = status;
			}

			public SignInException(GoogleSignInStatusCode status, string message, Exception innerException)
				: base(message, innerException)
			{
				Status = status;
			}

			protected SignInException(GoogleSignInStatusCode status, SerializationInfo info, StreamingContext context)
				: base(info, context)
			{
				Status = status;
			}
		}

		private static GoogleSignIn theInstance;

		private static GoogleSignInConfiguration theConfiguration;

		private ISignInImpl impl;

		public static GoogleSignInConfiguration Configuration
		{
			get
			{
				return theConfiguration;
			}
			set
			{
				if (theInstance == null || theConfiguration == value || theConfiguration == null)
				{
					theConfiguration = value;
					return;
				}
				throw new SignInException(GoogleSignInStatusCode.DeveloperError, "DefaultInstance already created.  Cannot change configuration after creation.");
			}
		}

		public static GoogleSignIn DefaultInstance
		{
			get
			{
				if (theInstance == null)
				{
					theInstance = new GoogleSignIn(new GoogleSignInImpl(Configuration));
				}
				return theInstance;
			}
		}

		internal GoogleSignIn(GoogleSignInImpl impl)
		{
			this.impl = impl;
		}

		public void EnableDebugLogging(bool flag)
		{
			impl.EnableDebugLogging(flag);
		}

		public Task<GoogleSignInUser> SignIn()
		{
			TaskCompletionSource<GoogleSignInUser> taskCompletionSource = new TaskCompletionSource<GoogleSignInUser>();
			SignInHelperObject.Instance.StartCoroutine(impl.SignIn().WaitForResult(taskCompletionSource));
			return taskCompletionSource.Task;
		}

		public Task<GoogleSignInUser> SignInSilently()
		{
			TaskCompletionSource<GoogleSignInUser> taskCompletionSource = new TaskCompletionSource<GoogleSignInUser>();
			SignInHelperObject.Instance.StartCoroutine(impl.SignInSilently().WaitForResult(taskCompletionSource));
			return taskCompletionSource.Task;
		}

		public void SignOut()
		{
			theConfiguration = null;
			impl.SignOut();
		}

		public void Disconnect()
		{
			impl.Disconnect();
		}
	}
}
