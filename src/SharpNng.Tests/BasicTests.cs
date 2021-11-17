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

            const string IpcName = "ipc:///tmp/reqrep.ipc";

            var sync = new EventWaitHandle(false, EventResetMode.ManualReset);

            void Node0()
            {
                nng_socket sock = default;

                int result = nng_rep0_open(ref sock);
                nng_assert(result);

                nng_listener listener = default;
                result = nng_listen(sock, IpcName, ref listener, 0);
                nng_assert(result);

                IntPtr buf;
                size_t sz = default;

                sync.Set();

                unsafe
                {
                    result = nng_recv(sock, new IntPtr(&buf), ref sz, NNG_FLAG_ALLOC);
                    nng_assert(result);
                }

                Assert.AreEqual(4, sz.Value.ToInt64());

                nng_free(buf, sz);

                result = nng_close(sock);
                nng_assert(result);
            };

            void Node1()
            {
                nng_socket sock = default;

                int result = nng_req0_open(ref sock);
                nng_assert(result);

                nng_dialer dialer = default;
                result = nng_dial(sock, IpcName, ref dialer, 0);
                nng_assert(result);

                unsafe
                {
                    int value = 0x6afedead;
                    result = nng_send(sock, new IntPtr(&value), 4, 0);
                    nng_assert(result);
                }
                result = nng_close(sock);
                nng_assert(result);
            };

            // Start the server
            var thread = new Thread(Node0)
            {
                IsBackground = true
            };
            thread.Start();

            // Wait for the server to start
            sync.WaitOne(2000);

            // Run the client
            Node1();
        }
    }
}