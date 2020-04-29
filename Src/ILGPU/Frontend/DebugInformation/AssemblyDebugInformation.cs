// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AssemblyDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

namespace ILGPU.Frontend.DebugInformation
{
    /// <summary>
    /// Represents assembly debug information.
    /// </summary>
    public sealed class AssemblyDebugInformation : IMetadataReaderOperationProvider
    {
        #region Instance

        /// <summary>
        /// The internal mapping of methods to cached debug information.
        /// </summary>
        private readonly Dictionary<MethodBase, MethodDebugInformation>
            debugInformation =
            new Dictionary<MethodBase, MethodDebugInformation>();

        /// <summary>
        /// The internal reader provider.
        /// </summary>
        private readonly MetadataReaderProvider readerProvider;

        /// <summary>
        /// The internal synchronization object.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// Constructs new empty assembly debug information.
        /// </summary>
        /// <param name="assembly">The referenced assembly.</param>
        internal AssemblyDebugInformation(Assembly assembly)
        {
            Assembly = assembly;
            Modules = ImmutableArray<Module>.Empty;
        }

        /// <summary>
        /// Constructs new assembly debug information.
        /// </summary>
        /// <param name="assembly">The referenced assembly.</param>
        /// <param name="pdbStream">
        /// The associated PDB stream (hast to be kept open).
        /// </param>
        internal AssemblyDebugInformation(Assembly assembly, Stream pdbStream)
        {
            Assembly = assembly;
            Modules = ImmutableArray.Create(assembly.GetModules());

            readerProvider = MetadataReaderProvider.FromPortablePdbStream(
                pdbStream,
                MetadataStreamOptions.Default);
            MetadataReader = readerProvider.GetMetadataReader();

            foreach (var methodHandle in MetadataReader.MethodDebugInformation)
            {
                var definitionHandle = methodHandle.ToDefinitionHandle();
                var metadataToken = MetadataTokens.GetToken(definitionHandle);
                if (TryResolveMethod(metadataToken, out MethodBase method))
                {
                    debugInformation.Add(
                        method,
                        new MethodDebugInformation(
                            this,
                            method,
                            definitionHandle));
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated assembly.
        /// </summary>
        public Assembly Assembly { get; }

        /// <summary>
        /// Returns the associated modules.
        /// </summary>
        public ImmutableArray<Module> Modules { get; }

        /// <summary>
        /// Returns true if this container holds valid debug information.
        /// </summary>
        public bool IsValid => !Modules.IsDefaultOrEmpty;

        /// <summary>
        /// Returns the associated metadata reader.
        /// </summary>
        private MetadataReader MetadataReader { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Begins a synchronized metadata reader operation.
        /// </summary>
        /// <returns>The operation instance.</returns>
        MetadataReaderOperation IMetadataReaderOperationProvider.BeginOperation() =>
            new MetadataReaderOperation(MetadataReader, syncLock);

        /// <summary>
        /// Tries to resolve the given metadata token to a method.
        /// </summary>
        /// <param name="metadataToken">The metadata token to resolve.</param>
        /// <param name="method">The resolved method (or null).</param>
        /// <returns>True, if the given token could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveMethod(int metadataToken, out MethodBase method)
        {
            foreach (var module in Modules)
            {
                method = module.ResolveMethod(metadataToken);
                if (method != null)
                    return true;
            }
            method = null;
            return false;
        }

        /// <summary>
        /// Tries to load debug information for the given method base.
        /// </summary>
        /// <param name="methodBase">The method base.</param>
        /// <param name="methodDebugInformation">
        /// The loaded debug information (or null).
        /// </param>
        /// <returns>True, if the requested debug information could be loaded.</returns>
        public bool TryLoadDebugInformation(
            MethodBase methodBase,
            out MethodDebugInformation methodDebugInformation)
        {
            Debug.Assert(methodBase != null, "Invalid method");
            Debug.Assert(
                methodBase.Module.Assembly == Assembly,
                "Invalid method association");

            if (methodBase is MethodInfo methodInfo &&
                methodInfo.GetGenericArguments().Length > 0)
            {
                methodBase = methodInfo.GetGenericMethodDefinition();
            }
            return debugInformation.TryGetValue(
                methodBase,
                out methodDebugInformation);
        }

        #endregion
    }
}
