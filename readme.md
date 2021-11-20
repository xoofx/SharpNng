# SharpNng [![Build Status](https://github.com/xoofx/SharpNng/workflows/managed/badge.svg?branch=main)](https://github.com/xoofx/SharpNng/actions) [![Build Status](https://github.com/xoofx/SharpNng/workflows/native/badge.svg?branch=main)](https://github.com/xoofx/SharpNng/actions) [![NuGet](https://img.shields.io/nuget/v/SharpNng.svg)](https://www.nuget.org/packages/SharpNng/)

<img align="right" width="160px" height="160px" src="img/logo.png">

SharpNng is a lightweight low-level managed wrapper around [NNG](https://nng.nanomsg.org/) a Lightweight Messaging Library.

> The current _native_ version of NNG used by SharpNng is `1.5.2`

## Features

- Strict mapping with the C API
- Pure DllImport library via `using static nng;`
- Compatible with `netstandard2.0` and `netstandard2.1+`
- Fast interop with `Span` friendly API.

## Usage

- Install the [SharpNng](https://www.nuget.org/packages/SharpNng/) NuGet Package to your project.

```c#
using static nng;
 // port of https://nanomsg.org/gettingstarted/nng/reqrep.html
string ipcName = $"ipc:///tmp/SharpNng_{Guid.NewGuid():N}.ipc";

var sync = new EventWaitHandle(false, EventResetMode.ManualReset);

void Node0()
{
    nng_socket sock = default;

    int result = nng_rep0_open(ref sock);
    nng_assert(result);
    try
    {
        nng_listener listener = default;
        result = nng_listen(sock, ipcName, ref listener, 0);
        nng_assert(result);

        IntPtr buf;
        size_t sz = default;

        TestContext.Out.WriteLine("Server: Listening");

        sync.Set();

        unsafe
        {
            result = nng_recv(sock, new IntPtr(&buf), ref sz, NNG_FLAG_ALLOC);
            nng_assert(result);
        }

        Assert.AreEqual(4, sz.Value.ToInt64());

        nng_free(buf, sz);
    }
    finally
    {
        result = nng_close(sock);
    }
};

void Node1()
{
    TestContext.Out.WriteLine("Client: Started");

    nng_socket sock = default;

    int result = nng_req0_open(ref sock);
    nng_assert(result);

    try
    {
        nng_dialer dialer = default;
        result = nng_dial(sock, ipcName, ref dialer, 0);
        nng_assert(result);

        TestContext.Out.WriteLine("Client: Connected");

        unsafe
        {
            int value = 0x6afedead;
            result = nng_send(sock, new IntPtr(&value), 4, 0);
            nng_assert(result);
        }
    }
    finally
    {
        result = nng_close(sock);
    }
};

// Start the server
var thread = new Thread(Node0)
{
    IsBackground = true
};
thread.Start();

// Wait for the server to start
sync.WaitOne(1000);

// Run the client
Node1();
```

## Platforms

SharpNng is supported on the following platforms:

- `win-x64`, `win-x86`, `win-arm64`, `win-arm`
- `linux-x64`, `linux-arm64`, `linux-arm`
- `osx-x64`, `osx-arm64`

> Note that the Linux version might probably only work on debian derivatives for now...

## How to Build?

You need to install the [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0). Then from the root folder:

```console
$ dotnet build src -c Release
```

In order to rebuild the native binaries, you need to run the build scripts from [ext](ext/readme.md)

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](https://xoofx.com).
