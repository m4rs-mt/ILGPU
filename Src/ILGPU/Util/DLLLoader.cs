// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DLLLoader.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;

namespace ILGPU.Util
{
    /// <summary>
    /// A helper class for injecting native libraries from the X64 or the X86 lib folder.
    /// </summary>
    public static class DLLLoader
    {
        private const string PathVariable = "PATH";

        /// <summary>
        /// Adds the default search directory (../X86/ or ../X64/) to the search path.
        /// </summary>
        public static void AddDefaultX86X64SearchPath()
        {
            AddSearchPath(Environment.Is64BitProcess ? "X64" : "X86");
        }

        /// <summary>
        /// Adds the given sub directory to the current search path.
        /// </summary>
        /// <param name="subDirectory">The sub directory to add.</param>
        public static void AddSearchPath(string subDirectory)
        {
            if (string.IsNullOrWhiteSpace(subDirectory))
                throw new ArgumentNullException(nameof(subDirectory));
            var rootDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            rootDir = Path.Combine(rootDir, subDirectory);
            var currentPath = Environment.GetEnvironmentVariable(PathVariable);
            if (currentPath.Contains(rootDir))
                return;
            if (!currentPath.EndsWith(";", StringComparison.Ordinal))
                currentPath += ";";
            currentPath += rootDir + ";";
            Environment.SetEnvironmentVariable(PathVariable, currentPath, EnvironmentVariableTarget.Process);
        }
    }
}
