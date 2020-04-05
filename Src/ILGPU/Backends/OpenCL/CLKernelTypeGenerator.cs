// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLKernelTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
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

        #region Instance

        private readonly (StructureType, string)[] parameterTypes;

        /// <summary>
        /// Constructs a new type generator and defines all internal types for the OpenCL backend.
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
            Debug.Assert(parameterType.Item1 == null, "Parameter already registered");

            // Check whether we require a custom mapping
            if (!EntryPoint.TryGetViewParameters(
                parameter.Index - ParameterOffset,
                out var _))
                return;

            // Resolve base type name from the parent type generator
            var clName = TypeGenerator[parameter.ParameterType];

            // Adjust the current type name
            clName += KernelTypeNameSuffix;

            // Retrieve structure type
            var structureType = parameter.ParameterType as StructureType;
            Debug.Assert(structureType != null, "Invalid custom kernel type");

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

                // Get the current view mapping
                var viewMapping = EntryPoint.GetViewParameters(paramIdx - ParameterOffset);
                Debug.Assert(viewMapping.Count > 0, "There must be at least one view entry");

                builder.AppendLine(typeName);
                builder.AppendLine("{");
                for (int i = 0, specialParamIdx = 0, e = type.NumFields; i < e; ++i)
                {
                    // Check whether the current field is a view pointer
                    builder.Append('\t');
                    if (specialParamIdx < viewMapping.Count &&
                        viewMapping[specialParamIdx].TargetAccess.Index == i)
                    {
                        // Append an integer type (the view index)
                        builder.Append(
                            CLTypeGenerator.GetBasicValueType(BasicValueType.Int32));
                        ++specialParamIdx;
                    }
                    else
                    {
                        // Append the field type
                        builder.Append(TypeGenerator[type[i]]);
                    }
                    builder.Append(' ');
                    builder.Append(CLTypeGenerator.GetFieldName(i));
                    builder.AppendLine(";");
                }
                builder.AppendLine("};");
            }

            builder.AppendLine();
        }

        #endregion
    }
}
