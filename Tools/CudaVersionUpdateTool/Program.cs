// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CudaVersionUpdateTool.Abstractions;
using CudaVersionUpdateTool.Services;
using CudaVersionUpdateTool.Util;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CudaVersionUpdateTool
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Use the path supplied in the first parameter.
            // If none specified, use the current folder.
            string path =
                args.Length >= 1
                ? args[0]
                : GetDefaultFolder();

            // Register dependency injection.
            var services = new ServiceCollection();
            services.AddSingleton<Runner>();
            services.AddHttpClient();
            services.AddScoped<IParserService, ParserService>();
            services.AddScoped<IGeneratorService, GeneratorService>();

            // Perform architecture update.
            var serviceProvider = services.BuildServiceProvider();
            var runner = serviceProvider.GetRequiredService<Runner>();

            await runner.UpdateArchitectureAsync(path);
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
