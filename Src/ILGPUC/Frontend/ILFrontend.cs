// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
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
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1031 // Do not catch general exception types

namespace ILGPUC.Frontend;

/// <summary>
/// Per-sub-phase timing breakdown of <see cref="ILFrontend.LoadMethods"/>.
/// </summary>
internal struct LoadMethodsTimings
{
    /// <summary>Total time spent inside <c>DisassembleMethods</c>.</summary>
    public double DisassembleTotalMs;

    /// <summary>Aggregated <c>Intrinsics.TryImplement</c> time across all
    /// processed methods (worker-thread sum, not wall clock).</summary>
    public double IntrinsicResolveMs;

    /// <summary>Number of <c>Intrinsics.TryImplement</c> invocations.</summary>
    public long IntrinsicResolveCount;

    /// <summary>Aggregated <c>Disassembler.TryDisassemble</c> time across
    /// all methods (worker-thread sum, not wall clock).</summary>
    public double RawDisassembleMs;

    /// <summary>Number of <c>Disassembler.TryDisassemble</c> invocations.</summary>
    public long RawDisassembleCount;

    /// <summary>Total methods discovered (entry + transitive).</summary>
    public int MethodsDiscovered;

    /// <summary>Distinct assemblies whose PDBs were probed.</summary>
    public int AssembliesScanned;

    /// <summary>Time loading PDB streams + parsing debug metadata.</summary>
    public double LoadDebugSymbolsMs;

