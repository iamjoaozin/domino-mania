namespace Google
{
	internal interface FutureAPIImpl<T>
	{
		bool Pending { get; }

		GoogleSignInStatusCode Status { get; }

		T Result { get; }
	}
}
