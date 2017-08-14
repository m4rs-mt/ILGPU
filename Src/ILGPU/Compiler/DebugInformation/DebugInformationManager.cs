// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DebugInformationManager.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace ILGPU.Compiler.DebugInformation
{
    /// <summary>
    /// Represents a debug-information manager.
    /// </summary>
    public sealed class DebugInformationManager : DisposeBase
    {
        #region Constants

        /// <summary>
        /// The PDB file extension (.pdb).
        /// </summary>
        public const string PDBFileExtensions = ".pdb";

        /// <summary>
        /// The PDB file-search extension (*.pdb).
        /// </summary>
        public const string PDBFileSearchPattern = "*" + PDBFileExtensions;

        #endregion

        #region Instance

        private readonly object thisLock = new object();
        private readonly Dictionary<string, string> pdbFiles = new Dictionary<string, string>();
        private readonly Dictionary<Assembly, AssemblyDebugInformation> assemblies =
            new Dictionary<Assembly, AssemblyDebugInformation>();
        private readonly HashSet<Assembly> triedAssemlbies = new HashSet<Assembly>();

        /// <summary>
        /// Constructs a new debug-information manager.
        /// </summary>
        public DebugInformationManager() { }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to load symbols for the given assemlby.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="assemblyDebugInformation">The loaded debug information (or null).</param>
        /// <returns>True, iff the debug information could be loaded.</returns>
        public bool TryLoadSymbols(
            Assembly assembly,
            out AssemblyDebugInformation assemblyDebugInformation)
        {
            var debugDir = Path.GetDirectoryName(assembly.Location);
            if (Directory.Exists(debugDir))
                RegisterLookupDirectory(debugDir);
            var pdbFileName = Path.GetFileNameWithoutExtension(assembly.GetName().Name) + PDBFileExtensions;
            return TryLoadSymbols(assembly, pdbFileName, out assemblyDebugInformation);
        }

        /// <summary>
        /// Tries to load symbols for the given assembly based on the given debug-information file.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="pdbFileName">The name of the debug-information file.</param>
        /// <param name="assemblyDebugInformation">The loaded debug information (or null).</param>
        /// <returns>True, iff the debug information could be loaded.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Loading exceptions will be published on the debug output")]
        public bool TryLoadSymbols(
            Assembly assembly,
            string pdbFileName,
            out AssemblyDebugInformation assemblyDebugInformation)
        {
            lock (thisLock)
            {
                if (assemblies.TryGetValue(assembly, out assemblyDebugInformation))
                    return true;

                if (triedAssemlbies.Contains(assembly) ||
                   !TryFindPbdFile(pdbFileName, out string fileName))
                    return false;
                triedAssemlbies.Add(assembly);

                try
                {
                    assemblyDebugInformation = new AssemblyDebugInformation(assembly, fileName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error loading pdb file: " + pdbFileName);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    return false;
                }
                assemblies.Add(assembly, assemblyDebugInformation);
                return true;
            }
        }

        /// <summary>
        /// Tries to find a debug-information file with the name <paramref name="pdbFileName"/>.
        /// </summary>
        /// <param name="pdbFileName">The name of the debug-information file.</param>
        /// <param name="fileName">The resolved filename (or null).</param>
        /// <returns>True, iff the given debug-information file could be found.</returns>
        public bool TryFindPbdFile(string pdbFileName, out string fileName)
        {
            lock (thisLock)
            {
                return pdbFiles.TryGetValue(pdbFileName, out fileName);
            }
        }

        /// <summary>
        /// Registers the given directory as a source directory for debug-information files.
        /// </summary>
        /// <param name="directory">The directory to register.</param>
        public void RegisterLookupDirectory(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException();
            var files = Directory.GetFiles(directory, PDBFileSearchPattern);
            lock (thisLock)
            {
                foreach (var file in files)
                    pdbFiles[Path.GetFileName(file)] = file;
            }
        }

        /// <summary>
        /// Tries to load debug information for the given method.
        /// </summary>
        /// <param name="methodBase">The method.</param>
        /// <param name="methodDebugInformation">Loaded debug information (or null).</param>
        /// <returns>True, iff debug information could be loaded.</returns>
        public bool TryLoadDebugInformation(
            MethodBase methodBase,
            out MethodDebugInformation methodDebugInformation)
        {
            if (methodBase == null)
                throw new ArgumentNullException(nameof(methodBase));
            methodDebugInformation = null;
            if (!TryLoadSymbols(
                methodBase.Module.Assembly,
                out AssemblyDebugInformation assemblyDebugInformation))
                return false;
            return assemblyDebugInformation.TryLoadDebugInformation(
                methodBase,
                out methodDebugInformation);
        }

        /// <summary>
        /// Loads the sequence points of the given method.
        /// </summary>
        /// <param name="methodBase">The method base.</param>
        /// <returns>A sequence-point enumerator that targets the given method.</returns>
        /// <remarks>
        /// If no debug information could be loaded for the given method, an empty
        /// <see cref="SequencePointEnumerator"/> will be returned.
        /// </remarks>
        public SequencePointEnumerator LoadSequencePoints(MethodBase methodBase)
        {
            if (TryLoadDebugInformation(methodBase, out MethodDebugInformation methodDebugInformation))
                return methodDebugInformation.CreateSequencePointEnumerator();
            return SequencePointEnumerator.Empty;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            foreach (var assembly in assemblies.Values)
                assembly.Dispose();
            assemblies.Clear();
        }

        #endregion
    }
}
