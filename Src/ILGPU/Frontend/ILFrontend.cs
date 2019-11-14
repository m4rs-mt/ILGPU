// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ILFrontend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.DebugInformation;
using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace ILGPU.Frontend
{
    /// <summary>
    /// The ILGPU MSIL frontend.
    /// </summary>
    public sealed class ILFrontend : DisposeBase
    {
        #region Nested Types

        /// <summary>
        /// Represents a single processing entry.
        /// </summary>
        private readonly struct ProcessingEntry
        {
            public ProcessingEntry(
                MethodBase method,
                CodeGenerationResult result)
            {
                Method = method;
                Result = result;
            }

            /// <summary>
            /// Returns the method.
            /// </summary>
            public MethodBase Method { get; }

            /// <summary>
            /// Returns the processing future.
            /// </summary>
            public CodeGenerationResult Result { get; }

            /// <summary>
            /// Returns true if this is an external processing request.
            /// </summary>
            public bool IsExternalRequest => Result != null;

            /// <summary>
            /// Signals the future with the given value.
            /// </summary>
            /// <param name="irFunction">The function value.</param>
            public void SetResult(Method irFunction)
            {
                Debug.Assert(irFunction != null, "Invalid function value");
                if (Result != null)
                    Result.Result = irFunction;
            }
        }

        #endregion

        #region Instance

        private volatile bool running = true;
        private readonly Thread[] threads;
        private readonly ManualResetEventSlim driverNotifier;
        private volatile int activeThreads = 0;
        private readonly object processingSyncObject = new object();
        private readonly Stack<ProcessingEntry> processing =
            new Stack<ProcessingEntry>(1 << 6);

        private volatile CodeGenerationPhase codeGenerationPhase;

        /// <summary>
        /// Constructs a new frontend with two threads.
        /// </summary>
        /// <param name="debugInformationManager">The associated debug information manager.</param>
        public ILFrontend(DebugInformationManager debugInformationManager)
            : this(debugInformationManager, 2)
        { }

        /// <summary>
        /// Constructs a new frontend that uses the given number of
        /// threads for code generation.
        /// </summary>
        /// <param name="debugInformationManager">The associated debug information manager.</param>
        /// <param name="numThreads">The number of threads.</param>
        public ILFrontend(DebugInformationManager debugInformationManager, int numThreads)
        {
            if (numThreads < 1)
                throw new ArgumentOutOfRangeException(nameof(numThreads));
            DebugInformationManager = debugInformationManager;
            driverNotifier = new ManualResetEventSlim(false);
            threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                var thread = new Thread(DoWork)
                {
                    Name = "ILFrontendWorker" + i,
                    IsBackground = true,
                };
                threads[i] = thread;
                thread.Start();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated debug information manager (if any).
        /// </summary>
        public DebugInformationManager DebugInformationManager { get; }

        #endregion

        #region Methods

        /// <summary>
        /// The code-generation thread.
        /// </summary>
        private void DoWork()
        {
            var detectedMethods = new HashSet<MethodBase>();
            for (; ; )
            {
                ProcessingEntry current;
                lock (processingSyncObject)
                {
                    while (processing.Count < 1 & running)
                        Monitor.Wait(processingSyncObject);

                    if (!running)
                        break;

                    ++activeThreads;
                    current = processing.Pop();
                }

                Debug.Assert(codeGenerationPhase != null, "Invalid processing state");

                detectedMethods.Clear();
                codeGenerationPhase.GenerateCodeInternal(
                    current.Method,
                    current.IsExternalRequest,
                    detectedMethods,
                    out Method method);
                current.SetResult(method);

                // Check dependencies
                lock (processingSyncObject)
                {
                    --activeThreads;

                    foreach (var detectedMethod in detectedMethods)
                        processing.Push(new ProcessingEntry(detectedMethod, null));

                    if (detectedMethods.Count > 0)
                        Monitor.PulseAll(processingSyncObject);
                    else
                    {
                        if (activeThreads == 0 && processing.Count < 1)
                            driverNotifier.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Internal method used for code generation.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The generation future.</returns>
        internal CodeGenerationResult GenerateCode(MethodBase method)
        {
            var result = new CodeGenerationResult(method);
            lock (processingSyncObject)
            {
                driverNotifier.Reset();
                processing.Push(new ProcessingEntry(method, result));
                Monitor.Pulse(processingSyncObject);
            }
            return result;
        }

        /// <summary>
        /// Starts a code-generation phase.
        /// </summary>
        /// <param name="context">The target IR context.</param>
        /// <returns>The created code-generation phase.</returns>
        public CodeGenerationPhase BeginCodeGeneration(IRContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var newPhase = new CodeGenerationPhase(this, context);
            if (Interlocked.CompareExchange(ref codeGenerationPhase, newPhase, null) != null)
                throw new InvalidOperationException();
            driverNotifier.Reset();
            return newPhase;
        }

        /// <summary>
        /// Finishes the current code-generation phase.
        /// </summary>
        /// <param name="phase">The current phase.</param>
        internal void FinishCodeGeneration(CodeGenerationPhase phase)
        {
            Debug.Assert(phase != null, "Invalid phase");

            Debug.WriteLineIf(!phase.HadWorkToDo, "This code generation phase had nothing to do");
            if (phase.HadWorkToDo)
                driverNotifier.Wait();

            if (Interlocked.CompareExchange(ref codeGenerationPhase, null, phase) != phase)
                throw new InvalidOperationException();
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                running = false;
                lock (processingSyncObject)
                    Monitor.PulseAll(processingSyncObject);
                foreach (var thread in threads)
                    thread.Join();
                driverNotifier.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Represents a code-generation future.
    /// </summary>
    public sealed class CodeGenerationResult
    {
        /// <summary>
        /// Creates a new code generation result.
        /// </summary>
        /// <param name="method">The associated method.</param>
        internal CodeGenerationResult(MethodBase method)
        {
            Debug.Assert(method != null, "Invalid method");
            Method = method;
        }

        /// <summary>
        /// Returns the associated method.
        /// </summary>
        public MethodBase Method { get; }

        /// <summary>
        /// The associated function result.
        /// </summary>
        public Method Result { get; internal set; }

        /// <summary>
        /// Returns the associated function handle.
        /// </summary>
        public MethodHandle ResultHandle => Result.Handle;

        /// <summary>
        /// Returns true if this result has a function value.
        /// </summary>
        public bool HasResult => Result != null;
    }

    /// <summary>
    /// A single code generation phase.
    /// Note that only a single phase instance can be created at a time.
    /// </summary>
    public sealed class CodeGenerationPhase : DisposeBase
    {
        #region Instance

        private volatile bool isFinished = false;
        private volatile bool hadWorkToDo = false;

        /// <summary>
        /// Constructs a new generation phase.
        /// </summary>
        /// <param name="frontend">The current frontend instance.</param>
        /// <param name="context">The target IR context.</param>
        internal CodeGenerationPhase(ILFrontend frontend, IRContext context)
        {
            Debug.Assert(frontend != null, "Invalid frontend");
            Debug.Assert(context != null, "Invalid context");

            Context = context;
            Frontend = frontend;
            DebugInformationManager = frontend.DebugInformationManager;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public IRContext Context { get; }

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public ILFrontend Frontend { get; }

        /// <summary>
        /// Returns the associated debug information manager (if any).
        /// </summary>
        public DebugInformationManager DebugInformationManager { get; }

        /// <summary>
        /// Returns true if the generation phase has been finished.
        /// </summary>
        public bool IsFinished => isFinished;

        /// <summary>
        /// Returns true if the code generation phase had work to do.
        /// </summary>
        public bool HadWorkToDo => hadWorkToDo;

        #endregion

        #region Methods

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="methodDeclaration">The method declaration.</param>
        /// <returns>The declared method.</returns>
        internal Method DeclareMethod(MethodDeclaration methodDeclaration) =>
            Context.Declare(methodDeclaration, out bool _);

        /// <summary>
        /// Declares a method.
        /// </summary>
        /// <param name="methodDeclaration">The method declaration.</param>
        /// <param name="created">True, iff the method has been created.</param>
        /// <returns>The declared method.</returns>
        internal Method DeclareMethod(
            MethodDeclaration methodDeclaration,
            out bool created) =>
            Context.Declare(methodDeclaration, out created);

        /// <summary>
        /// Performs the actual (async) code generation.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="isExternalRequest">True, if processing of this method was requested by a user.</param>
        /// <param name="detectedMethods">The set of newly detected methods.</param>
        /// <param name="generatedMethod">The resolved IR method.</param>
        internal void GenerateCodeInternal(
            MethodBase method,
            bool isExternalRequest,
            HashSet<MethodBase> detectedMethods,
            out Method generatedMethod)
        {
            generatedMethod = Context.Declare(method, out bool created);
            if (!created & isExternalRequest)
                return;

            SequencePointEnumerator sequencePoints =
                DebugInformationManager?.LoadSequencePoints(method) ?? SequencePointEnumerator.Empty;
            var disassembler = new Disassembler(method, sequencePoints);
            var disassembledMethod = disassembler.Disassemble();

            using (var builder = generatedMethod.CreateBuilder())
            {
                var codeGenerator = new CodeGenerator(
                    Frontend,
                    builder,
                    disassembledMethod,
                    detectedMethods);
                codeGenerator.GenerateCode();
            }

            // Evaluate inlining heuristic to adjust method declaration
            Inliner.SetupInliningAttributes(
                Context,
                generatedMethod,
                disassembledMethod);
        }

        /// <summary>
        /// Generates code for the given method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>A completion future.</returns>
        public CodeGenerationResult GenerateCode(MethodBase method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            hadWorkToDo = true;
            return Frontend.GenerateCode(method);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isFinished = true;
                Frontend.FinishCodeGeneration(this);
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
