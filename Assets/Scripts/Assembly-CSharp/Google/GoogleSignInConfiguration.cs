using System.Collections.Generic;

namespace Google
{
	public class GoogleSignInConfiguration
	{
		public bool UseGameSignIn = false;

		public string WebClientId = null;

		public bool RequestAuthCode = false;

		public bool ForceTokenRefresh = false;

		public bool RequestEmail = false;

		public bool RequestIdToken = false;

		public bool RequestProfile = false;

		public bool HidePopups = false;

		public string AccountName = null;

		public IEnumerable<string> AdditionalScopes = null;
	}
}
