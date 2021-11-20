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

    /// <summary>
    /// nng_send sends (or arranges to send) the data on the socket.  Note that
    /// this function may (will!) return before any receiver has actually
    /// received the data.  The return value will be zero to indicate that the
    /// socket has accepted the entire data for send, or an errno to indicate
    /// failure.
    /// </summary>
    public static unsafe int nng_send(nng.nng_socket arg0, ReadOnlySpan<byte> arg2, bool nonBlocking = false)
    {
        fixed (byte* arg4 = &MemoryMarshal.GetReference(arg2))
        {
            return nng_send(arg0, (IntPtr)arg4, arg2.Length, nonBlocking ? NNG_FLAG_NONBLOCK : 0);
        }
    }

    /// <summary>
    /// nng_recv receives message data into the socket, up to the supplied size.
    /// The actual size of the message data will be written to the value pointed
    /// to by size.  The flags may include NNG_FLAG_NONBLOCK and NNG_FLAG_ALLOC.
    /// If NNG_FLAG_ALLOC is supplied then the library will allocate memory for
    /// the caller.  In that case the pointer to the allocated will be stored
    /// instead of the data itself.  The caller is responsible for freeing the
    /// associated memory with nng_free().
    /// </summary>
    public static unsafe int nng_recv(nng.nng_socket arg0, out NngBuffer buffer, bool nonBlocking = false)
    {
        IntPtr pBuffer = default;
        size_t size = default;
        var result = nng_recv(arg0, new IntPtr(&pBuffer), ref size, (nonBlocking ? NNG_FLAG_NONBLOCK : 0) | NNG_FLAG_ALLOC);
        buffer = result == 0 ? new NngBuffer(pBuffer, size.Value.ToInt32()) : default;
        return result;
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
    public static extern unsafe int nng_aio_alloc(out nng_aio arg0, delegate* unmanaged[Cdecl]<IntPtr, void> callback, IntPtr callbackContext = default);

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
    /// Wrapper around a memory buffer allocated by <see cref="nng.nng_alloc"/>.
    /// </summary>
    public readonly struct NngBuffer : IDisposable
    {
        public NngBuffer(IntPtr pointer, int length)
        {
            if (pointer == IntPtr.Zero) throw new ArgumentException("pointer cannot be null", nameof(pointer));
            if (length < 0) throw new ArgumentOutOfRangeException($"length ({length}) be < 0", nameof(length));
            Pointer = pointer;
            Length = length;
        }

        public NngBuffer(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            Pointer = nng_alloc(length);
            if (Pointer == IntPtr.Zero) throw new InvalidOperationException($"Unable to allocate {nameof(NngBuffer)} of size {length} bytes");
            Length = length;
        }

        public readonly IntPtr Pointer;

        public readonly int Length;

        /// <summary>
        /// Calls <see cref="nng.nng_free"/>
        /// </summary>
        public void Dispose()
        {
            nng_free(Pointer, Length);
        }

        public unsafe Span<byte> AsSpan()
        {
            return new Span<byte>((void*)Pointer, Length);
        }

        public override string ToString()
        {
            return $"{nameof(Pointer)}: 0x{Pointer.ToString("x8")}, {nameof(Length)}: {Length}";
        }
    }

    public partial struct size_t
    {
        public static implicit operator size_t(int value)
        {
            return new size_t(new IntPtr(value));
        }
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
            var ptr = (byte*)Marshal.AllocHGlobal(length + 1);
            //Encoding.UTF8.GetBytes((string)obj, 0, 
            fixed (char* pStr = str)
                Encoding.UTF8.GetEncoder().GetBytes(pStr,  str.Length, ptr, length, true);
            ptr[length] = 0;
            return (IntPtr)ptr;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            throw new NotSupportedException();
        }
    }
}
