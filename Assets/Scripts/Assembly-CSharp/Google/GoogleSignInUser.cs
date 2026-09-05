using System;

namespace Google
{
	public class GoogleSignInUser
	{
		public string AuthCode { get; internal set; }

		public string Email { get; internal set; }

		public string IdToken { get; internal set; }

		public string DisplayName { get; internal set; }

		public string GivenName { get; internal set; }

		public string FamilyName { get; internal set; }

		public Uri ImageUrl { get; internal set; }

		public string UserId { get; internal set; }
	}
}
