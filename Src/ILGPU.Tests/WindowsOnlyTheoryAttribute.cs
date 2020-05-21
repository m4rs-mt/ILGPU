using System.Runtime.InteropServices;
using Xunit;

namespace ILGPU.Tests
{
    internal class WindowsOnlyTheoryAttribute : TheoryAttribute
    {
        public WindowsOnlyTheoryAttribute(string skipReason)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = skipReason;
            }
        }
    }
}
