using System.Diagnostics;
using static nng;

const int count = 100000;

if (args.Length == 2)
{
    switch (args[0])
    {
        case "--server":
            Server(args[1]);
            break;
    }
}
else
{
    foreach (var size in new int[] { 128, 1024, 16384 })
    {
        Benchmark($"ipc:///tmp/SharpNngBenchmarks_{Guid.NewGuid():N}.ipc", size);
        Benchmark($"tcp://127.0.0.1:6001", size);
    }
}

static void Benchmark(string ipcName, int size)
{
    var benchKind = ipcName.Substring(0, ipcName.IndexOf(':'));
    Console.WriteLine($"===========================================================================================");
    Console.WriteLine($"Benchmarking {benchKind} - Data Size = {size}");

    var process = new Process();
    process.StartInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, $"--server {ipcName}")
    {
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
    };

    process.ErrorDataReceived += server_ErrorDataReceived;
    process.OutputDataReceived += server_OutputDataReceived;
    process.EnableRaisingEvents = true;
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    Thread.Sleep(1000);
    var clock = Stopwatch.StartNew();
    bool processTerminated = false;
    try
    {
        Client(ipcName, 128);
        clock.Stop();
        process.WaitForExit(1000);
        processTerminated = true;
        Console.WriteLine($"{benchKind}: {((double)count) / clock.Elapsed.TotalSeconds} request_reply/s");
        Console.WriteLine($"{benchKind}: {clock.Elapsed.TotalMilliseconds * 1000.0 / (double)count} μs/request_reply");
    }
    finally
    {
        if (!processTerminated)
        {
            process.Kill();
        }
    }
}

static void Server(string ipcName)
{
    nng_socket sock = default;

    long sizeInBytesReceived = 0;

    int result = nng_rep0_open(ref sock);
    nng_assert(result);
    try
    {
        Console.Out.WriteLine($"Server: Starting {ipcName}");

        nng_listener listener = default;
        result = nng_listen(sock, ipcName, ref listener, 0);
        nng_assert(result);

        Console.Out.WriteLine("Server: Listening");
        for (int i = 0; i < count; i++)
        {
            // Receive the buffer
            result = nng_recv(sock, out var buffer);
            nng_assert(result);
            sizeInBytesReceived += buffer.Length;
            // Send the same buffer back
            result = nng_send(sock, buffer.AsSpan());
            nng_assert(result);
            buffer.Dispose();
        }
    }
    finally
    {
        nng_close(sock);
        Console.WriteLine($"Server: Closed ({sizeInBytesReceived} bytes received)");
    }
};

static void Client(string ipcName, int size)
{
    Console.Out.WriteLine("Client: Started");

    nng_socket sock = default;

    int result = nng_req0_open(ref sock);
    nng_assert(result);
    var buffer = new byte[size];

    try
    {
        nng_dialer dialer = default;
        for (int i = 0; i < 10; i++)
        {
            result = nng_dial(sock, ipcName, ref dialer, 0);
            if (result == 0) break;
            Console.WriteLine("Client: dial failed, waiting for server to listen - sleep 100ms");
            Thread.Sleep(100);
        }

        nng_assert(result);

        Console.Out.WriteLine("Client: Connected");
        Console.Out.WriteLine("Client: Sending");
        for (int i = 0; i < count; i++)
        {
            result = nng_send(sock, buffer);
            nng_assert(result);

            result = nng_recv(sock, out var recvbuffer);
            nng_assert(result);
            if (recvbuffer.Length != buffer.Length) throw new InvalidOperationException("Size is not matching");
            recvbuffer.Dispose();
        }
    }
    finally
    {
        nng_close(sock);
        Console.WriteLine("Client: Closed");
    }
};


static void server_ErrorDataReceived(object sender, DataReceivedEventArgs e)
{
    if (e.Data == null) return;
    Console.WriteLine(e.Data);
}

static void server_OutputDataReceived(object sender, DataReceivedEventArgs e)
{
    if (e.Data == null) return;
    Console.WriteLine(e.Data);
}
