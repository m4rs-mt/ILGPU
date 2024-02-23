using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using VerifyCS =
    ILGPU.Analyzers.Tests.Generic.DiagnosticAnalyzerVerifier<
        ILGPU.Analyzers.ReferenceTypeAnalyzer>;

namespace ILGPU.Analyzers.Tests;

public class RefTypeAnalyzer
{
    [Fact]
    public async Task Simple()
    {
        // In build, we copy all programs to output directory. See ILGPU.Analyzers.Tests.csproj
        var code = await File.ReadAllTextAsync("Programs/RefType.Simple.cs");
        await VerifyCS.Verify(code);
    }
}