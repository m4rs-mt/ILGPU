// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: AssemblyDebugInformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend.DebugInformation;

/// <summary>
/// Represents assembly debug information.
/// </summary>
sealed class AssemblyDebugInformation
{
    #region Static

    /// <summary>
    /// Loads assembly debug information for the given assembly and stream.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="pdbStream">The source stream to load debug information from.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static AssemblyDebugInformation Load(Assembly assembly, Stream pdbStream)
    {
        var modules = assembly.GetModules();
        bool TryResolveMethod(
            int metadataToken,
            [NotNullWhen(true)] out MethodBase? method)
        {
            foreach (var module in modules)
            {
                method = module.ResolveMethod(metadataToken);
                if (method is not null)
                    return true;
            }
            method = null;
            return false;
        }

        using var readerProvider = MetadataReaderProvider.FromPortablePdbStream(
            pdbStream,
            MetadataStreamOptions.PrefetchMetadata);
        var reader = readerProvider.GetMetadataReader();
        var debugInformation = new Dictionary<MethodBase, MethodDebugInformation>(128);
        foreach (var methodHandle in reader.MethodDebugInformation)
        {
            var definitionHandle = methodHandle.ToDefinitionHandle();
            var metadataToken = MetadataTokens.GetToken(definitionHandle);
            if (!TryResolveMethod(metadataToken, out MethodBase? method))
                continue;
            debugInformation.Add(
                method,
                MethodDebugInformation.Load(reader, definitionHandle));
        }

        return new AssemblyDebugInformation(assembly, debugInformation);
    }

    #endregion

    #region Instance

    /// <summary>
    /// The internal mapping of methods to cached debug information.
    /// </summary>
    private readonly Dictionary<MethodBase, MethodDebugInformation> _debugInformation;

    /// <summary>
    /// Constructs new assembly debug information.
    /// </summary>
    /// <param name="assembly">The referenced assembly.</param>
    /// <param name="debugInformation">
    /// Associated debug information mappings for each method.
    /// </param>
    private AssemblyDebugInformation(
        Assembly assembly,
        Dictionary<MethodBase, MethodDebugInformation> debugInformation)
    {
        Assembly = assembly;

        _debugInformation = debugInformation;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated assembly.
    /// </summary>
    public Assembly Assembly { get; }

    #endregion

    #region Methods

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
        [NotNullWhen(true)] out MethodDebugInformation methodDebugInformation)
    {
        Debug.Assert(
            methodBase.Module.Assembly == Assembly,
            "Invalid method association");

        if (methodBase is MethodInfo methodInfo &&
            methodInfo.GetGenericArguments().Length > 0)
        {
            methodBase = methodInfo.GetGenericMethodDefinition();
        }
        return _debugInformation.TryGetValue(
            methodBase,
            out methodDebugInformation);
    }

    #endregion
}
