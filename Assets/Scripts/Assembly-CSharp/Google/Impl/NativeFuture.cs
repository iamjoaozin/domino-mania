using System;
using System.Runtime.InteropServices;

namespace Google.Impl
{
	internal class NativeFuture : BaseObject, FutureAPIImpl<GoogleSignInUser>
	{
		public bool Pending
		{
			get
			{
				if (!HasValidSelfPtr())
				{
					return false;
				}
				return GoogleSignInImpl.GoogleSignIn_Pending(SelfPtr());
			}
		}

		public GoogleSignInUser Result
		{
			get
			{
				if (!HasValidSelfPtr())
				{
					return null;
				}
				IntPtr intPtr = GoogleSignInImpl.GoogleSignIn_Result(SelfPtr());
				if (intPtr != IntPtr.Zero)
				{
					GoogleSignInUser googleSignInUser = new GoogleSignInUser();
					HandleRef userPtr = new HandleRef(googleSignInUser, intPtr);
					googleSignInUser.DisplayName = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetDisplayName(userPtr, out_string, out_size));
					googleSignInUser.Email = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetEmail(userPtr, out_string, out_size));
					googleSignInUser.FamilyName = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetFamilyName(userPtr, out_string, out_size));
					googleSignInUser.GivenName = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetGivenName(userPtr, out_string, out_size));
					googleSignInUser.IdToken = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetIdToken(userPtr, out_string, out_size));
					googleSignInUser.AuthCode = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetServerAuthCode(userPtr, out_string, out_size));
					string text = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetImageUrl(userPtr, out_string, out_size));
					if (text.Length > 0)
					{
						googleSignInUser.ImageUrl = new Uri(text);
					}
					googleSignInUser.UserId = BaseObject.OutParamsToString((byte[] out_string, UIntPtr out_size) => GoogleSignInImpl.GoogleSignIn_GetUserId(userPtr, out_string, out_size));
					return googleSignInUser;
				}
				return null;
			}
		}

		public GoogleSignInStatusCode Status
		{
			get
			{
				if (!HasValidSelfPtr())
				{
					return GoogleSignInStatusCode.DeveloperError;
				}
				return (GoogleSignInStatusCode)GoogleSignInImpl.GoogleSignIn_Status(SelfPtr());
			}
		}

		internal NativeFuture(IntPtr ptr)
			: base(ptr)
		{
		}

		public override void Dispose()
		{
			if (!HasValidSelfPtr())
			{
				base.Dispose();
				return;
			}
			GoogleSignInImpl.GoogleSignIn_DisposeFuture(SelfPtr());
			base.Dispose();
		}
	}
}
