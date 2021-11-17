using System.Diagnostics;
using static nng;

string ipcName;
const int count = 100000;

void Server()
{
    nng_socket sock = default;

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
            IntPtr buf;
            size_t sz = default;
            unsafe
            {
                result = nng_recv(sock, new IntPtr(&buf), ref sz, NNG_FLAG_ALLOC);
                nng_assert(result);
            }
            nng_free(buf, sz);
        }
    }
    finally
    {
        nng_close(sock);
        Console.WriteLine("Server: Closed");
    }
};

void Client()
{
    Console.Out.WriteLine("Client: Started");

    nng_socket sock = default;

    int result = nng_req0_open(ref sock);
    nng_assert(result);

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
            unsafe
            {
                int value = 0x6afedead;
                result = nng_send(sock, new IntPtr(&value), 4, 0);
                nng_assert(result);
            }
        }
    }
    finally
    {
        nng_close(sock);
        Console.WriteLine("Client: Closed");
    }
};


if (args.Length == 2)
{
    switch (args[0])
    {
        case "--server":
            ipcName = args[1];
            Server();
            break;
    }
}
else
{
    ipcName = $"ipc:///tmp/SharpNngBenchmarks_{Guid.NewGuid():N}.ipc";

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
        Client();
        clock.Stop();
        process.WaitForExit(1000);
        processTerminated = true;
        Console.WriteLine($"{((double)count) / clock.Elapsed.TotalSeconds} req/s");
        Console.WriteLine($"{clock.Elapsed.TotalMilliseconds * 1000.0 / (double)count} μs/req");
    }
    finally
    {
        if (!processTerminated)
        {
            process.Kill();
        }
    }
}


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
