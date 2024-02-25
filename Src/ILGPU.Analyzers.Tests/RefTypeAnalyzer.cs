using System.IO;
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

    [Fact]
    public async Task Arrays()
    {
        var code = await File.ReadAllTextAsync("Programs/RefType.Arrays.cs");
        await VerifyCS.Verify(code);
    }

    [Fact]
    public async Task Functions()
    {
        var code = await File.ReadAllTextAsync("Programs/RefType.Functions.cs");
        await VerifyCS.Verify(code);
    }

    [Fact]
    public async Task Constructors()
    {
        var code = await File.ReadAllTextAsync("Programs/RefType.Constructors.cs");
        await VerifyCS.Verify(code);
    }
}