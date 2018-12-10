// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Frontend.DebugInformation
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

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim(
            LockRecursionPolicy.SupportsRecursion);
        private readonly Dictionary<string, string> pdbFiles = new Dictionary<string, string>();
        private readonly HashSet<string> lookupDirectories = new HashSet<string>();
        private readonly Dictionary<Assembly, AssemblyDebugInformation> assemblies =
            new Dictionary<Assembly, AssemblyDebugInformation>();

        /// <summary>
        /// Constructs a new debug-information manager.
        /// </summary>
        public DebugInformationManager()
        {
            // Register the current application path
            var currentDirectory = Directory.GetCurrentDirectory();
            if (Directory.Exists(currentDirectory))
                RegisterLookupDirectory(currentDirectory);
        }

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
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (assemblies.TryGetValue(assembly, out assemblyDebugInformation))
                    return true;

                var debugDir = Path.GetDirectoryName(assembly.Location);
                if (!lookupDirectories.Contains(debugDir))
                    RegisterLookupDirectory(debugDir);

                var pdbFileName = assembly.GetName().Name;
                return TryLoadSymbolsInternal(assembly, pdbFileName, out assemblyDebugInformation);
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Tries to load symbols for the given assembly based on the given debug-information file.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="pdbFileName">The name of the debug-information file.</param>
        /// <param name="assemblyDebugInformation">The loaded debug information (or null).</param>
        /// <returns>True, iff the debug information could be loaded.</returns>
        public bool TryLoadSymbols(
            Assembly assembly,
            string pdbFileName,
            out AssemblyDebugInformation assemblyDebugInformation)
        {
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (assemblies.TryGetValue(assembly, out assemblyDebugInformation))
                    return true;
                return TryLoadSymbolsInternal(
                    assembly,
                    pdbFileName,
                    out assemblyDebugInformation);
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Tries to load symbols for the given assembly based on the given debug-information file.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="pdbFileName">The name of the debug-information file.</param>
        /// <param name="assemblyDebugInformation">The loaded debug information (or null).</param>
        /// <returns>True, iff the debug information could be loaded.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Loading exceptions will be published on the debug output")]
        private bool TryLoadSymbolsInternal(
            Assembly assembly,
            string pdbFileName,
            out AssemblyDebugInformation assemblyDebugInformation)
        {
            cacheLock.EnterWriteLock();
            try
            {
                if (pdbFiles.TryGetValue(pdbFileName, out string fileName))
                {
                    try
                    {
                        assemblyDebugInformation = new AssemblyDebugInformation(assembly, fileName);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error loading pdb file: " + pdbFileName);
                        Debug.WriteLine(ex.ToString());
                        assemblyDebugInformation = new AssemblyDebugInformation(assembly);
                    }
                }
                else
                    assemblyDebugInformation = new AssemblyDebugInformation(assembly);

                assemblies.Add(assembly, assemblyDebugInformation);
                return assemblyDebugInformation.IsValid;
            }
            finally
            {
                cacheLock.ExitWriteLock();
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
            cacheLock.EnterReadLock();
            try
            {
                return pdbFiles.TryGetValue(pdbFileName, out fileName);
            }
            finally
            {
                cacheLock.ExitReadLock();
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

            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (lookupDirectories.Contains(directory))
                    return;

                cacheLock.EnterWriteLock();
                try
                {
                    var files = Directory.GetFiles(directory, PDBFileSearchPattern);
                    foreach (var file in files)
                        pdbFiles[Path.GetFileNameWithoutExtension(file)] = file;

                    lookupDirectories.Add(directory);
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
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
            Debug.Assert(methodBase != null, "Invalid method");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SequencePointEnumerator LoadSequencePoints(MethodBase methodBase)
        {
            if (TryLoadDebugInformation(methodBase, out MethodDebugInformation methodDebugInformation))
                return methodDebugInformation.CreateSequencePointEnumerator();
            return SequencePointEnumerator.Empty;
        }

        /// <summary>
        /// Loads the scopes of the given method.
        /// </summary>
        /// <param name="methodBase">The method base.</param>
        /// <returns>A scope-enumerator that targets the given method.</returns>
        /// <remarks>
        /// If no debug information could be loaded for the given method, an empty
        /// <see cref="MethodScopeEnumerator"/> will be returned.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodScopeEnumerator LoadScopes(MethodBase methodBase)
        {
            if (TryLoadDebugInformation(methodBase, out MethodDebugInformation methodDebugInformation))
                return methodDebugInformation.CreateScopeEnumerator();
            return MethodScopeEnumerator.Empty;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "cacheLock",
            Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            foreach (var assembly in assemblies.Values)
                assembly.Dispose();
            assemblies.Clear();
            Dispose(ref cacheLock);
        }

        #endregion
    }
}
