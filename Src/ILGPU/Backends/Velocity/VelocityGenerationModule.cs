// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityGenerationModule.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        /// Represents a reference to the public dumping method.
        /// </summary>
        private static readonly MethodInfo DumpMethodInfo =
            typeof(VelocityParameters).GetMethod(
                    nameof(VelocityParameters.DumpToConsole),
                    BindingFlags.Public | BindingFlags.Instance)
                .ThrowIfNull();

        /// <summary>
        /// Builds a complete parameter-type class wrapper that takes all scalar kernel
        /// arguments as constructor arguments and converts them into ready-to-load
        /// vectorized versions that are in turn stored as class fields.
        /// </summary>
        private static Type BuildParametersType(
            RuntimeSystem runtimeSystem,
            VelocityTargetSpecializer specializer,
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
            DefineConstructor(
                specializer,
                typeGenerator,
                typeBuilder,
                constructorParameterTypes,
                constructorLocalTypes,
                nativeParameterTypes,
                builtFields);

            // Define our dumping method
            DefineDumpMethod(
                specializer,
                typeBuilder,
                nativeParameterTypes,
                builtFields);

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
            constructor = result.GetConstructor(constructorParameterTypes).AsNotNull();
            return result;
        }

        /// <summary>
        /// Defines our argument conversion constructor to map scalar arguments to
        /// vectorized Velocity space.
        /// </summary>
        private static void DefineConstructor(
            VelocityTargetSpecializer specializer,
            VelocityTypeGenerator typeGenerator,
            TypeBuilder typeBuilder,
            Type[] constructorParameterTypes,
            Type[] constructorLocalTypes,
            TypeNode[] nativeParameterTypes,
            FieldInfo[] builtFields)
        {
            var constructorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                constructorParameterTypes);

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
                    specializer,
                    typeGenerator);

                // Store vectorized version
                emitter.Emit(OpCodes.Stfld, builtFields[i]);
            }

            // Return
            emitter.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Builds a vectorized kernel parameter load for arbitrary types.
        /// </summary>
        private static void BuildParameterLoad<TILEmitter>(
            TILEmitter emitter,
            ILLocal source,
            TypeNode typeNode,
            VelocityTargetSpecializer specializer,
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
                        specializer);

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
                    specializer);
            }
        }

        /// <summary>
        /// Defines a dumping method to visualize kernel input parameters.
        /// </summary>
        private static void DefineDumpMethod(
            VelocityTargetSpecializer specializer,
            TypeBuilder typeBuilder,
            TypeNode[] nativeParameterTypes,
            FieldInfo[] fieldsToVisualize)
        {
            var dumpMethod = typeBuilder.DefineMethod(
                DumpMethodInfo.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(void),
                Array.Empty<Type>());

            // Emit code to visualize each field
            var emitter = new ILEmitter(dumpMethod.GetILGenerator());
            void DumpRawValue(BasicValueType basicValueType, string fieldLabel)
            {
                if (basicValueType.IsTreatedAs32Bit())
                    specializer.DumpWarp32(emitter, fieldLabel);
                else
                    specializer.DumpWarp64(emitter, fieldLabel);
            }

            // Dump all fields
            for (int i = 0; i < nativeParameterTypes.Length; ++i)
            {
                string fieldLabel = $"InputArg_{i}";

                var nativeType = nativeParameterTypes[i];
                var field = fieldsToVisualize[i];

                void LoadFieldInstance()
                {
                    emitter.Emit(OpCodes.Ldarg_0);
                    emitter.Emit(OpCodes.Ldfld, field);
                }

                if (nativeType is StructureType structureType)
                {
                    // Dump each field separately
                    emitter.EmitWriteLine(fieldLabel);

                    for (int j = 0; j < structureType.NumFields; ++j)
                    {
                        LoadFieldInstance();
                        emitter.LoadField(field.FieldType, j);
                        DumpRawValue(structureType[j].BasicValueType, $"  Field_{j}");
                    }
                }
                else
                {
                    LoadFieldInstance();
                    DumpRawValue(nativeType.BasicValueType, fieldLabel);
                }
            }

            emitter.Emit(OpCodes.Ret);
            emitter.Finish();
            typeBuilder.DefineMethodOverride(dumpMethod, DumpMethodInfo);
        }

        /// <summary>
        /// Builds a vectorized kernel parameter load for scalar types.
        /// </summary>
        private static void BuildScalarParameterLoad<TILEmitter>(
            TILEmitter emitter,
            TypeNode typeNode,
            VelocityTargetSpecializer specializer)
            where TILEmitter : struct, IILEmitter
        {
            var basicValueType = typeNode switch
            {
                PointerType _ => BasicValueType.Int64,
                _ => typeNode.BasicValueType == BasicValueType.None
                    ? throw typeNode.GetNotSupportedException(
                            ErrorMessages.NotSupportedType,
                            typeNode)
                    : typeNode.BasicValueType
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
                if (basicValueType == BasicValueType.Float16)
                    throw CapabilityContext.GetNotSupportedFloat16Exception();
            }

            // Load the values onto the evaluation stack
            if (basicValueType.IsTreatedAs32Bit())
                specializer.ConvertScalarTo32(emitter, mode);
            else
                specializer.ConvertScalarTo64(emitter, mode);
        }

        #endregion

        #region Instance

        private readonly Dictionary<Method, RuntimeSystem.MethodEmitter> methodMapping;

        public VelocityGenerationModule(
            RuntimeSystem runtimeSystem,
            VelocityTargetSpecializer specializer,
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
                specializer,
                typeGenerator,
                backendContext,
                entryPoint,
                out var constructorInfo,
                out var parameterFields);
            ParametersTypeConstructor = constructorInfo;
            ParameterFields = parameterFields;

            // Declare all methods
            DeclareMethod(runtimeSystem, backendContext.KernelMethod, specializer);
            foreach (var (method, _) in backendContext)
                DeclareMethod(runtimeSystem, method, specializer);

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
        private void DeclareMethod(
            RuntimeSystem runtimeSystem,
            Method method,
            VelocityTargetSpecializer specializer)
        {
            // Convert the method signature
            var returnType = TypeGenerator.GetVectorizedType(method.ReturnType);
            Type[] parameterTypes;

            // The first parameter is the current mask (if it is not an entry point)
            if (method.HasFlags(MethodFlags.EntryPoint))
            {
                // This is our main method
                parameterTypes = VelocityEntryPointHandlerHelper.EntryPointParameterTypes;
            }
            else
            {
                parameterTypes = new Type[
                    method.NumParameters + VelocityCodeGenerator.MethodParameterOffset];
                // Convert all parameter types
                parameterTypes[VelocityCodeGenerator.ExecutionContextIndex] =
                    typeof(VelocityGroupExecutionContext);
                parameterTypes[VelocityCodeGenerator.MaskParameterIndex] =
                    specializer.WarpType32;
                for (int i = 0; i < method.NumParameters; ++i)
                {
                    var parameterType = method.Parameters[i].ParameterType;
                    parameterTypes[i + VelocityCodeGenerator.MethodParameterOffset] =
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
