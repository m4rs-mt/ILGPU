// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLKernelTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Generates OpenCL type structures that can be used for data marshaling data.
    /// </summary>
    sealed class CLKernelTypeGenerator : ICLTypeGenerator
    {
        #region Constants

        /// <summary>
        /// The string format of a single structure-like type.
        /// </summary>
        private const string KernelTypeNameSuffix = "_kernel";

        #endregion

        #region Nested Types

        /// <summary>
        /// Replaces pointers with an integer index offsets.
        /// </summary>
        private sealed class CLKernelTypeConverter : TypeConverter<PointerType>
        {
            /// <summary>
            /// Converts a pointer to an index argument for kernel-argument mapping.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                PointerType type) =>
                typeContext.GetPrimitiveType(BasicValueType.Int32);

            /// <summary>
            /// The result will consume one field.
            /// </summary>
            protected override int GetNumFields(PointerType type) => 1;
        }

        #endregion

        #region Instance

        private readonly CLKernelTypeConverter typeConverter;
        private readonly (StructureType, string)[] parameterTypes;

        /// <summary>
        /// Constructs a new type generator and defines all internal types for the
        /// OpenCL backend.
        /// </summary>
        /// <param name="typeGenerator">The parent type generator.</param>
        /// <param name="entryPoint">The current entry point.</param>
        public CLKernelTypeGenerator(
            CLTypeGenerator typeGenerator,
            SeparateViewEntryPoint entryPoint)
        {
            TypeGenerator = typeGenerator;
            EntryPoint = entryPoint;
            ParameterOffset = entryPoint.KernelIndexParameterOffset;

            typeConverter = new CLKernelTypeConverter();
            parameterTypes = new (StructureType, string)[
                entryPoint.Parameters.Count + ParameterOffset];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent type generator to use.
        /// </summary>
        public CLTypeGenerator TypeGenerator { get; }

        /// <summary>
        /// Returns the associated entry point.
        /// </summary>
        public SeparateViewEntryPoint EntryPoint { get; }

        /// <summary>
        /// Returns the current parameter offset.
        /// </summary>
        public int ParameterOffset { get; }

        /// <summary>
        /// Returns the associated OpenCL type name.
        /// </summary>
        /// <param name="parameter">The IR parameter.</param>
        /// <returns>The resolved OpenCL type name.</returns>
        public string this[Parameter parameter]
        {
            get
            {
                var (_, typeName) = parameterTypes[parameter.Index];
                if (typeName != null)
                    return typeName;

                // Resolve base type name from the parent type generator
                return TypeGenerator[parameter.ParameterType];
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Registers a new kernel parameter.
        /// </summary>
        /// <param name="parameter">The parameter to register.</param>
        public void Register(Parameter parameter)
        {
            ref var parameterType = ref parameterTypes[parameter.Index];
            parameter.Assert(parameterType.Item1 is null);

            // Check whether we require a custom mapping
            if (!EntryPoint.TryGetViewParameters(
                parameter.Index - ParameterOffset,
                out var _))
            {
                return;
            }

            // Resolve base type name from the parent type generator
            var clName = TypeGenerator[parameter.ParameterType];

            // Adjust the current type name
            clName += KernelTypeNameSuffix;

            // Retrieve structure type
            var structureType = parameter.ParameterType.As<StructureType>(parameter);

            // Convert the kernel type using a specific type converter
            structureType = typeConverter.ConvertType(
                TypeGenerator.TypeContext,
                structureType).AsNotNullCast<StructureType>();

            // Register internally
            parameterTypes[parameter.Index] = (structureType, clName);
        }

        /// <summary>
        /// Generate all forward type declarations.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDeclarations(StringBuilder builder)
        {
            foreach (var (_, typeName) in parameterTypes)
            {
                if (typeName == null)
                    continue;

                builder.Append(typeName);
                builder.AppendLine(";");
            }
            builder.AppendLine();
        }

        /// <summary>
        /// Generate all type definitions.
        /// </summary>
        /// <param name="builder">The target builder.</param>
        public void GenerateTypeDefinitions(StringBuilder builder)
        {
            var generatedTypes = new HashSet<string>();
            for (
                int paramIdx = ParameterOffset, numParams = parameterTypes.Length;
                paramIdx < numParams;
                ++paramIdx)
            {
                // Check for registered types
                var (type, typeName) = parameterTypes[paramIdx];
                if (type == null || !generatedTypes.Add(typeName))
                    continue;

#if DEBUG
                // Get the current view mapping
                var viewMapping = EntryPoint.GetViewParameters(
                    paramIdx - ParameterOffset);
                Debug.Assert(
                    viewMapping.Count > 0,
                    "There must be at least one view entry");

                for (
                    int i = 0, specialParamIdx = 0, e = type.NumFields;
                    i < e && specialParamIdx < viewMapping.Count;
                    ++i)
                {
                    // Check whether the current field is a view pointer
                    if (viewMapping[specialParamIdx].TargetAccess.Index == i)
                    {
                        Debug.Assert(
                            type[i].BasicValueType == BasicValueType.Int32,
                            "Invalid view index");
                    }
                }
#endif
                TypeGenerator.GenerateStructureDefinition(
                    type,
                    typeName,
                    builder);
            }

            builder.AppendLine();
        }

        #endregion
    }
}
