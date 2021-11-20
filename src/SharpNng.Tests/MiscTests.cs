// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;

using static nng;

namespace SharpNng.Tests
{
    public class MiscTests
    {
        [Test]
        public void CheckVersion()
        {
            var version = nng_version();
            Assert.NotNull(version);
            StringAssert.StartsWith("1.", version);
        }
    }
}