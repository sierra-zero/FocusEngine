// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Stride.Core.Diagnostics;
using Stride.FixProjectReferences;

namespace Stride.Code.Tests
{
    /// <summary>
    /// Test class that check if there is some copy-local references between Stride projects.
    /// </summary>
    public class FixProjectReferenceTests
    {
        [Fact]
        public void TestCopyLocals()
        {
            var log = new LoggerResult();
            log.ActivateLog(LogMessageType.Error);
            Assert.True(FixProjectReference.ProcessCopyLocals(log, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\build\Stride.sln"), false),
                $"Found some dependencies between Stride projects that are not set to CopyLocal=false; please run Stride.FixProjectReferences:\r\n{log.ToText()}");
        }
    }
}
