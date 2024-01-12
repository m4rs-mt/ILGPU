// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;
using System.Reflection;
using System.Text;
using System.Xml;

namespace CudaGenerateLibDeviceTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Configure NVVM and LibDevice.
            PTXLibDevice.FindLibDevicePaths(
                out _,
                out _,
                out var libNvvmPath,
                out _,
                out var libDevicePath);

            using var nvvmAPI = NvvmAPI.Create(
                libNvvmPath!,
                libDevicePath!);

            // Generate the PTX for each of the LibDevice methods.
            var filePath = Path.Combine(GetDefaultFolder(), "CudaLibDevicePtx.xml");
            using var doc = XmlWriter.Create(
                filePath,
                new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = Encoding.UTF8
                });
            doc.WriteStartElement("LibDevicePtx");

            var methods = LoadMethodNames();

            foreach (var method in methods)
            {
                PTXLibDevice.GenerateLibDeviceCode(
                    nvvmAPI,
                    CudaArchitecture.SM_60,
                    new[] { method },
                    out var ptx);

                ptx = ptx!.ReplaceLineEndings().Trim();
                var decl = ParseDeclaration(ptx);

                doc.WriteStartElement("Function");
                doc.WriteAttributeString("Name", method);
                doc.WriteAttributeString("PtxModule", ptx);
                doc.WriteAttributeString("PtxDeclaration", decl);
                doc.WriteEndElement();
            }
        }

        private static string ParseDeclaration(string ptx)
        {
            // The PTX function starts with .visible, and ends when the function body
            // opens with a { brace.
            //
            // This is a forward declaration, so use .extern.
            const string VisibleKeyword = ".visible";
            var startIdx = ptx.IndexOf(VisibleKeyword) + VisibleKeyword.Length;
            var endIdx = ptx.IndexOf('{', startIdx);

            return string.Concat(".extern", ptx.AsSpan(startIdx, endIdx - startIdx), ";");
        }

        private static IEnumerable<string> LoadMethodNames()
        {
            var doc = new XmlDocument();
            var filePath = Path.Combine(GetDefaultFolder(), "CudaLibDevice.xml");
            doc.Load(filePath);

            var functionNodes = doc.SelectNodes("//Function");
            if (functionNodes != null)
                foreach (var element in functionNodes.OfType<XmlElement>())
                    yield return element.GetAttribute("Name");
        }

        private static string GetDefaultFolder()
        {
            var rootFolder = GetRepositoryFromFile()!.FullName;
            return Path.Combine(rootFolder, "Src", "ILGPU", "Static");
        }

        private static DirectoryInfo? GetRepositoryFromFile()
        {
            const string DotGit = ".git";
            var file = new FileInfo(Assembly.GetEntryAssembly()!.Location);
            var next = file.Directory;

            while (next != null)
            {
                if (next.Name.Equals(DotGit, StringComparison.OrdinalIgnoreCase))
                    return default;
                else if (Directory.Exists(Path.Combine(next.FullName, DotGit)))
                    return next;

                next = next.Parent;
            }

            return default;
        }
    }
}
