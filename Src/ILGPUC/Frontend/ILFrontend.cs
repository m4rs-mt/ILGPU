// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILFrontend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.Backends;
using ILGPUC.Frontend.DebugInformation;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPUC.Frontend;

/// <summary>
/// The ILGPUC MSIL frontend.
/// </summary>
sealed class ILFrontend
{
    #region Constants & Nested Types

    /// <summary>
    /// The PDB file extension (.pdb).
    /// </summary>
    private const string PDBFileExtensions = ".pdb";

    #endregion

    #region Instance

    private readonly List<string?> _pdbSearchPaths;
    private readonly Dictionary<Assembly, AssemblyDebugInformation?>
        _referencedAssemblies = new(16);
    private readonly Dictionary<MethodBase, DisassembledMethod?> _methods = new(1024);
    private readonly Stack<HashSet<MethodBase>> _methodsStack = new(2);

    /// <summary>
    /// Constructs a new IL frontend using the given paths to resolve debug information
    /// </summary>
    /// <param name="backendType">The current backend type we are compiling for.</param>
    /// <param name="pdbSearchPaths">The list of search paths.</param>
    public ILFrontend(BackendType backendType, params List<string?> pdbSearchPaths)
    {
        BackendType = backendType;

        // Determine pdb lookup directories
        var currentDirectory = Directory.GetCurrentDirectory();
        if (Directory.Exists(currentDirectory))
            pdbSearchPaths.Insert(0, currentDirectory);
        _pdbSearchPaths = pdbSearchPaths;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the current backend type we are compiling for.
    /// </summary>
    public BackendType BackendType { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Pushes a processing scope to record all methods to be encountered.
    /// </summary>
    public void PushScope() => _methodsStack.Push(new(256));

    /// <summary>
    /// Pops a processing scope and returns the set of all methods encountered.
    /// </summary>
    public IReadOnlyCollection<MethodBase> PopScope() => _methodsStack.Pop();

    /// <summary>
    /// Tries to find a pdb file for the given assembly.
    /// </summary>
    /// <param name="assembly">The assembly to find the pdb file for.</param>
    /// <param name="pdbFilePath">The determined pdb file path (or null).</param>
    /// <returns>True if a valid pdb file path could be found.</returns>
    private bool TryFindPdbFile(
        Assembly assembly,
        [NotNullWhen(true)] out string? pdbFilePath)
    {
        pdbFilePath = null;
        var debugDir = Path.GetDirectoryName(assembly.Location).AsNotNull();
        var pdbFileName = assembly.GetName().Name;
        if (pdbFileName is null) return false;

        pdbFileName += PDBFileExtensions;
        pdbFilePath = Path.Combine(debugDir, pdbFileName);
        if (File.Exists(pdbFilePath)) return true;

        foreach (var searchPath in _pdbSearchPaths)
        {
            if (string.IsNullOrWhiteSpace(searchPath)) continue;
            pdbFilePath = Path.Combine(searchPath, pdbFileName);
            if (File.Exists(pdbFilePath)) return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get the disassembled method for the given method base.
    /// </summary>
    /// <param name="methodBase">The method base to map to a disassembled method.</param>
    /// <returns>True if a valid disassembled method could be found.</returns>
    public DisassembledMethod? GetDisassembledMethod(MethodBase methodBase) =>
        _methods.TryGetValue(methodBase, out var disassembled) ? disassembled : null;

    /// <summary>
    /// Tries to load debug information for the given method.
    /// </summary>
    /// <param name="methodBase">The method.</param>
    /// <param name="methodDebugInformation">
    /// Loaded debug information (or null).
    /// </param>
    /// <returns>True, if debug information could be loaded.</returns>
    public bool TryLoadDebugInformation(
        MethodBase methodBase,
        out MethodDebugInformation methodDebugInformation)
    {
        methodDebugInformation = default;
        return _referencedAssemblies.TryGetValue(
            methodBase.Module.Assembly,
            out var assemblyDebugInformation) &&
            (assemblyDebugInformation?.TryLoadDebugInformation(
                methodBase,
                out methodDebugInformation) ?? false);
    }

    /// <summary>
    /// Loads the sequence points of the given method.
    /// </summary>
    /// <param name="methodBase">The method base.</param>
    /// <returns>
    /// A sequence-point enumerator that targets the given method.
    /// </returns>
    /// <remarks>
    /// If no debug information could be loaded for the given method, an empty
    /// <see cref="SequencePointEnumerator"/> will be returned.
    /// </remarks>
    public SequencePointEnumerator LoadSequencePoints(MethodBase methodBase) =>
        TryLoadDebugInformation(
            methodBase,
            out MethodDebugInformation methodDebugInformation)
        ? methodDebugInformation.CreateSequencePointEnumerator()
        : SequencePointEnumerator.Empty;

    /// <summary>
    /// Loads all methods while fully disassembling all methods and all called methods.
    /// </summary>
    /// <param name="methods">methods to load.</param>
    public void LoadMethods(IReadOnlyCollection<MethodBase> methods)
    {
        // Disassemble all methods
        DisassembleMethods(methods);

        // Load debug symbols of all assemblies in parallel
        Parallel.ForEach(
            _methods.Keys.Select(t => t.Module.Assembly).Distinct(),
            LoadDebugSymbols);

        // Process all methods and get debug info assigned to them
        Parallel.ForEach(_methods, method =>
        {
            // Make sure the method is valid
            if (method.Value is null) return;

            // Get sequence information from data
            var sequencePoints = LoadSequencePoints(method.Key);
            if (!sequencePoints.IsValid) return;

            // Assemble all instructions
            foreach (var instruction in method.Value)
                instruction.UpdateLocation(sequencePoints);
        });
    }

    /// <summary>
    /// Disassembles all methods and tracks nested/referenced methods to be disassembled.
    /// </summary>
    /// <param name="methods">All methods to be disassembled in full.</param>
    private void DisassembleMethods(IReadOnlyCollection<MethodBase> methods)
    {
        var queue = new ConcurrentQueue<MethodBase>();
        var remapping = new Dictionary<MethodBase, MethodBase>();

        // Process all kernels in parallel
        Parallel.ForEach(methods, method =>
            ProcessMethod(method, queue, remapping));

        // Process all remaining methods in parallel
        Parallel.For(0, queue.Count, _ =>
        {
            while (queue.TryDequeue(out var method))
                ProcessMethod(method, queue, remapping);
        });

        // Map all intrinsic implementations to original methods
        foreach (var (source, target) in remapping)
        {
            while (_methods[source] is null)
                _methods[source] = _methods[target];
        }
    }

    /// <summary>
    /// Processes the given method and registers all calls with the given queue.
    /// </summary>
    /// <param name="method">The method to process.</param>
    /// <param name="queue">The queue to process all methods.</param>
    /// <param name="remapping">Internal method remapping.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ProcessMethod(
        MethodBase method,
        ConcurrentQueue<MethodBase> queue,
        Dictionary<MethodBase, MethodBase> remapping)
    {
        // Register method
        lock (_methods)
        {
            if (_methodsStack.TryPeek(out var set)) set.Add(method);
            if (!_methods.TryAdd(method, null)) return;
        }

        // Try to remap intrinsic methods
        var sourceLocation = new Method.MethodLocation(method);

        // Try to remap an intrinsic function
        var intrinsicKind = Intrinsics.TryImplement(
            sourceLocation,
            method,
            BackendType,
            out var impl);
        if (intrinsicKind == IntrinsicImplementationKind.Remapped ||
            intrinsicKind == IntrinsicImplementationKind.Implemented)
        {
            // Specialize method if necessary
            if (impl is MethodInfo methodInfo && impl.IsGenericMethod)
            {
                var genericArguments = method.GetGenericArguments();
                impl = methodInfo.MakeGenericMethod(genericArguments);
            }

            // Complete mapping of our current method
            lock (remapping) remapping[method] = impl.AsNotNull();

            // Process our intrinsic instead
            ProcessMethod(impl.AsNotNull(), queue, remapping);
        }
        else if (intrinsicKind == IntrinsicImplementationKind.Generated)
        {
            // This method will be completely generated as does not need to be analyzed
            // in further detail. Moreover, we do not need to disassemble the method at
            // all.
        }
        else
        {
            // Disassemble our new method
            var disassembled = Disassembler.TryDisassemble(method);

            // Ignore empty assignments
            if (disassembled is null) return;

            // Add method to internal methods
            lock (_methods) _methods[method] = disassembled;

            // Determine all called methods to disassemble
            foreach (var instruction in disassembled.Instructions)
            {
                if (instruction.Argument is MethodBase calledMethod)
                    queue.Enqueue(calledMethod);
            }
        }
    }

    /// <summary>
    /// Loads all debug symbols of all referenced assemblies.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void LoadDebugSymbols(Assembly assembly)
    {
        lock (_referencedAssemblies)
            if (!_referencedAssemblies.TryAdd(assembly, null)) return;

        // Load debug symbols for the given assembly
        if (assembly.IsDynamic ||
            string.IsNullOrEmpty(assembly.Location) ||
            !TryFindPdbFile(assembly, out var pdbFilePath))
        {
            // Skip this entry and avoid loading further symbols in the future
            return;
        }

        // Try find pdb source file
        using var stream = new FileStream(
            pdbFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        // Load assembly debug information
        var debugInformation = AssemblyDebugInformation.Load(assembly, stream);

        // Register result
        lock (_referencedAssemblies)
            _referencedAssemblies[assembly] = debugInformation;
    }

    /// <summary>
    /// Generates code for all methods given.
    /// </summary>
    /// <param name="context">The target context.</param>
    /// <param name="methods">Methods to generate code for.</param>
    /// <returns>True if code was generated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool GenerateCode(
        IRContext context,
        params IReadOnlyCollection<MethodBase> methods)
    {
        if (methods.Count < 1) return false;

        var queue = new ConcurrentQueue<MethodBase>();
        var processed = new HashSet<MethodBase>();

        // Generates code for the given method
        void GenerateCodeFor(MethodBase method)
        {
            lock (processed)
            {
                if (!processed.Add(method)) return;
            }

            // Get IR method and check for external declaration flags
            var irMethod = context.Declare(method, out _);
            if (irMethod.HasFlags(MethodFlags.External))
                return;

            // Retrieve disassembled method
            var disassembled = GetDisassembledMethod(method);
            if (disassembled is null)
                return;

            using var builder = irMethod.CreateBuilder();
            var codeGenerator = new CodeGenerator(
                context,
                builder,
                disassembled);
            codeGenerator.OnNewMethodCalled += (_, e) => queue.Enqueue(e);

            codeGenerator.GenerateCode();
            builder.Complete();
        }

        // Generate code for all entry point methods
        Parallel.ForEach(methods, GenerateCodeFor);

        // Process all remaining methods in parallel
        Parallel.For(0, queue.Count, _ =>
        {
            while (queue.TryDequeue(out var method))
                GenerateCodeFor(method);
        });

        return processed.Count > 0;
    }

    /// <summary>
    /// Generates code for all methods in the current scope.
    /// </summary>
    /// <param name="context">The target context.</param>
    /// <returns>True if code was generated.</returns>
    public bool GenerateCode(IRContext context)
    {
        // Declare all methods and register them
        var methods = PopScope();
        return GenerateCode(context, methods);
    }

    #endregion
}
