// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File IntrinsicImplementationResolver.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents an implementation provider for intrinsic functions.
    /// </summary>
    public class IntrinsicImplementationResolver
    {
        #region Nested Types

        /// <summary>
        /// A simple resolver for intrinsic math functions.
        /// </summary>
        protected sealed class MathImplementationResolver
        {
            /// <summary>
            /// Represents a mapping of all binary math functions.
            /// </summary>
            private readonly Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), CodeGenerationResult> binaryMathFunctions =
                new Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), CodeGenerationResult>();

            /// <summary>
            /// Represents a mapping of all unary math functions.
            /// </summary>
            private readonly Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), CodeGenerationResult> unaryMathFunctions =
                new Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), CodeGenerationResult>();

            /// <summary>
            /// Constructs a new resolver for intrinsic math functions.
            /// </summary>
            /// <param name="codeGenerationPhase">The current generation phase to emit code.</param>
            /// <param name="predicate">A predicate to filter candidate functions.</param>
            /// <param name="implementationTypes">The types that implement the desired math functions.</param>
            public MathImplementationResolver(
                CodeGenerationPhase codeGenerationPhase,
                Predicate<MethodInfo> predicate,
                params Type[] implementationTypes)
            {
                foreach (var implementationType in implementationTypes)
                {
                    var mathFunctions = implementationType.GetMethods(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    foreach (var mathFunction in mathFunctions)
                    {
                        var intrinsicAttribute = mathFunction.GetCustomAttribute<MathIntrinsicAttribute>();
                        if (intrinsicAttribute == null ||
                            !predicate(mathFunction))
                            continue;
                        var parameters = mathFunction.GetParameters();
                        var basicValueType = parameters[0].ParameterType.GetArithmeticBasicValueType();
                        var functionEntry = codeGenerationPhase.GenerateCode(mathFunction);
                        if (parameters.Length < 2)
                        {
                            unaryMathFunctions[((UnaryArithmeticKind)intrinsicAttribute.IntrinsicKind, basicValueType)] =
                                functionEntry;
                        }
                        else
                        {
                            binaryMathFunctions[
                                ((BinaryArithmeticKind)(intrinsicAttribute.IntrinsicKind - MathIntrinsicKind._BinaryFunctions - 1),
                                basicValueType)] = functionEntry;
                        }
                    }
                }
            }

            /// <summary>
            /// Adds all detected functions to the given resolver.
            /// </summary>
            /// <param name="resolver">The resolver to add to.</param>
            public void ApplyTo(IntrinsicImplementationResolver resolver)
            {
                var context = resolver.IntrinsicContext;

                foreach (var binaryFunction in binaryMathFunctions)
                {
                    resolver.binaryMathFunctions.Add(binaryFunction.Key,
                        context.GetMethod(binaryFunction.Value.ResultHandle));
                }

                foreach (var unaryFunction in unaryMathFunctions)
                {
                    resolver.unaryMathFunctions.Add(unaryFunction.Key,
                        context.GetMethod(unaryFunction.Value.ResultHandle));
                }
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Represents a mapping of all binary math functions.
        /// </summary>
        private readonly Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), Method> binaryMathFunctions =
            new Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), Method>();

        /// <summary>
        /// Represents a mapping of all unary math functions.
        /// </summary>
        private readonly Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), Method> unaryMathFunctions =
            new Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), Method>();

        /// <summary>
        /// Constructs a new implementation resolver.
        /// </summary>
        /// <param name="context">The source context.</param>
        protected IntrinsicImplementationResolver(IRContext context)
        {
            IntrinsicContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        ///// <summary>
        ///// Constructs a new implementation resolver.
        ///// </summary>
        ///// <param name="context">The source context.</param>
        ///// <param name="intrinsicMetadata">Math mapping information.</param>
        //protected IntrinsicImplementationResolver(IRContext context, IntrinsicMetadata intrinsicMetadata)
        //    : this(context)
        //{
        //    if (intrinsicMetadata == null)
        //        throw new ArgumentNullException(nameof(intrinsicMetadata));

        //    foreach (var binaryFunction in intrinsicMetadata.BinaryMathFunctions)
        //        binaryMathFunctions.Add(binaryFunction.Key, context.GetMethod(binaryFunction.Value));

        //    foreach (var unaryFunction in intrinsicMetadata.UnaryMathFunctions)
        //        unaryMathFunctions.Add(unaryFunction.Key, context.GetMethod(unaryFunction.Value));
        //}

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated intrinsic context.
        /// </summary>
        public IRContext IntrinsicContext { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the binary math implementation method that corrensponds to
        /// the passed arguments.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="valueType">The value type.</param>
        /// <param name="method">The resolved implementation method.</param>
        /// <returns>True, if the implementation method could be resolved.</returns>
        public bool TryGetMathImplementation(
            BinaryArithmeticKind kind,
            ArithmeticBasicValueType valueType,
            out Method method) => binaryMathFunctions.TryGetValue((kind, valueType), out method);

        /// <summary>
        /// Tries to resolve the unary math implementation method that corrensponds to
        /// the passed arguments.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <param name="valueType">The value type.</param>
        /// <param name="method">The resolved implementation method.</param>
        /// <returns>True, if the implementation method could be resolved.</returns>
        public bool TryGetMathImplementation(
            UnaryArithmeticKind kind,
            ArithmeticBasicValueType valueType,
            out Method method) => unaryMathFunctions.TryGetValue((kind, valueType), out method);

        /// <summary>
        /// Tries to resolve a debug assert implementation method.
        /// </summary>
        /// <param name="method">The resolved assert implementation method.</param>
        /// <returns>True, if the implementation method could be resolved.</returns>
        public virtual bool TryGetDebugImplementation(
            out Method method)
        {
            method = null;
            return false;
        }

        #endregion
    }
}
