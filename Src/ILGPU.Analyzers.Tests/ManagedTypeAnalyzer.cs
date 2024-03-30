using System.IO;
using System.Threading.Tasks;
using Xunit;
using VerifyCS =
    ILGPU.Analyzers.Tests.Generic.DiagnosticAnalyzerVerifier<
        ILGPU.Analyzers.ManagedTypeAnalyzer>;

namespace ILGPU.Analyzers.Tests;

public class ManagedTypeAnalyzer
{
    [Theory]
    [InlineData("Simple")]
    [InlineData("Arrays")]
    [InlineData("Functions")]
    [InlineData("Constructors")]
    [InlineData("ManagedUnmanaged")]
    [InlineData("LoadDiscovery")]
    public async Task FileTests(string file)
    {
        // In build, we copy all programs to output directory. See ILGPU.Analyzers.Tests.csproj
        var code = await File.ReadAllTextAsync($"Programs/RefType/{file}.cs");
        await VerifyCS.Verify(code, settings => settings.UseParameters(file));
    }
}