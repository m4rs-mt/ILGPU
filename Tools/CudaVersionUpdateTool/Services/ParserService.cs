// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ParserService.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using AngleSharp;
using AngleSharp.Dom;
using CudaVersionUpdateTool.Abstractions;
using System.Text.RegularExpressions;

namespace CudaVersionUpdateTool.Services
{
    internal partial class ParserService : IParserService
    {
        private readonly string ReleaseNotesUrl =
            "https://docs.nvidia.com/cuda/parallel-thread-execution/index.html";

        public async Task<IEnumerable<CudaVersionSet>> GetVersionSetsAsync()
        {
            var versionSets = new List<CudaVersionSet>();

            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            var doc = await context.OpenAsync(ReleaseNotesUrl);
            var section = doc.GetElementById("release-notes");
            var rows = section?.QuerySelectorAll("tbody tr");

            foreach (var row in rows ?? Enumerable.Empty<IElement>())
            {
                // NB: We expect there to be at least 3 cells per row. The Instruction
                // Set, Driver Version, then Architectures. However, for CUDA 3.0, the
                // Instruction Set spans across two rows, so we deal with it by using
                // the last found Instruction Set.
                var hasInstructionSet = row.ChildElementCount >= 3;
                CudaInstructionSet instructionSet;
                IElement? isaCell;
                IElement? driverCell;
                IElement? archCell;

                if (hasInstructionSet)
                {
                    isaCell = row.QuerySelector("td:nth-child(1)");
                    driverCell = row.QuerySelector("td:nth-child(2)");
                    archCell = row.QuerySelector("td:nth-child(3)");
                }
                else
                {
                    isaCell = default;
                    driverCell = row.QuerySelector("td:nth-child(1)");
                    archCell = row.QuerySelector("td:nth-child(2)");
                }

                // Instruction Set
                if (hasInstructionSet)
                {
                    var isaCellText = isaCell?.TextContent;
                    if (isaCellText == null ||
                        !InstructionSetPattern().IsMatch(isaCellText))
                        continue;
                    var instructionSetText =
                        InstructionSetPattern().Replace(isaCellText, "$1");
                    instructionSet = CudaInstructionSet.Parse(instructionSetText);
                }
                else
                {
                    instructionSet = versionSets.Last().InstructionSet;
                }

                // Driver Version
                var driverCellText = driverCell?.TextContent;
                if (driverCellText == null ||
                    !DriverVersionPattern().IsMatch(driverCellText))
                    continue;
                var driverVersionText =
                    DriverVersionPattern().Replace(driverCellText, "$1");
                var driverVersion = CudaDriverVersion.Parse(driverVersionText);

                // Architectures
                var archCellText = archCell?.TextContent;
                if (archCellText == null || !ArchitecturePattern().IsMatch(archCellText))
                    continue;
                var archMatches = ArchitecturePattern().Matches(archCellText);
                var architectures = archMatches
                    .SelectMany(match => match.Groups["arch"].Captures)
                    .Select(capture => capture.Value)
                    .Distinct()
                    .Select(smText =>
                    {
                        var length = smText.Length;
                        return CudaArchitecture.Parse(smText.Insert(length - 1, "."));
                    })
                    .ToList();

                foreach (var architecture in architectures)
                    versionSets.Add(new(instructionSet, driverVersion, architecture));
            }

            return versionSets;
        }

        [GeneratedRegex("^PTX ISA ([.\\d+])")]
        private static partial Regex InstructionSetPattern();

        [GeneratedRegex(
            "^CUDA (?<drv>[\\.\\d]+).*$",
            RegexOptions.ExplicitCapture)]
        private static partial Regex DriverVersionPattern();

        [GeneratedRegex(
            "sm_(?<arch>\\d+)|sm_{((?<arch>\\d+)[A-Za-z]*[,]*)*}",
            RegexOptions.ExplicitCapture)]
        private static partial Regex ArchitecturePattern();
    }
}
