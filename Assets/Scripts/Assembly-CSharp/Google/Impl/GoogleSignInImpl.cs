using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Google.Impl
{
	internal class GoogleSignInImpl : BaseObject, ISignInImpl
	{
		private const string DllName = "native-googlesignin";

		internal GoogleSignInImpl(GoogleSignInConfiguration configuration)
			: base(GoogleSignIn_Create(GetPlayerActivity()))
		{
			if (configuration != null)
			{
				List<string> list = new List<string>();
				if (configuration.AdditionalScopes != null)
				{
					list.AddRange(configuration.AdditionalScopes);
				}
				GoogleSignIn_Configure(SelfPtr(), configuration.UseGameSignIn, configuration.WebClientId, configuration.RequestAuthCode, configuration.ForceTokenRefresh, configuration.RequestEmail, configuration.RequestIdToken, configuration.HidePopups, list.ToArray(), list.Count, configuration.AccountName);
			}
		}

		public void EnableDebugLogging(bool flag)
		{
			GoogleSignIn_EnableDebugLogging(SelfPtr(), flag);
		}

		public Future<GoogleSignInUser> SignIn()
		{
			IntPtr ptr = GoogleSignIn_SignIn(SelfPtr());
			return new Future<GoogleSignInUser>(new NativeFuture(ptr));
		}

		public Future<GoogleSignInUser> SignInSilently()
		{
			IntPtr ptr = GoogleSignIn_SignInSilently(SelfPtr());
			return new Future<GoogleSignInUser>(new NativeFuture(ptr));
		}

		public void SignOut()
		{
			GoogleSignIn_Signout(SelfPtr());
		}

		public void Disconnect()
		{
			GoogleSignIn_Disconnect(SelfPtr());
		}

		[DllImport("native-googlesignin")]
		private static extern IntPtr GoogleSignIn_Create(IntPtr data);

		[DllImport("native-googlesignin")]
		private static extern void GoogleSignIn_EnableDebugLogging(HandleRef self, bool flag);

		[DllImport("native-googlesignin")]
		private static extern bool GoogleSignIn_Configure(HandleRef self, bool useGameSignIn, string webClientId, bool requestAuthCode, bool forceTokenRefresh, bool requestEmail, bool requestIdToken, bool hidePopups, string[] additionalScopes, int scopeCount, string accountName);

		[DllImport("native-googlesignin")]
		private static extern IntPtr GoogleSignIn_SignIn(HandleRef self);

		[DllImport("native-googlesignin")]
		private static extern IntPtr GoogleSignIn_SignInSilently(HandleRef self);

		[DllImport("native-googlesignin")]
		private static extern void GoogleSignIn_Signout(HandleRef self);

		[DllImport("native-googlesignin")]
		private static extern void GoogleSignIn_Disconnect(HandleRef self);

		[DllImport("native-googlesignin")]
		internal static extern void GoogleSignIn_DisposeFuture(HandleRef self);

		[DllImport("native-googlesignin")]
		internal static extern bool GoogleSignIn_Pending(HandleRef self);

		[DllImport("native-googlesignin")]
		internal static extern IntPtr GoogleSignIn_Result(HandleRef self);

		[DllImport("native-googlesignin")]
		internal static extern int GoogleSignIn_Status(HandleRef self);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetServerAuthCode(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetDisplayName(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetEmail(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetFamilyName(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetGivenName(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetIdToken(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetImageUrl(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		[DllImport("native-googlesignin")]
		internal static extern UIntPtr GoogleSignIn_GetUserId(HandleRef self, [In][Out] byte[] bytes, UIntPtr len);

		private static IntPtr GetPlayerActivity()
		{
			AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			return androidJavaClass.GetStatic<AndroidJavaObject>("currentActivity").GetRawObject();
		}
	}
}