    /// <summary>Time attaching sequence-point locations to instructions.</summary>
    public double AttachSequencePointsMs;
}

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
    /// Optional cross-compilation cache shared across all <see cref="ILFrontend"/>
    /// instances created by a single <see cref="ILGPUC.KernelCompiler"/>. When
    /// non-null, disassembled bodies and loaded PDBs are looked up here first
    /// and only computed on a miss; when null the frontend behaves as before.
    /// </summary>
    private readonly ILFrontendCache? _cache;

    /// <summary>
    /// Constructs a new IL frontend using the given paths to resolve debug
    /// information. The frontend operates in standalone (uncached) mode.
    /// </summary>
    /// <param name="backendType">The current backend type we are compiling for.</param>
    /// <param name="pdbSearchPaths">The list of search paths.</param>
    public ILFrontend(BackendType backendType, params List<string?> pdbSearchPaths)
        : this(backendType, cache: null, pdbSearchPaths) { }

    /// <summary>
    /// Constructs a new IL frontend that shares disassembly + PDB caches with
    /// every other frontend constructed against the same
    /// <paramref name="cache"/>. Pass <see langword="null"/> for the legacy,
    /// uncached behaviour.
    /// </summary>
    /// <param name="backendType">The current backend type we are compiling for.</param>
    /// <param name="cache">
    /// Optional shared cache; typically owned by the calling
    /// <see cref="ILGPUC.KernelCompiler"/> so that all kernel compilations in
    /// one CLI invocation reuse parsed BCL bodies and loaded PDBs.
    /// </param>
    /// <param name="pdbSearchPaths">The list of search paths.</param>
    public ILFrontend(
        BackendType backendType,
        ILFrontendCache? cache,
        params List<string?> pdbSearchPaths)
    {
        BackendType = backendType;
        _cache = cache;

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

        // Process all methods and get debug info assigned to them.
        // TryClaimLocationAttachment skips bodies whose instructions were
        // already location-tagged by an earlier compile that shared the same
        // cached DisassembledMethod — sequence-point merging is value-
        // deterministic for a given PDB so reattaching is wasted work.
        Parallel.ForEach(_methods, method =>
        {
            if (method.Value is null) return;
            if (!method.Value.TryClaimLocationAttachment()) return;

            var sequencePoints = LoadSequencePoints(method.Key);
            if (!sequencePoints.IsValid) return;

            foreach (var instruction in method.Value)
                instruction.UpdateLocation(sequencePoints);
        });
    }

    /// <summary>
    /// Instrumented variant of <see cref="LoadMethods"/> that records the
    /// elapsed time of each constituent sub-phase and the number of methods
    /// processed at each stage. Intended for profiling / benchmarking only.
    /// </summary>
    /// <param name="methods">Methods to load.</param>
    /// <param name="timings">Receives the per-sub-phase timing breakdown.</param>
    internal void LoadMethodsInstrumented(
        IReadOnlyCollection<MethodBase> methods,
        out LoadMethodsTimings timings)
    {
        timings = default;
        var sw = Stopwatch.StartNew();

        // Sub-phase 1: disassemble entry methods + transitive walk.
        // Within ProcessMethod we accumulate finer-grained counters into
        // thread-static fields so the bench can split disassembly time
        // between intrinsic resolution and actual IL parsing.
        var prev = s_collectSubCounters;
        s_collectSubCounters = true;
        s_intrinsicNs = 0;
        s_intrinsicCount = 0;
        s_disassembleNs = 0;
        s_disassembleCount = 0;
        try
        {
            DisassembleMethods(methods);
        }
        finally
        {
            s_collectSubCounters = prev;
        }
        sw.Stop();
        timings.DisassembleTotalMs = sw.Elapsed.TotalMilliseconds;
        timings.IntrinsicResolveMs = s_intrinsicNs / 1_000_000.0;
        timings.IntrinsicResolveCount = s_intrinsicCount;
        timings.RawDisassembleMs = s_disassembleNs / 1_000_000.0;
        timings.RawDisassembleCount = s_disassembleCount;
        timings.MethodsDiscovered = _methods.Count;

        // Sub-phase 2: per-assembly PDB load.
        var assemblies = _methods.Keys
            .Select(t => t.Module.Assembly)
            .Distinct()
            .ToArray();
        timings.AssembliesScanned = assemblies.Length;

        sw.Restart();
        Parallel.ForEach(assemblies, LoadDebugSymbols);
        sw.Stop();
        timings.LoadDebugSymbolsMs = sw.Elapsed.TotalMilliseconds;

        // Sub-phase 3: sequence-point attachment for each disassembled method.
        // The TryClaimLocationAttachment gate matches the production
        // LoadMethods path so warm runs measured by the bench correctly
        // reflect the no-op cost.
        sw.Restart();
        Parallel.ForEach(_methods, method =>
        {
            if (method.Value is null) return;
            if (!method.Value.TryClaimLocationAttachment()) return;

            var sequencePoints = LoadSequencePoints(method.Key);
            if (!sequencePoints.IsValid) return;
            foreach (var instruction in method.Value)
                instruction.UpdateLocation(sequencePoints);
        });
        sw.Stop();
        timings.AttachSequencePointsMs = sw.Elapsed.TotalMilliseconds;
    }

    // Process-wide accumulators populated only when running through
    // LoadMethodsInstrumented. Production LoadMethods leaves the gate at
    // false so the timing branches in ProcessMethod compile down to no-ops
    // on the hot path. Counters are touched via Interlocked from parallel
    // worker threads; not safe for concurrent benches in the same process.
    private static volatile bool s_collectSubCounters;
    private static long s_intrinsicNs;
    private static long s_intrinsicCount;
    private static long s_disassembleNs;
    private static long s_disassembleCount;

    private static readonly double s_nanosPerTick =
        1_000_000_000.0 / Stopwatch.Frequency;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ToNanos(long elapsedTicks) =>
        (long)(elapsedTicks * s_nanosPerTick);

    /// <summary>
    /// Disassembles all methods and tracks nested/referenced methods to be disassembled.
    /// </summary>
    /// <param name="methods">All methods to be disassembled in full.</param>
    private void DisassembleMethods(IReadOnlyCollection<MethodBase> methods)
    {
        var queue = new ConcurrentQueue<MethodBase>();
        var remapping = new Dictionary<MethodBase, MethodBase>(capacity: 128);

        // Process all kernels in parallel
        Parallel.ForEach(methods, method =>
            ProcessMethod(method, queue, remapping));

        // Process all remaining methods in parallel
        Parallel.For(0, queue.Count, _ =>
        {
            while (queue.TryDequeue(out var method))
                ProcessMethod(method, queue, remapping);
        });

        // Map all intrinsic implementations to original methods.
        // Note: _methods[target] may be null for Generated intrinsics that have
        // no disassembled body. A simple assignment (instead of the old busy-wait
        // while loop) avoids spinning forever in that case.
        foreach (var (source, target) in remapping)
            _methods[source] ??= _methods[target];
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
        bool collect = s_collectSubCounters;
        long intrinsicStart = collect ? Stopwatch.GetTimestamp() : 0;
        var intrinsicKind = Intrinsics.TryImplement(
            sourceLocation,
            method,
            BackendType,
            out var impl);
        if (collect)
        {
            Interlocked.Add(
                ref s_intrinsicNs,
                ToNanos(Stopwatch.GetTimestamp() - intrinsicStart));
            Interlocked.Increment(ref s_intrinsicCount);
        }
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
            // Disassemble our new method. Transitively discovered methods
            // (e.g. BCL internals) may contain unsupported IL instructions or
            // unresolvable tokens — treat them as non-disassemblable.
            //
            // The cross-compilation cache is consulted at this single
            // call site, keyed by the method that is actually parsed.
            // Aliasing introduced later by intrinsic remapping
            // (`_methods[source] ??= _methods[target]`) stays per-instance
            // so that future backend-specific intrinsic implementations
            // continue to resolve correctly per backend.
            DisassembledMethod? disassembled;
            if (_cache is not null &&
                _cache.Disassembly.TryGetValue(method, out var cached))
            {
                if (cached.Status == DisassembleStatus.Failed ||
                    cached.Body is null)
                    return;
                disassembled = cached.Body;
            }
            else
            {
                long disStart = collect ? Stopwatch.GetTimestamp() : 0;
                try
                {
                    disassembled = Disassembler.TryDisassemble(method);
                }
                catch (Exception)
                {
                    if (collect)
                    {
                        Interlocked.Add(
                            ref s_disassembleNs,
                            ToNanos(Stopwatch.GetTimestamp() - disStart));
                        Interlocked.Increment(ref s_disassembleCount);
                    }
                    // Transitively discovered methods may fail to disassemble
                    // due to unsupported IL, unresolvable tokens, etc. The
                    // code generator will simply skip methods without a
                    // disassembled body. Memoize the failure so warm compiles
                    // in the same session don't re-throw.
                    _cache?.Disassembly.TryAdd(
                        method,
                        new DisassemblyCacheEntry(null, DisassembleStatus.Failed));
                    return;
                }
                if (collect)
                {
                    Interlocked.Add(
                        ref s_disassembleNs,
                        ToNanos(Stopwatch.GetTimestamp() - disStart));
                    Interlocked.Increment(ref s_disassembleCount);
                }

                if (disassembled is null)
                {
                    _cache?.Disassembly.TryAdd(
                        method,
                        new DisassemblyCacheEntry(null, DisassembleStatus.Failed));
                    return;
                }

                // Publish to the shared cache for later kernel compiles in the
                // same KernelCompiler session.
                _cache?.Disassembly.TryAdd(
                    method,
                    new DisassemblyCacheEntry(
                        disassembled, DisassembleStatus.Disassembled));
            }

            // Add method to internal methods
            lock (_methods) _methods[method] = disassembled;

            // Determine all called methods to disassemble.
            // For constrained virtual calls (interface dispatch on structs),
            // also resolve and enqueue the concrete implementation so it
            // gets disassembled before code generation needs it.
            foreach (var instruction in disassembled.Instructions)
            {
                if (instruction.Argument is MethodBase calledMethod)
                {
                    queue.Enqueue(calledMethod);

                    // Resolve constrained interface calls to concrete methods
                    if (instruction.HasFlags(ILInstructionFlags.Constrained)
                        && instruction.FlagsContext.Argument is Type constrainedType
                        && calledMethod is MethodInfo calledMethodInfo
                        && calledMethodInfo.IsVirtual
                        && calledMethodInfo.DeclaringType?.IsInterface == true)
                    {
                        try
                        {
                            var mapping = constrainedType.GetInterfaceMap(
                                calledMethodInfo.DeclaringType);
                            var genericDef = calledMethodInfo.IsGenericMethod
                                ? calledMethodInfo.GetGenericMethodDefinition()
                                : calledMethodInfo;
                            for (int j = 0; j < mapping.InterfaceMethods.Length; j++)
                            {
                                if (mapping.InterfaceMethods[j] == genericDef)
                                {
                                    var concrete = mapping.TargetMethods[j];
                                    if (calledMethodInfo.IsGenericMethod)
                                    {
                                        concrete = concrete.MakeGenericMethod(
                                            calledMethodInfo.GetGenericArguments());
                                    }
                                    queue.Enqueue(concrete);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Interface mapping failed — skip
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loads all debug symbols of all referenced assemblies.
    /// </summary>
    /// <remarks>
    /// Per-assembly results are stored both in the per-instance
    /// <see cref="_referencedAssemblies"/> dictionary (so that
    /// <see cref="TryLoadDebugInformation"/> stays a simple lookup) and in
    /// the shared <see cref="ILFrontendCache.Pdb"/> when one was supplied.
    /// On a warm compile in the same <see cref="ILGPUC.KernelCompiler"/>
    /// session, the shared cache returns an existing <see cref="Lazy{T}"/>
    /// and zero PDB I/O is performed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void LoadDebugSymbols(Assembly assembly)
    {
        lock (_referencedAssemblies)
            if (!_referencedAssemblies.TryAdd(assembly, null)) return;

        AssemblyDebugInformation? debugInformation;
        if (_cache is not null)
        {
            // Shared cache path: GetOrAdd-with-Lazy ensures concurrent
            // first-time loads of the same assembly only open and parse
            // the PDB once across all frontends in this KernelCompiler.
            var lazy = _cache.Pdb.GetOrAdd(
                assembly,
                a => new Lazy<AssemblyDebugInformation?>(
                    () => LoadDebugInformationFromDisk(a),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            debugInformation = lazy.Value;
        }
        else
        {
            debugInformation = LoadDebugInformationFromDisk(assembly);
        }

        if (debugInformation is null)
            return;

        lock (_referencedAssemblies)
            _referencedAssemblies[assembly] = debugInformation;
    }

    /// <summary>
    /// Performs the actual on-disk PDB lookup and metadata parse for
    /// <paramref name="assembly"/>. Returns <see langword="null"/> when no
    /// PDB is available — matching the legacy behaviour of leaving
    /// <c>_referencedAssemblies[assembly]</c> at <see langword="null"/>.
    /// </summary>
    private AssemblyDebugInformation? LoadDebugInformationFromDisk(Assembly assembly)
    {
        if (assembly.IsDynamic ||
            string.IsNullOrEmpty(assembly.Location) ||
            !TryFindPdbFile(assembly, out var pdbFilePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                pdbFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return AssemblyDebugInformation.Load(assembly, stream);
        }
        catch (Exception)
        {
            // Locked file, corrupt PDB, etc. — fall back to "no debug info"
            // and let downstream code use Location.Unknown.
            return null;
        }
    }

    /// <summary>
    /// Generates code for all methods given.
    /// </summary>
    /// <param name="moduleBuilder">The target context.</param>
    /// <param name="methods">Methods to generate code for.</param>
    /// <returns>True if code was generated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool GenerateCode(
        ModuleBuilder moduleBuilder,
        IReadOnlyCollection<MethodBase> methods)
    {
        if (methods.Count < 1) return false;

        var queue = new Queue<MethodBase>(capacity: 32);
        var processed = new HashSet<MethodBase>();

        // Generates code for the given method
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        void GenerateCodeFor(MethodBase method, bool isEntryPoint = false)
        {
            if (!processed.Add(method))
                return;

            // Get IR method and check for external declaration flags
            var declaration = moduleBuilder.CreateMethodDeclaration(method);
            var methodBuilder = moduleBuilder.GetOrCreateMethod(declaration);
            if (methodBuilder.Method.IsExternal)
                return;

            // Retrieve disassembled method. If not available, try on-the-fly
            // disassembly — this handles methods discovered through
            // devirtualization or intrinsic handler reflection during code
            // generation that were never in the initial DisassembleMethods
            // queue (e.g., IRadixSortOperation.ExtractRadixBits resolved
            // via reflection in the RadixSort intrinsic handler). Consult
            // the shared cache before parsing IL so warm compiles skip the
            // re-disassembly.
            var disassembled = GetDisassembledMethod(method);
            if (disassembled is null && _cache is not null &&
                _cache.Disassembly.TryGetValue(method, out var ondemandCached) &&
                ondemandCached.Status == DisassembleStatus.Disassembled)
            {
                disassembled = ondemandCached.Body;
            }
            if (disassembled is null)
            {
                try
                {
                    disassembled = Disassembler.TryDisassemble(method);
                }
                catch (Exception)
                {
                    // Fall through to External marking below
                }

                if (disassembled is not null)
                {
                    _cache?.Disassembly.TryAdd(
                        method,
                        new DisassemblyCacheEntry(
                            disassembled, DisassembleStatus.Disassembled));
                    lock (_methods) _methods[method] = disassembled;

                    // Enqueue transitive dependencies so they also get
                    // disassembled and compiled.
                    foreach (var instruction in disassembled.Instructions)
                    {
                        if (instruction.Argument is MethodBase calledMethod)
                            queue.Enqueue(calledMethod);
                    }
                }
                else
                {
                    methodBuilder.Method.AddFlags(MethodFlags.External);
                    return;
                }
            }

            try
            {
                var codeGenerator = new CodeGenerator(methodBuilder, disassembled);
                codeGenerator.OnNewMethodCalled += (_, e) => queue.Enqueue(e);

                codeGenerator.GenerateCode();
                methodBuilder.Seal();
            }
            catch (Exception) when (!isEntryPoint)
            {
                // Transitively called methods may use types unsupported in GPU IR
                // or hit other compilation issues. Mark them as external and
                // continue.
                methodBuilder.Method.AddFlags(MethodFlags.External);
            }
        }

        // Generate code for all entry point methods
        foreach (var method in methods)
            GenerateCodeFor(method, isEntryPoint: true);

        // Process all remaining methods
        while (queue.TryDequeue(out var method))
            GenerateCodeFor(method);

        return processed.Count > 0;
    }

    /// <summary>
    /// Generates code for all methods in the current scope.
    /// </summary>
    /// <param name="builder">The target builder.</param>
    /// <returns>True if code was generated.</returns>
    public bool GenerateCode(ModuleBuilder builder)
    {
        // Declare all methods and register them
        var methods = PopScope();
        return GenerateCode(builder, methods);
    }

    #endregion
}

#pragma warning restore CA1031 // Do not catch general exception types
