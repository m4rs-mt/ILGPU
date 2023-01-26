// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityGenerationModule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// A kernel module generator for managed velocity kernel types.
    /// </summary>
    sealed class VelocityGenerationModule : DisposeBase
    {
        #region Static

        /// <summary>
        /// Builds a complete parameter-type class wrapper that takes all scalar kernel
        /// arguments as constructor arguments and converts them into ready-to-load
        /// vectorized versions that are in turn stored as class fields.
        /// </summary>
        private static Type BuildParametersType(
            RuntimeSystem runtimeSystem,
            VelocityInstructions instructions,
            VelocityTypeGenerator typeGenerator,
            in Backend.BackendContext backendContext,
            EntryPoint entryPoint,
            out ConstructorInfo constructor,
            out ImmutableArray<FieldInfo> parameterFields)
        {
            // Build a new parameter passing type
            using var parametersLock = runtimeSystem.DefineRuntimeClass(
                typeof(VelocityParameters),
                out var typeBuilder);

            var kernelMethod = backendContext.KernelMethod;
            int numParameters =
                kernelMethod.Parameters.Count -
                entryPoint.KernelIndexParameterOffset;
            var nativeParameterTypes = new TypeNode[numParameters];
            var constructorParameterTypes = new Type[nativeParameterTypes.Length];
            var constructorLocalTypes = new Type[nativeParameterTypes.Length];
            var builtFields = new FieldInfo[numParameters];
            for (int i = 0; i < numParameters; ++i)
            {
                // Determine the scalar parameter type and remember it
                int parameterIndex = i + entryPoint.KernelIndexParameterOffset;
                var parameterType = kernelMethod.Parameters[parameterIndex].ParameterType;
                nativeParameterTypes[i] = parameterType;
                constructorLocalTypes[i] =
                    typeGenerator.GetLinearizedScalarType(parameterType);
                constructorParameterTypes[i] = typeof(void*);

                // Convert the parameter type and declare a new field
                var vectorizedType = typeGenerator.GetVectorizedType(parameterType);
                builtFields[i] = typeBuilder.DefineField(
                    StructureType.GetFieldName(i),
                    vectorizedType,
                    FieldAttributes.Public);
            }

            // Build a constructor that converts all parameters into their vectorized
            // representation
            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                constructorParameterTypes);
            {
                // Create a new constructor IL emitter
                var emitter = new ILEmitter(constructorBuilder.GetILGenerator());

                // Load each argument passed to the constructor and convert it into its
                // vectorized form via specialized convert operations
                for (int i = 0; i < constructorParameterTypes.Length; ++i)
                {
                    // Convert the current argument into a temporary local to load from
                    var loadLocal = emitter.DeclareLocal(constructorLocalTypes[i]);
                    emitter.Emit(ArgumentOperation.Load, i + 1);

                    // Load object via direct memory operations from pinned memory
                    emitter.Emit(OpCodes.Ldobj, constructorLocalTypes[i]);
                    emitter.Emit(LocalOperation.Store, loadLocal);

                    // Load a vectorized version
                    emitter.Emit(OpCodes.Ldarg_0);
                    BuildParameterLoad(
                        emitter,
                        loadLocal,
                        nativeParameterTypes[i],
                        instructions,
                        typeGenerator);

                    // Store vectorized version
                    emitter.Emit(OpCodes.Stfld, builtFields[i]);
                }

                // Return
                emitter.Emit(OpCodes.Ret);
            }

            // Build the parameter type and determine the parameter mapping
            var result = typeBuilder.CreateType();
            var parameterMapping =
                ImmutableArray.CreateBuilder<FieldInfo>(numParameters);
            for (int i = 0; i < numParameters; ++i)
            {
                var fieldInfo = ILEmitterExtensions.GetFieldInfo(result, i);
                parameterMapping.Add(fieldInfo);
            }
            parameterFields = parameterMapping.MoveToImmutable();
            constructor = result.GetConstructor(constructorParameterTypes);
            return result;
        }

        /// <summary>
        /// Builds a vectorized kernel parameter load for arbitrary types.
        /// </summary>
        private static void BuildParameterLoad<TILEmitter>(
            in TILEmitter emitter,
            ILLocal source,
            TypeNode typeNode,
            VelocityInstructions instructions,
            VelocityTypeGenerator typeGenerator)
            where TILEmitter : struct, IILEmitter
        {
            if (typeNode is StructureType structureType)
            {
                var vectorizedType = typeGenerator.GetVectorizedType(structureType);
                var temporary = emitter.DeclareLocal(vectorizedType);

                // Fill the temporary structure instance with values
                foreach (var (fieldType, fieldAccess) in structureType)
                {
                    // Load the target variable address
                    emitter.Emit(LocalOperation.LoadAddress, temporary);

                    // Load the input value
                    emitter.Emit(LocalOperation.Load, source);
                    emitter.LoadField(source.VariableType, fieldAccess.Index);

                    // Load the converted field type
                    BuildScalarParameterLoad(
                        emitter,
                        fieldType,
                        instructions);

                    // Store it into out structure field
                    emitter.StoreField(vectorizedType, fieldAccess.Index);
                }

                emitter.Emit(LocalOperation.Load, temporary);
            }
            else
            {
                // Load input argument value
                emitter.Emit(LocalOperation.Load, source);

                // Load the scalar parameter
                BuildScalarParameterLoad(
                    emitter,
                    typeNode,
                    instructions);
            }
        }

        /// <summary>
        /// Builds a vectorized kernel parameter load for scalar types.
        /// </summary>
        private static void BuildScalarParameterLoad<TILEmitter>(
            in TILEmitter emitter,
            TypeNode typeNode,
            VelocityInstructions instructions)
            where TILEmitter : struct, IILEmitter
        {
            var basicValueType = typeNode switch
            {
                PrimitiveType primitiveType => primitiveType.BasicValueType,
                PaddingType paddingType => paddingType.BasicValueType,
                PointerType _ => BasicValueType.Int64,
                _ => // Not supported type conversions
                    throw typeNode.GetNotSupportedException(
                        ErrorMessages.NotSupportedType,
                        typeNode)
            };

            // Convert value on top of the evaluation stack without sign extension
            var mode = VelocityWarpOperationMode.F;
            if (basicValueType.IsInt())
            {
                // Expand type
                emitter.Emit(basicValueType.IsTreatedAs32Bit()
                    ? OpCodes.Conv_U4
                    : OpCodes.Conv_U8);

                mode = VelocityWarpOperationMode.U;
            }
            else
            {
                // Convert half to 32bit float
                if (basicValueType == BasicValueType.Float16)
                    emitter.EmitCall(instructions.FromHalfMethod);
            }

            // Load the values onto the evaluation stack
            var load = basicValueType.IsTreatedAs32Bit()
                ? instructions.GetConstValueOperation32(mode)
                : instructions.GetConstValueOperation64(mode);
            emitter.EmitCall(load);
        }

        #endregion

        #region Instance

        private readonly Dictionary<Method, RuntimeSystem.MethodEmitter> methodMapping;

        public VelocityGenerationModule(
            RuntimeSystem runtimeSystem,
            VelocityInstructions instructions,
            VelocityTypeGenerator typeGenerator,
            in Backend.BackendContext backendContext,
            EntryPoint entryPoint)
        {
            methodMapping = new Dictionary<Method, RuntimeSystem.MethodEmitter>(
                backendContext.Count);
            TypeGenerator = typeGenerator;

            // Create the parameter passing type
            ParametersType = BuildParametersType(
                runtimeSystem,
                instructions,
                typeGenerator,
                backendContext,
                entryPoint,
                out var constructorInfo,
                out var parameterFields);
            ParametersTypeConstructor = constructorInfo;
            ParameterFields = parameterFields;

            // Declare all methods
            DeclareMethod(runtimeSystem, backendContext.KernelMethod);
            foreach (var (method, _) in backendContext)
                DeclareMethod(runtimeSystem, method);

            // Get the kernel method
            KernelMethod = this[backendContext.KernelMethod];

            // Setup shared memory information
            SharedAllocationSize = backendContext.SharedAllocations.TotalSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current type generator being used.
        /// </summary>
        public VelocityTypeGenerator TypeGenerator { get; }

        /// <summary>
        /// Returns the kernel method.
        /// </summary>
        public MethodInfo KernelMethod { get; }

        /// <summary>
        /// Gets the method builder that is associated with the given method.
        /// </summary>
        /// <param name="method">The method to get the managed method for.</param>
        public MethodInfo this[Method method] => methodMapping[method].Method;

        /// <summary>
        /// Returns the class type to store all parameter values to.
        /// </summary>
        public Type ParametersType { get; }

        /// <summary>
        /// Returns the constructor to build a new parameters type instance.
        /// </summary>
        public ConstructorInfo ParametersTypeConstructor { get; }

        /// <summary>
        /// Returns all parameter fields to store the actual parameter data into.
        /// </summary>
        public ImmutableArray<FieldInfo> ParameterFields { get; }

        /// <summary>
        /// The total amount of bytes residing in shared memory.
        /// </summary>
        public int SharedAllocationSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Declares the given method.
        /// </summary>
        private void DeclareMethod(RuntimeSystem runtimeSystem, Method method)
        {
            // Convert the method signature
            var returnType = TypeGenerator.GetVectorizedType(method.ReturnType);
            int parameterOffset = method.HasFlags(MethodFlags.EntryPoint) ? 1 : 0;
            int parameterAddition = method.HasFlags(MethodFlags.EntryPoint) ? 0 : 1;
            var parameterTypes = new Type[
                method.NumParameters - parameterOffset + parameterAddition];

            // The first parameter is the current mask (if it is not an entry point)
            if (parameterOffset > 0)
            {
                // This is our main method
                parameterTypes = VelocityMultiprocessor.KernelHandlerTypes.ToArray();
            }
            else
            {
                // Convert all parameter types
                parameterTypes[0] = typeof(VelocityLaneMask);
                for (int i = 0; i < method.NumParameters; ++i)
                {
                    var parameterType = method.Parameters[i].ParameterType;
                    parameterTypes[i + parameterAddition] =
                        TypeGenerator.GetVectorizedType(parameterType);
                }
            }

            // Define a new method stub
            using var scopedLock = runtimeSystem.DefineRuntimeMethod(
                returnType,
                parameterTypes,
                out var methodBuilder);
            methodMapping.Add(method, methodBuilder);
        }

        /// <summary>
        /// Gets the IL generator that is associated with the method.
        /// </summary>
        public ILGenerator GetILGenerator(Method method) =>
            methodMapping[method].ILGenerator;

        #endregion

        #region IDisposable

        /// <summary>
        /// Frees the current scoped locked.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            foreach (var (_, builder) in methodMapping)
                builder.Finish();
            base.Dispose(disposing);
        }

        #endregion
    }
}
