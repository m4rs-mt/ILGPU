// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionCloning.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents a custom specializer that can be used during function cloning.
    /// </summary>
    public interface IFunctionCloningSpecializer
    {
        /// <summary>
        /// Maps the given type to a custom type.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <returns>The mapped result type, or null iff the type could not be mapped.</returns>
        TypeNode MapType(TypeNode type);

        /// <summary>
        /// Specializes the given source parameter by building a new
        /// parameter that represents the specialized one.
        /// </summary>
        /// <param name="sourceFunction">The source function.</param>
        /// <param name="sourceParameter">The source parameter.</param>
        /// <param name="mappedType">The mapped parameter type.</param>
        /// <param name="functionBuilder">The target function builder.</param>
        /// <returns></returns>
        Parameter SpecializeParameter(
            FunctionValue sourceFunction,
            Parameter sourceParameter,
            TypeNode mappedType,
            FunctionBuilder functionBuilder);
    }

    partial class IRBuilder
    {
        #region Nested Types

        /// <summary>
        /// Represents no cloning specializer.
        /// </summary>
        private readonly struct NoParameterSpecializer : IFunctionCloningSpecializer
        {
            /// <summary cref="IFunctionCloningSpecializer.MapType(TypeNode)"/>
            public TypeNode MapType(TypeNode type) => type;

            /// <summary cref="IFunctionCloningSpecializer.SpecializeParameter(FunctionValue, Parameter, TypeNode, FunctionBuilder)"/>
            public Parameter SpecializeParameter(
                FunctionValue sourceFunction,
                Parameter sourceParameter,
                TypeNode mappedType,
                FunctionBuilder functionBuilder) =>
                functionBuilder.AddParameter(mappedType, sourceParameter.Name);
        }

        #endregion

        #region Function Cloning

        /// <summary>
        /// Clones the given function value and replaces all parameters and the function
        /// itself with the cloned function.
        /// </summary>
        /// <param name="functionValue">The function value to clone.</param>
        /// <returns>The builder of the cloned function.</returns>
        public FunctionBuilder CloneAndReplaceFunction(FunctionValue functionValue) =>
            CloneAndReplaceFunction(functionValue, new NoParameterSpecializer());

        /// <summary>
        /// Clones the given function value and replaces all parameters and the function
        /// itself with the cloned function.
        /// </summary>
        /// <typeparam name="TSpecializer">The specializer type.</typeparam>
        /// <param name="functionValue">The function value to clone.</param>
        /// <param name="specializer">A specializer to customize cloned functions.</param>
        /// <returns>The builder of the cloned function.</returns>
        public FunctionBuilder CloneAndReplaceFunction<TSpecializer>(
            FunctionValue functionValue,
            in TSpecializer specializer)
            where TSpecializer : IFunctionCloningSpecializer
        {
            if (functionValue == null)
                throw new ArgumentNullException(nameof(functionValue));

            FunctionBuilder functionBuilder;
            if (functionValue is TopLevelFunction topLevelFunction)
            {
                var returnType = specializer.MapType(topLevelFunction.ReturnType);
                Debug.Assert(returnType != null, "Invalid mapped return type");

                var specializedDeclaration = topLevelFunction.Declaration.Specialize(returnType);
                functionBuilder = CreateFunction(specializedDeclaration);

                topLevelFunction.MemoryParam.Replace(functionBuilder.MemoryParam);
                topLevelFunction.ReturnParam.Replace(functionBuilder.ReturnParam);
            }
            else
                functionBuilder = CreateFunction(functionValue.Name);

            // Append all required parameters and replace the old parameters with the new values
            foreach (var sourceParam in functionValue.Parameters)
            {
                var mappedType = specializer.MapType(sourceParam.Type);
                Debug.Assert(mappedType != null, "Invalid mapped parameter type");
                var targetParam = specializer.SpecializeParameter(
                    functionValue,
                    sourceParam,
                    mappedType,
                    functionBuilder);
                Debug.Assert(targetParam != null, "Invalid target parameter");
                sourceParam.Replace(targetParam);
            }

            // Replace function
            functionValue.Replace(functionBuilder.FunctionValue);

            return functionBuilder;
        }

        /// <summary>
        /// Clones the given function value and replaces all parameters and the function
        /// itself with the cloned function.
        /// </summary>
        /// <param name="functionValue">The function value to clone.</param>
        /// <returns>The clone.</returns>
        public FunctionValue CloneAndReplaceSealedFunction(FunctionValue functionValue) =>
            CloneAndReplaceSealedFunction(functionValue, new NoParameterSpecializer());

        /// <summary>
        /// Clones the given function value and replaces all parameters and the function
        /// itself with the cloned function.
        /// </summary>
        /// <typeparam name="TSpecializer">The specializer type.</typeparam>
        /// <param name="functionValue">The function value to clone.</param>
        /// <param name="specializer">A specializer to customize cloned functions.</param>
        /// <returns>The clone.</returns>
        public FunctionValue CloneAndReplaceSealedFunction<TSpecializer>(
            FunctionValue functionValue,
            in TSpecializer specializer)
            where TSpecializer : IFunctionCloningSpecializer
        {
            var target = functionValue.Target;
            var newFunction = CloneAndReplaceFunction(functionValue, specializer);
            return newFunction.Seal(target);
        }

        #endregion
    }
}
