// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: AssemblyDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
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
    public sealed class AssemblyDebugInformation : DisposeBase
    {
        #region Instance

        private MetadataReaderProvider metadataReaderProvider;
        private readonly Dictionary<MethodBase, MethodDebugInformation> debugInformation =
            new Dictionary<MethodBase, MethodDebugInformation>();

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
        /// <param name="fileName">The associated PDB file.</param>
        internal AssemblyDebugInformation(Assembly assembly, string fileName)
        {
            Assembly = assembly;
            Modules = ImmutableArray.Create(assembly.GetModules());

            using (var pdbFileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                metadataReaderProvider = MetadataReaderProvider.FromPortablePdbStream(
                    pdbFileStream,
                    MetadataStreamOptions.PrefetchMetadata);
                MetadataReader = metadataReaderProvider.GetMetadataReader();
            }

            var methodDebugInformationEnumerator = MetadataReader.MethodDebugInformation.GetEnumerator();
            while (methodDebugInformationEnumerator.MoveNext())
            {
                var methodDebugRef = methodDebugInformationEnumerator.Current;
                var methodDefinitionHandle = methodDebugRef.ToDefinitionHandle();
                var metadataToken = MetadataTokens.GetToken(methodDefinitionHandle);
                if (TryResolveMethod(metadataToken, out MethodBase method))
                    debugInformation.Add(method, new MethodDebugInformation(this, method, methodDebugRef));
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
        internal MetadataReader MetadataReader { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the given metadata token to a method.
        /// </summary>
        /// <param name="metadataToken">The metadata token to resolve.</param>
        /// <param name="method">The resolved method (or null).</param>
        /// <returns>True, iff the given token could be resolved.</returns>
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
        /// <param name="methodDebugInformation">The loaded debug information (or null).</param>
        /// <returns>True, iff the requested debug information could be loaded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLoadDebugInformation(
            MethodBase methodBase,
            out MethodDebugInformation methodDebugInformation)
        {
            Debug.Assert(methodBase != null, "Invalid method");
            Debug.Assert(methodBase.Module.Assembly == Assembly, "Invalid method association");

            if (methodBase is MethodInfo methodInfo && methodInfo.GetGenericArguments().Length > 0)
                methodBase = methodInfo.GetGenericMethodDefinition();
            if (!debugInformation.TryGetValue(methodBase, out methodDebugInformation))
                return false;
            methodDebugInformation.LoadSequencePoints();
            return true;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Dispose(ref metadataReaderProvider);
        }

        #endregion
    }
}
