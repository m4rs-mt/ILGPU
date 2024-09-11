using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ILGPUwebCompiler
{
    internal class Compiler
    {
        private List<MetadataReference> references { get; set; }
        public void Init()
        {
            if (references == null)
            {
                references = new List<MetadataReference>();
                for (int i = 0; i < Service.GetAmmountOfReferences(); i++)
                {
                    var file = Service.GetReference(i);
                    using var wri = new BinaryWriter(System.IO.File.OpenWrite(i + ".dll"));
                    wri.Write(file);
                    var reference = MetadataReference.CreateFromFile(i + ".dll");
                    references.Add(reference);
                }
            }


        }

        private Assembly Compile(string code)
        {
            CSharpCompilation compilation = CSharpCompilation.Create("DynamicCode")
                .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
                .AddReferences(references)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview)));

            var diagnostic = compilation.GetDiagnostics();
            bool error = false;
            foreach (Diagnostic diag in diagnostic)
            {
                switch (diag.Severity)
                {
                    case DiagnosticSeverity.Info:
                        Console.WriteLine(diag.ToString());
                        break;
                    case DiagnosticSeverity.Warning:
                        Console.WriteLine(diag.ToString());
                        break;
                    case DiagnosticSeverity.Error:
                        error = true;
                        Console.WriteLine(diag.ToString());
                        break;
                }
            }
            if (error)
            {
                return null;
            }


            var outputAssembly = new MemoryStream();
            var res = compilation.Emit(outputAssembly);

            return Assembly.Load(outputAssembly.ToArray());

        }

        public async Task<string> CompileAndRun(string code)
        {
            //redirecting console output on a stream writer
            var writer = new StringWriter();
            Console.SetOut(writer);
            string output = "";
            Init();

            var currentOut = Console.Out;

            var sw = Stopwatch.StartNew(); //recording time
            string exception = "";
            try
            {
                var assembly = this.Compile(code);
                if (assembly != null)
                {
                    var entry = assembly.EntryPoint;
                    if (entry.Name == "<Main>")
                    {
                        entry = entry.DeclaringType.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); // reflect for the async Task Main
                    }
                    var hasArgs = entry.GetParameters().Length > 0;
                    var result = entry.Invoke(null, hasArgs ? new object[] { new string[0] } : null);
                    if (result is Task t)
                    {
                        await t;
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex.ToString();
            }
            output = writer.ToString();
            output += "\r\n" + exception;
            sw.Stop();
            output += "Done in " + sw.ElapsedMilliseconds + "ms";

            Console.SetOut(currentOut);
            return output;
        }
    }
}
