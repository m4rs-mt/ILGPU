// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents a function generator for main kernel functions.
    /// </summary>
    sealed class CLKernelFunctionGenerator : CLCodeGenerator
    {
        #region Constants

        /// <summary>
        /// The string format of a kernel-view parameter name.
        /// </summary>
        public const string KernelViewNameFormat = "view_{0}";

        /// <summary>
        /// The parameter name of dynamic shared memory parameter.
        /// </summary>
        public const string DynamicSharedMemoryParamName = "dynamic_shared_memory";

        /// <summary>
        /// The parameter name of dynamic shared memory length parameter.
        /// </summary>
        public const string DynamicSharedMemoryLengthParamName =
            "dynamic_shared_memory_length";

        #endregion

        #region Nested Types

        /// <summary>
        /// A specialized kernel setup logic for parameters.
        /// </summary>
        private struct KernelParameterSetupLogic : IParametersSetupLogic
        {
            /// <summary>
            /// Constructs a new specialized kernel setup logic.
            /// </summary>
            /// <param name="generator">The parent generator.</param>
            public KernelParameterSetupLogic(CLKernelFunctionGenerator generator)
            {
                Parent = generator;

                IndexVariable = null;
                LengthVariable = null;
            }

            /// <summary>
            /// Returns the main index variable.
            /// </summary>
            public Variable IndexVariable { get; private set; }

            /// <summary>
            /// Returns the length variable of implicitly grouped kernels.
            /// </summary>
            public Variable LengthVariable { get; private set; }

            /// <summary>
            /// Returns the parent type generator.
            /// </summary>
            public CLKernelFunctionGenerator Parent { get; }

            /// <summary>
            /// Returns the associated kernel type.
            /// </summary>
            public string GetParameterType(Parameter parameter) =>
                Parent.KernelTypeGenerator[parameter];

            /// <summary>
            /// Updates index and length variables.
            /// </summary>
            public Variable HandleIntrinsicParameter(
                int parameterOffset,
                Parameter parameter)
            {
                if (!Parent.EntryPoint.IsExplicitlyGrouped)
                {
                    IndexVariable = Parent.Allocate(parameter);

                    // This is an implicitly grouped kernel that needs boundary
                    // information to avoid out-of-bounds dispatches
                    // (See also PTXKernelFunctionGenerator)
                    LengthVariable = Parent.AllocateType(parameter.ParameterType);
                }

                return LengthVariable;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// All globally accessible shared allocations inside the kernel module.
        /// </summary>
        private readonly List<AllocaInformation> globallySharedAllocations;

        /// <summary>
        /// Creates a new OpenCL function generator.
        /// </summary>
        /// <param name="args">The generation arguments.</param>
        /// <param name="method">The current method.</param>
        /// <param name="allocas">All local allocas.</param>
        public CLKernelFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        {
            EntryPoint = args.EntryPoint;
            KernelTypeGenerator = args.KernelTypeGenerator;
            DynamicSharedAllocations = args.DynamicSharedAllocations;

            // Analyze and create required kernel interop types first
            foreach (var param in Method.Parameters)
                KernelTypeGenerator.Register(param);

            // Gather all globally shared allocations that need to be tracked
            globallySharedAllocations = new List<AllocaInformation>(
                args.SharedAllocations.Length +
                args.DynamicSharedAllocations.Length);
            foreach (var allocaInfo in args.SharedAllocations)
            {
                if (allocas.SharedAllocations.Contains(allocaInfo.Alloca))
                    continue;
                globallySharedAllocations.Add(allocaInfo);
            }
            globallySharedAllocations.AddRange(
                args.DynamicSharedAllocations.Allocas);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated entry point.
        /// </summary>
        public SeparateViewEntryPoint EntryPoint { get; }

        /// <summary>
        /// The current kernel type generator.
        /// </summary>
        public CLKernelTypeGenerator KernelTypeGenerator { get; }

        /// <summary>
        /// All dynamic shared memory allocations.
        /// </summary>
        public AllocaKindInformation DynamicSharedAllocations { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a function declaration in OpenCL code.
        /// </summary>
        public override void GenerateHeader(StringBuilder builder)
        {
            // Emit shared-memory declarations for all functions
            var addressSpacePrefix = CLInstructions.GetAddressSpacePrefix(
                MemoryAddressSpace.Shared);
            foreach (var allocaInfo in globallySharedAllocations)
            {
                builder.Append(addressSpacePrefix);
                builder.Append(' ');
                builder.Append(TypeGenerator[allocaInfo.ElementType]);
                builder.Append(CLInstructions.DereferenceOperation);
                builder.Append(' ');
                builder.Append(GetSharedMemoryAllocationName(allocaInfo));
                builder.AppendLine(";");
                if (allocaInfo.IsDynamicArray)
                {
                    builder.Append(TypeGenerator[allocaInfo.Alloca.ArrayLength.Type]);
                    builder.Append(' ');
                    builder.Append(GetSharedMemoryAllocationLengthName(allocaInfo));
                    builder.AppendLine(";");
                }
            }
        }

        /// <summary>
        /// Generates OpenCL code.
        /// </summary>
        public override void GenerateCode()
        {
            // Emit kernel declaration and parameter definitions
            Builder.Append("kernel void ");
            Builder.Append(CLCompiledKernel.EntryName);
            Builder.AppendLine("(");

            // Initialize view information
            var viewParameters = EntryPoint.ViewParameters;
            bool hasDefaultParameters = EntryPoint.Parameters.Count > 0;

            // Check for dynamic shared memory
            if (DynamicSharedAllocations.Length > 0)
            {
                // We need a single pointer to local memory of byte elements
                Builder.Append("\tlocal ");
                Builder.Append(
                    TypeGenerator.GetBasicValueType(
                        ArithmeticBasicValueType.Int8));
                Builder.Append(CLInstructions.DereferenceOperation);
                Builder.Append(' ');
                Builder.Append(DynamicSharedMemoryParamName);

                // Append the length of the memory in bytes.
                Builder.AppendLine(",\t");
                Builder.Append(
                    TypeGenerator.GetBasicValueType(
                        ArithmeticBasicValueType.Int32));
                Builder.Append(' ');
                Builder.Append(DynamicSharedMemoryLengthParamName);

                if (hasDefaultParameters || viewParameters.Length > 0)
                    Builder.AppendLine(",");
            }

            // Note that we have to emit custom parameters for every view argument
            // since views have to be mapped by the driver to kernel arguments.
            for (int i = 0, e = viewParameters.Length; i < e; ++i)
            {
                // Emit a specialized pointer type
                var elementType = viewParameters[i].ElementType;
                Builder.Append("\tglobal ");
                Builder.Append(TypeGenerator[elementType]);
                Builder.Append(CLInstructions.DereferenceOperation);
                Builder.Append(' ');
                Builder.AppendFormat(KernelViewNameFormat, i.ToString());

                if (hasDefaultParameters || i + 1 < e)
                    Builder.AppendLine(",");
            }

            // Emit all parameter declarations
            var setupLogic = new KernelParameterSetupLogic(this);
            SetupParameters(
                Builder,
                ref setupLogic,
                EntryPoint.KernelIndexParameterOffset);
            Builder.AppendLine(")");

            // Emit code that moves view arguments into their appropriate targets
            Builder.AppendLine("{");
            PushIndent();
            GenerateArgumentMapping();

            // Emit index computation
#if DEBUG
            Builder.AppendLine();
            Builder.AppendLine("\t// Kernel indices");
            Builder.AppendLine();
#endif
            SetupKernelIndex(setupLogic.IndexVariable, setupLogic.LengthVariable);

            // Emit shared-memory allocations for all functions
            SetupAllocations(Allocas.SharedAllocations, MemoryAddressSpace.Shared);
            foreach (var allocaInfo in globallySharedAllocations)
            {
                // Skip dynamic allocations here since we need special setup code
                if (DynamicSharedAllocations.Contains(allocaInfo.Alloca))
                    continue;

                var allocationVariable = DeclareAllocation(
                    allocaInfo,
                    MemoryAddressSpace.Shared);

                // We have to bind the local allocations to the shared variables in
                // order to make them visible to all other functions in this module
                using var statement = new StatementEmitter(this);
                statement.AppendTarget(
                    GetSharedMemoryAllocationVariable(allocaInfo),
                    newTarget: false);
                statement.Append(allocationVariable);
            }

            // Emit special dynamic shared memory "allocations"
            foreach (var dynamicAllocaInfo in DynamicSharedAllocations)
            {
                // We have to bind the local allocations to the shared variables in
                // order to make them visible to all other functions in this module
                using (var statement = new StatementEmitter(this))
                {
                    statement.AppendTarget(
                        GetSharedMemoryAllocationVariable(dynamicAllocaInfo),
                        newTarget: false);
                    statement.AppendCast(dynamicAllocaInfo.Alloca.Type);
                    statement.AppendCommand(DynamicSharedMemoryParamName);
                }
                using (var statement = new StatementEmitter(this))
                {
                    statement.AppendTarget(
                        GetSharedMemoryAllocationLengthVariable(dynamicAllocaInfo),
                        newTarget: false);
                    statement.AppendCast(dynamicAllocaInfo.Alloca.ArrayLength.Type);
                    statement.AppendCommand(DynamicSharedMemoryLengthParamName);
                }
            }

            // Bind all dynamic shared memory allocations
            BindSharedMemoryAllocation(Allocas.DynamicSharedAllocations);

            // Generate code
            GenerateCodeInternal();
            PopIndent();
            Builder.AppendLine("}");
        }

        /// <summary>
        /// Generates code that wires kernel-specific arguments into internal arguments.
        /// </summary>
        private void GenerateArgumentMapping()
        {
#if DEBUG
            Builder.AppendLine("\t// Map parameters");
            Builder.AppendLine();
#endif
            int paramOffset = EntryPoint.KernelIndexParameterOffset;
            var parameters = Method.Parameters;
            for (
                int paramIdx = paramOffset, numParams = parameters.Count;
                paramIdx < numParams;
                ++paramIdx)
            {
                var param = parameters[paramIdx];
                var sourceVariable = Load(param);

                // Check whether we need to create a custom mapping
                if (!EntryPoint.TryGetViewParameters(
                    param.Index - paramOffset,
                    out var viewMapping))
                {
                    continue;
                }
                Debug.Assert(
                    viewMapping.Count > 0,
                    "There must be at least one view entry");

                // We require a custom mapping step
                var targetVariable = AllocateType(param.Type);
                Declare(targetVariable);
                Bind(param, targetVariable);

                // The current type must be a structure type
                var structureType = param.Type.As<StructureType>(param.Location);

                // Map each field
                for (
                    int i = 0, specialParamIdx = 0, e = structureType.NumFields;
                    i < e;
                    ++i)
                {
                    var access = new FieldAccess(i);
                    // Check whether the current field is a nested view pointer
                    if (specialParamIdx < viewMapping.Count &&
                        structureType[access].IsPointerType)
                    {
                        var viewIndex = viewMapping[specialParamIdx].Index;

                        // Map the view pointer
                        using (var statement = BeginStatement(targetVariable, access))
                        {
                            statement.AppendCast(structureType[access]);
                            statement.AppendOperation(
                                string.Format(
                                    KernelViewNameFormat,
                                    viewIndex.ToString()));
                            statement.AppendOperation(
                                CLInstructions.GetArithmeticOperation(
                                    BinaryArithmeticKind.Add,
                                    false,
                                    out var _));

                            statement.Append(sourceVariable);
                            statement.AppendField(access);
                        }

                        // Map the length
                        var lengthAccess = access.Add(1);
                        using (var statement = BeginStatement(
                            targetVariable,
                            lengthAccess))
                        {
                            statement.Append(sourceVariable);
                            statement.AppendField(lengthAccess);
                        }

                        // Move special index and field index
                        // (since we have assigned two fields)
                        ++specialParamIdx;
                        ++i;
                    }
                    else
                    {
                        // Map the field
                        using var statement = BeginStatement(targetVariable, access);
                        statement.Append(sourceVariable);
                        statement.AppendField(access);
                    }
                }
            }
        }

        /// <summary>
        /// Emits an implicit kernel index computation.
        /// </summary>
        /// <param name="indexVariable">The index variable to write to.</param>
        /// <param name="boundsVariable">The associated bounds variable.</param>
        /// <param name="fieldAccess">The access chain to use.</param>
        /// <param name="dimension">The parameter dimension.</param>
        private void EmitImplicitKernelIndex(
            Variable indexVariable,
            Variable boundsVariable,
            FieldAccess? fieldAccess,
            int dimension)
        {
            // Assign global id
            using (var statement = BeginStatement(indexVariable, fieldAccess))
            {
                statement.AppendOperation(CLInstructions.GetGlobalId);
                statement.BeginArguments();
                statement.AppendCommand(dimension.ToString());
                statement.EndArguments();
            }

            // Access bounds check
            var tempCondition = AllocateType(BasicValueType.Int1);
            using (var statement = BeginStatement(tempCondition))
            {
                statement.Append(indexVariable);
                statement.AppendField(fieldAccess);

                statement.AppendOperation(
                    CLInstructions.GetCompareOperation(CompareKind.GreaterEqual));

                statement.Append(boundsVariable);
                statement.AppendField(fieldAccess);
            }

            // TODO: refactor if-block generation into a separate emitter
            // See also Visit(ConditionalBranch).
            AppendIndent();
            Builder.Append("if (");
            Builder.Append(tempCondition.ToString());
            Builder.AppendLine(")");
            PushIndent();
            using (var statement = BeginStatement(CLInstructions.ReturnStatement)) { }
            PopIndent();
        }

        /// <summary>
        /// Setups the current kernel indices.
        /// </summary>
        /// <param name="indexVariable">The main kernel index variable.</param>
        /// <param name="lengthVariable">
        /// The length variable of implicitly grouped kernels.
        /// </param>
        private void SetupKernelIndex(Variable indexVariable, Variable lengthVariable)
        {
            if (EntryPoint.IsExplicitlyGrouped)
                return;
            Debug.Assert(indexVariable != null, "Invalid index variable");

            if (EntryPoint.IndexType == IndexType.Index1D)
            {
                EmitImplicitKernelIndex(
                    indexVariable,
                    lengthVariable,
                    null,
                    0);
            }
            else
            {
                Declare(indexVariable);
                for (int i = 0, e = (int)EntryPoint.IndexType; i < e; ++i)
                {
                    EmitImplicitKernelIndex(
                        indexVariable,
                        lengthVariable,
                        new FieldAccess(i),
                        i);
                }
            }
        }

        #endregion
    }
}
