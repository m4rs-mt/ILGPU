// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CopyrightUpdateTool.Abstractions;
using CopyrightUpdateTool.Parsers;
using CopyrightUpdateTool.Util;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CopyrightUpdateTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Use the path supplied in the first parameter.
            // If none specified, use the current folder.
            string path =
                args.Length >= 1
                ? path = args[0]
                : Directory.GetCurrentDirectory();

            // Register dependency injection.
            var services = new ServiceCollection();
            services.AddSingleton<Runner>();
            services.AddSingleton<IVersionControlService, VersionControlService>();
            services.AddSingleton<ICopyrightParser, SourceCodeCopyrightParser>();
            services.AddSingleton<ICopyrightParser, NuspecCopyrightParser>();
            services.AddSingleton<ICopyrightParser, LicenseCopyrighParser>();
            services.AddSingleton<ICopyrightParser, ReadmeCopyrightParser>();

            // Perform copyright update.
            var serviceProvider = services.BuildServiceProvider();
            var runner = serviceProvider.GetRequiredService<Runner>();

            await runner.UpdateCopyrightAsync(path, CancellationToken.None);
        }
    }
}
