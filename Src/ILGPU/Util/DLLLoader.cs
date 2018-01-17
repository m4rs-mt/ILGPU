// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: DLLLoader.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents an operating system.
    /// </summary>
    public enum OSPlatform
    {
        /// <summary>
        /// The windows operating system.
        /// </summary>
        Windows,

        /// <summary>
        /// The Linux/Ubuntu operating system.
        /// </summary>
        Ubuntu,

        /// <summary>
        /// The Linux/CentOS operating system.
        /// </summary>
        Fedora,

        /// <summary>
        /// The Linux/CentOS operating system.
        /// </summary>
        CentOS,

        /// <summary>
        /// The Linux/Debian operating system.
        /// </summary>
        Debian,

        /// <summary>
        /// The MacOS operating system.
        /// </summary>
        Darwin
    }

    /// <summary>
    /// A helper class for injecting native libraries from the X64 or the X86 lib folder.
    /// </summary>
    public static class DLLLoader
    {
        private static readonly IReadOnlyDictionary<OSPlatform, string> LibraryPathVariableNames = new Dictionary<OSPlatform, string>()
        {
            { OSPlatform.Windows, "PATH" },
            { OSPlatform.Ubuntu, "LD_LIBRARY_PATH" },
            { OSPlatform.Fedora, "LD_LIBRARY_PATH" },
            { OSPlatform.CentOS, "LD_LIBRARY_PATH" },
            { OSPlatform.Debian, "LD_LIBRARY_PATH" },
            { OSPlatform.Darwin, "DYLD_LIBRARY_PATH" },
        };

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static DLLLoader()
        {
            CurrentOSPlatform = OSPlatform.Windows;
#if NETCOREAPP2_0
            var platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            var supportedPlatforms = Enum.GetNames(typeof(OSPlatform));
            for (int i = 0, e = supportedPlatforms.Length; i < e; ++i)
            {
                if (platform.Contains(supportedPlatforms[i]))
                {
                    CurrentOSPlatform = (OSPlatform)i;
                    break;
                }
            }
#endif
            LibraryPathVariable = LibraryPathVariableNames[CurrentOSPlatform];
        }

        /// <summary>
        /// Returns the current OS platform.
        /// </summary>
        public static OSPlatform CurrentOSPlatform { get; }

        /// <summary>
        /// Returns the current dynamic-library-path variable.
        /// </summary>
        public static string LibraryPathVariable { get; }

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
            var libDir = Path.Combine(rootDir, subDirectory);
            libDir = Path.Combine(libDir, CurrentOSPlatform.ToString());

            switch (CurrentOSPlatform)
            {
                case OSPlatform.Windows:
                    var currentPath = Environment.GetEnvironmentVariable(LibraryPathVariable) ?? string.Empty;
                    if (currentPath.Contains(libDir))
                        return;
                    if (currentPath.Length > 0 &&
                        !currentPath.EndsWith(Path.PathSeparator.ToString(), StringComparison.Ordinal))
                        currentPath += Path.PathSeparator;
                    currentPath += libDir;
                    Environment.SetEnvironmentVariable(LibraryPathVariable, currentPath, EnvironmentVariableTarget.Process);
                    break;
                default:
                    // Look for all dynamic libraries in this folder and symlink them to the current target directory
                    // TODO: this is a workaround for the limited DLLImport functionality of dotnetcore.
                    // As soon as this functionality is extended, we can remove this workaround and adapt the search path.
                    foreach (var file in Directory.GetFiles(libDir))
                    {
                        var targetPath = Path.Combine(rootDir, Path.GetFileName(file));
                        if (File.Exists(targetPath))
                            File.Delete(targetPath);
                        using (var p = Process.Start("ln", $"-s \"{file}\" \"{targetPath}\""))
                            p.WaitForExit();
                    }
                    break;
            }
        }
    }
}
