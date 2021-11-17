// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using System;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable InconsistentNaming
public static partial class nng
{
    public const string nngDll = "nng_native";

    public partial struct size_t
    {
        public static implicit operator size_t(int value)
        {
            return new size_t(new IntPtr(value));
        }
    }

    /// <summary>
    /// nng_aio_alloc allocates a new AIO, and associated the completion
    /// callback and its opaque argument.  If NULL is supplied for the
    /// callback, then the caller must use nng_aio_wait() to wait for the
    /// operation to complete.  If the completion callback is not NULL, then
    /// when a submitted operation completes (or is canceled or fails) the
    /// callback will be executed, generally in a different thread, with no
    /// locks held.
    /// </summary>
    [DllImport(nngDll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_aio_alloc(out nng_aio arg0, IntPtr arg1, IntPtr arg2);

    /// <summary>
    /// Throws a <see cref="NngException"/> if the result is non zero.
    /// </summary>
    /// <param name="result">Result from the nng function</param>
    public static void nng_assert(int result)
    {
        if (result == 0) return;
        throw NngException.Create((nng_errno_enum)result);
    }

    /// <summary>
    /// Exception wrapping error code nng_errno_enum
    /// </summary>
    public class NngException : Exception
    {
        private NngException(nng_errno_enum result, string message) : base(message)
        {
            Value = result;
        }

        public nng_errno_enum Value { get; }

        public static NngException Create(nng_errno_enum result)
        {
            var errorString = nng_strerror((int)result);
            return new NngException(result, $"{errorString} ({result} = {(int)result})");
        }
    }

    /// <summary>
    /// Marshaller used for returning string from nng
    /// </summary>
    private class ReturnUtf8StringMarshaller : ICustomMarshaler
    {
        private static readonly ReturnUtf8StringMarshaller Instance = new();

        public static ICustomMarshaler GetInstance(string s)
        {
            return Instance;
        }

        public void CleanUpManagedData(object o)
        {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
        }

        public int GetNativeDataSize()
        {
            return IntPtr.Size;
        }

        public unsafe IntPtr MarshalManagedToNative(object obj)
        {
            throw new NotSupportedException();
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            unsafe
            {
                int length = 0;
                byte* pNativeDataPtr = (byte*)pNativeData;
                while (*pNativeDataPtr != 0)
                {
                    length++;
                    pNativeDataPtr++;
                }

                return Encoding.UTF8.GetString((byte*)pNativeData, length);
            }
        }
    }

    /// <summary>
    /// Marshaller used for returning string from nng
    /// </summary>
    private class FastUtf8StringMarshaller : ICustomMarshaler
    {
        private static readonly FastUtf8StringMarshaller Instance = new();

        public static ICustomMarshaler GetInstance(string s)
        {
            return Instance;
        }

        public void CleanUpManagedData(object o)
        {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            Marshal.FreeHGlobal(pNativeData);
        }

        public int GetNativeDataSize()
        {
            return IntPtr.Size;
        }

        public unsafe IntPtr MarshalManagedToNative(object obj)
        {
            if (obj == null) return IntPtr.Zero;

            var str = (string)obj;
            var length = Encoding.UTF8.GetByteCount(str);
            var ptr = Marshal.AllocHGlobal(length + 1);
            //Encoding.UTF8.GetBytes((string)obj, 0, 
            fixed (char* pStr = str)
                Encoding.UTF8.GetEncoder().GetBytes(pStr,  str.Length, (byte*)ptr, length, true);
            return ptr;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            throw new NotSupportedException();
        }
    }
}
