// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// The basic configuration interface for all intrinsic specializers.
    /// </summary>
    public interface IAcceleratorSpecializerConfiguration
    {
        /// <summary>
        /// Returns the current warp size (if any).
        /// </summary>
        int? WarpSize { get; }

        /// <summary>
        /// Returns the current accelerator type.
        /// </summary>
        AcceleratorType AcceleratorType { get; }

        /// <summary>
        /// Tries to resolve the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="size">The native size in bytes.</param>
        /// <returns>True, if the size could be resolved.</returns>
        bool TryGetSizeOf(TypeNode type, out int size);
    }

    /// <summary>
    /// Represents a device specializer that instantiates device-specific constants
    /// and updates device-specific functionality.
    /// </summary>
    /// <remarks>
    /// Note that this class does not perform recursive specialization operations.
    /// </remarks>
    /// <typeparam name="TConfiguration">The actual configuration type.</typeparam>
    public sealed class AcceleratorSpecializer<TConfiguration> : UnorderedTransformation
        where TConfiguration : IAcceleratorSpecializerConfiguration
    {
        #region Nested Types

        /// <summary>
        /// Specializes device constants.
        /// </summary>
        private struct Specalizer
        {
            public Specalizer(Method.Builder builder)
            {
                Builder = builder;
                Applied = false;
            }

            /// <summary>
            /// Returns the current builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns true if the current specializer has been applied.
            /// </summary>
            public bool Applied { get; private set; }

            /// <summary>
            /// Specializes the given value.
            /// </summary>
            /// <param name="value">The value to specialize.</param>
            /// <param name="constant">The constant to replace the value with.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Specialize(Value value, int constant)
            {
                var blockBuilder = Builder[value.BasicBlock];

                blockBuilder.SetupInsertPosition(value);
                var primitiveSize = blockBuilder.CreatePrimitiveValue(constant);
                value.Replace(primitiveSize);
                blockBuilder.Remove(value);

                Applied = true;
            }
        }

        #endregion

        private readonly TConfiguration configuration;

        /// <summary>
        /// Constructs a new device specializer.
        /// </summary>
        public AcceleratorSpecializer(in TConfiguration specializerConfiguration)
        {
            configuration = specializerConfiguration;
        }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();
            var specializer = new Specalizer(builder);

            var nativeWarpSize = configuration.WarpSize;
            foreach (Value value in scope.Values)
            {
                switch (value)
                {
                    case AcceleratorTypeValue _:
                        specializer.Specialize(value, (int)configuration.AcceleratorType);
                        break;
                    case WarpSizeValue _ when nativeWarpSize.HasValue:
                        specializer.Specialize(value, nativeWarpSize.Value);
                        break;
                    case SizeOfValue sizeOfValue when
                        configuration.TryGetSizeOf(sizeOfValue.TargetType, out int size):
                        specializer.Specialize(value, size);
                        break;
                }
            }

            return specializer.Applied;
        }
    }
}

