// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: GeneratorService.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CudaVersionUpdateTool.Abstractions;
using System.Text;

namespace CudaVersionUpdateTool.Services
{
    internal class GeneratorService : IGeneratorService
    {
        public async Task GenerateAsync(
            string path,
            IReadOnlyCollection<CudaVersionSet> versionSets)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (versionSets == null)
                throw new ArgumentNullException(nameof(versionSets));

            var doc = new StringBuilder();
            doc.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            doc.AppendLine("<Versions>");

            var orderedVersionSets =
                versionSets
                .OrderBy(x => x.InstructionSet.Major)
                .ThenBy(x => x.InstructionSet.Minor)
                .ThenBy(x => x.DriverVersion.Major)
                .ThenBy(x => x.DriverVersion.Minor)
                .ThenBy(x => x.Architecture.Major)
                .ThenBy(x => x.Architecture.Minor);

            foreach (var versionSet in orderedVersionSets)
            {
                doc.AppendLine($"    <Version" +
                    $" InstructionSet=\"{versionSet.InstructionSet}\"" +
                    $" Driver=\"{versionSet.DriverVersion}\"" +
                    $" Architecture=\"{versionSet.Architecture}\"" +
                    $" />");
            }

            doc.AppendLine("</Versions>");

            var filePath = Path.Combine(path, "CudaVersions.xml");
            var fileContents = doc.ToString();
            await File.WriteAllTextAsync(filePath, fileContents);
        }
    }
}
