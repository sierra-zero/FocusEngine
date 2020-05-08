// Copyright (c) Stride contributors (https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Core.BuildEngine.Tests
{
    class Program
    {
        static void Main()
        {
            //var testCancellation = new TestCancellation();
            //testCancellation.TestCancellationToken();
            //testCancellation.TestCancelCallback();
            //testCancellation.TestCancelPrerequisites();
            TestIO test = new TestIO();
            test.TestInputFromPreviousOutputWithCache();
        }
    }
}
