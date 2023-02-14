// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Runner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using CudaVersionUpdateTool.Abstractions;

namespace CudaVersionUpdateTool.Util
{
    class Runner
    {
        private readonly IParserService ParserService;
        private readonly IGeneratorService GeneratorService;

        /// <summary>
        /// Constructs a new runner instance.
        /// </summary>
        public Runner(IParserService service, IGeneratorService generatorService)
        {
            ParserService = service;
            GeneratorService = generatorService;
        }

        /// <summary>
        /// Updates the Cuda files in the supplied path.
        /// </summary>
        /// <param name="path">The base path to process.</param>
        public async Task UpdateArchitectureAsync(string path)
        {
            var versionSets = await ParserService.GetVersionSetsAsync();
            await GeneratorService.GenerateAsync(path, versionSets.ToList());
        }
    }
}
