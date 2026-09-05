using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Google.Impl
{
	internal abstract class BaseObject : IDisposable
	{
		internal delegate UIntPtr OutStringMethod([In][Out] byte[] out_bytes, UIntPtr out_size);

		private HandleRef selfHandleRef;

		private static HandleRef nullSelf = default(HandleRef);

		public BaseObject(IntPtr intPtr)
		{
			selfHandleRef = new HandleRef(this, intPtr);
		}

		protected HandleRef SelfPtr()
		{
			if (selfHandleRef.Equals(nullSelf))
			{
				throw new InvalidOperationException("Attempted to use object after it was cleaned up");
			}
			return selfHandleRef;
		}

		protected bool HasValidSelfPtr()
		{
			return !selfHandleRef.Equals(nullSelf) && selfHandleRef.Handle != IntPtr.Zero;
		}

		public virtual void Dispose()
		{
			selfHandleRef = nullSelf;
		}

		internal static string OutParamsToString(OutStringMethod outStringMethod)
		{
			UIntPtr out_size = outStringMethod(null, UIntPtr.Zero);
			if (out_size.Equals(UIntPtr.Zero))
			{
				return null;
			}
			string text = null;
			try
			{
				byte[] array = new byte[out_size.ToUInt32()];
				outStringMethod(array, out_size);
				text = Encoding.UTF8.GetString(array, 0, (int)(out_size.ToUInt32() - 1));
			}
			catch (Exception ex)
			{
				Debug.LogError("Exception creating string from char array: " + ex);
				text = string.Empty;
			}
			return text;
		}
	}
}
