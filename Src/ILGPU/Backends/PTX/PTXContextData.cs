// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXContextData.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Util;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents context information that is required by all PTXBackend instances.
    /// </summary>
    sealed class PTXContextData : DisposeBase
    {
        #region Nested Types

        /// <summary>
        /// The kernel specializer configuration for PTX intrinsics.
        /// </summary>
        private readonly struct PTXIntrinsicConfiguration : IIntrinsicSpecializerConfiguration
        {
            /// <summary>
            /// Constructs a new specializer configuration.
            /// </summary>
            /// <param name="resolver">The associated resolver.</param>
            public PTXIntrinsicConfiguration(
                IntrinsicImplementationResolver resolver)
            {
                ImplementationResolver = resolver;
            }

            /// <summary cref="IIntrinsicSpecializerConfiguration.EnableAssertions"/>
            public bool EnableAssertions => false;

            /// <summary cref="IIntrinsicSpecializerConfiguration.WarpSize"/>
            public int WarpSize => PTXBackend.WarpSize;

            /// <summary cref="IIntrinsicSpecializerConfiguration.ImplementationResolver"/>
            public IntrinsicImplementationResolver ImplementationResolver { get; }

            /// <summary cref="IIntrinsicSpecializerConfiguration.TryGetSizeOf(TypeNode, out int)"/>
            public bool TryGetSizeOf(TypeNode type, out int size)
            {
                size = 0;
                return false;
            }
        }

        /// <summary>
        /// Represents a PTX-specific intrinsic resolver.
        /// </summary>
        private sealed class Resolver : IntrinsicImplementationResolver
        {
            private const string DebugAssertFailedName = "__assertfail";
            private const string WrapperDebugAssertFailedName = "__wassert";

            private readonly Method debugAssertFunction;

            public Resolver(Context context, IRContext irContext)
                : base(irContext)
            {
                MathImplementationResolver resolver;
                using (var phase = context.BeginCodeGeneration(irContext))
                {
                    using (var frontendPhase = phase.BeginFrontendCodeGeneration())
                    {
                        resolver = new MathImplementationResolver(
                            frontendPhase,
                            mathFunction => mathFunction.GetCustomAttribute<PTXMathIntrinsicAttribute>() == null,
                            typeof(XMath), typeof(Resolver));

                        // Declare debugging functions
                        var deviceAssertFunction = frontendPhase.DeclareMethod(
                            new MethodDeclaration(
                                DebugAssertFailedName,
                                irContext.VoidType,
                                MethodFlags.External));

                        using (var failedBuilder = deviceAssertFunction.CreateBuilder())
                        {
                            failedBuilder.AddParameter(irContext.StringType, "message");
                            failedBuilder.AddParameter(irContext.StringType, "file");
                            failedBuilder.AddParameter(
                                irContext.GetPrimitiveType(BasicValueType.Int32),
                                "line");
                            failedBuilder.AddParameter(irContext.StringType, "function");
                            failedBuilder.AddParameter(
                                irContext.GetPrimitiveType(BasicValueType.Int32),
                                "charSize");
                        }

                        debugAssertFunction = frontendPhase.DeclareMethod(
                            new MethodDeclaration(
                                WrapperDebugAssertFailedName,
                                irContext.VoidType,
                                MethodFlags.AggressiveInlining));
                        using (var assertBuilder = debugAssertFunction.CreateBuilder())
                        {
                            var messageParam = assertBuilder.AddParameter(irContext.StringType, "message");
                            var entryBlock = assertBuilder.CreateEntryBlock();

                            entryBlock.CreateCall(
                                deviceAssertFunction,
                                ImmutableArray.Create(
                                    messageParam,
                                    entryBlock.CreatePrimitiveValue("Kernel.cs"),
                                    entryBlock.CreatePrimitiveValue(0),
                                    entryBlock.CreatePrimitiveValue("Kernel"),
                                    entryBlock.CreatePrimitiveValue(1)));
                            entryBlock.CreateReturn();
                        }
                    }
                }

                var transformer = Transformer.Create(
                    new TransformerConfiguration(MethodTransformationFlags.None, false),
                    new IntrinsicSpecializer<PTXIntrinsicConfiguration>(new PTXIntrinsicConfiguration(
                        this)));
                irContext.Transform(transformer);
                irContext.Optimize();
                resolver.ApplyTo(this);
            }

            public override bool TryGetDebugImplementation(
                out Method topLevelFunction)
            {
                topLevelFunction = debugAssertFunction;
                return true;
            }

            #region Implementations

            // Basic math remapping functionality to enable general support
            // double operations in GPU kernels.
            // CAUTION: These functions will be replaced by a specific
            // fast-math library for cross platform CPU/GPU math functions
            // in the future. See the XMath class for more details.

            [MathIntrinsic(MathIntrinsicKind.SinF)]
            static double Sin(double x) => XMath.Sin((float)x);

            [MathIntrinsic(MathIntrinsicKind.CosF)]
            static double Cos(double x) => XMath.Cos((float)x);

            [MathIntrinsic(MathIntrinsicKind.Log2F)]
            static double Log2(double x) => XMath.Log2((float)x);

            [MathIntrinsic(MathIntrinsicKind.Exp2F)]
            static double Exp2(double x) => XMath.Exp2((float)x);

            #endregion
        }

        #endregion

        private IRContext intrinsicContext;

        /// <summary>
        /// Creates a new PTX context information.
        /// </summary>
        /// <param name="context">The main context.</param>
        public PTXContextData(Context context)
        {
            intrinsicContext = new IRContext(context);
            ImplementationResolver = new Resolver(context, IntrinsicContext);
        }

        /// <summary>
        /// Returns the associated internal intrinsic context.
        /// </summary>
        public IRContext IntrinsicContext => intrinsicContext;

        /// <summary>
        /// Returns the default math implementation resolver for PTX.
        /// </summary>
        public IntrinsicImplementationResolver ImplementationResolver { get; }

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
            MessageId = "intrinsicContext",
            Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            Dispose(ref intrinsicContext);
        }
    }
}
