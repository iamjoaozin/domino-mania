namespace Google
{
	internal interface ISignInImpl
	{
		Future<GoogleSignInUser> SignIn();

		Future<GoogleSignInUser> SignInSilently();

		void EnableDebugLogging(bool flag);

		void SignOut();

		void Disconnect();
	}
}
