// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.ProbeServicesCommand.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using System;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC;

sealed partial class Program
{
    /// <summary>
    /// Diagnostic subcommand that prints the routing table the build/compile
    /// commands would produce for the current environment. Useful for
    /// dev-loop and CI debugging — answers "is my Docker compiler service
    /// actually being picked up?" without running a real build.
    /// </summary>
    sealed class ProbeServicesCommand
    {
        readonly Option<string[]> _compilerServiceOption = new("--compiler-service")
        {
            Description =
                "Base URL of a remote ILGPUC.CompilerService instance "
                + "(repeatable; first to advertise a backend wins)",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = static _ => [],
        };

        readonly Option<bool> _allowLocalOption = new("--allow-local")
        {
            Description =
                "Include the local CompilerManager as a fallback for "
                + "any target not served by a remote service",
            DefaultValueFactory = static _ => true,
        };

        public ProbeServicesCommand(RootCommand rootCommand)
        {
            var command = new Command(
                "probe-services",
                description:
                    "Walks the same compiler-service resolution chain as "
                    + "`build` / `compile` and prints the resolved routing "
                    + "table. Honours --compiler-service flags, "
                    + "ILGPU_*_SERVICE_URL env vars, and Docker default "
                    + "ports (5001/5002/5003).")
            {
                _compilerServiceOption,
                _allowLocalOption,
            };

            command.SetAction(Execute);
            rootCommand.Add(command);
        }

        async Task Execute(ParseResult parseResult, CancellationToken ct)
        {
            var rawUrls = parseResult.GetValue(_compilerServiceOption) ?? [];
            var serviceUrls = rawUrls
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new Uri(s))
                .ToArray();
            var allowLocal = parseResult.GetValue(_allowLocalOption);

            var manager = await CompilerManagerFactory.CreateAsync(
                serviceUrls,
                allowLocal,
                ct).ConfigureAwait(false);

            await Console.Out.WriteLineAsync(
                $"ILGPUC compiler-service routing "
                + $"({manager.Routes.Count} target(s) routed):");

            foreach (var target in Enum.GetValues<CompilationTarget>())
            {
                if (!manager.Routes.TryGetValue(target, out var inner))
                {
                    await Console.Out.WriteLineAsync(
                        $"  {target,-12} -> (no manager)");
                    continue;
                }

                var label = inner switch
                {
                    RemoteCompilerManager remote => remote.BaseUrl.ToString(),
                    CompilerManager => "local",
                    _ => inner.GetType().Name,
                };

                var caps = await inner.GetCapabilitiesAsync(ct)
                    .ConfigureAwait(false);
                var available = caps.Compilers.Any(
                    c => c.Target == target && c.Available);
                var status = available ? "available" : "UNAVAILABLE";
                var version = caps.Compilers
                    .FirstOrDefault(c => c.Target == target)
                    ?.Version;
                var versionSuffix = version is null ? "" : $" [{version}]";

                await Console.Out.WriteLineAsync(
                    $"  {target,-12} -> {label} ({status}){versionSuffix}");
            }
        }
    }
}
