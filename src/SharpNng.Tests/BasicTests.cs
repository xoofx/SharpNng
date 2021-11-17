// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using System;
using System.Threading;
using NUnit.Framework;

using static nng;

namespace SharpNng.Tests
{
    public class BasicTests
    {
        [Test]
        public void CheckVersion()
        {
            var version = nng_version();
            Assert.NotNull(version);
            StringAssert.StartsWith("1.", version);
        }

        [Test]
        public void TestRequestReply()
        {
            // https://nanomsg.org/gettingstarted/nng/reqrep.html
            string IpcName = $"ipc:///TestRequestReply_{Guid.NewGuid():N}.ipc";

            var sync = new EventWaitHandle(false, EventResetMode.ManualReset);

            void Node0()
            {
                nng_socket sock = default;

                int result = nng_rep0_open(ref sock);
                nng_assert(result);
                try
                {
                    nng_listener listener = default;
                    result = nng_listen(sock, IpcName, ref listener, 0);
                    nng_assert(result);

                    IntPtr buf;
                    size_t sz = default;

                    sync.Set();

                    TestContext.Out.WriteLine("Server: Listening");

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
                    nng_close(sock);
                }
            };

            void Node1()
            {
                TestContext.Out.WriteLine("Client: Started");

                nng_socket sock = default;

                int result = nng_req0_open(ref sock);
                nng_assert(result);


                    nng_dialer dialer = default;
                    for (int i = 0; i < 10; i++)
                    {
                        result = nng_dial(sock, IpcName, ref dialer, 0);
                        if (result == 0) break;
                        TestContext.Out.WriteLine("Client: dial failed, waiting for server to listen - sleep 100ms");
                        Thread.Sleep(500);
                    }

                    nng_assert(result);

                try
                {
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
                    nng_close(sock);
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
        }
    }
}