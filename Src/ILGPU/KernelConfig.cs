using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ILGPU
{
    /// <summary>
    /// A single kernel configuration for an explicitly grouped kernel.
    /// </summary>
    public readonly struct KernelConfig
    {
        #region Static

        internal static readonly ConstructorInfo ImplicitlyGroupedKernelConstructor =
            typeof(KernelConfig).GetConstructor(new Type[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int)
                });

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index gridDimension, Index groupDimension)
            : this(gridDimension, groupDimension, 0)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemorySize">The dynamic shared memory size in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index gridDimension,
            Index groupDimension,
            int sharedMemorySize)
            : this(
                  new Index3(gridDimension.X, 1, 1),
                  new Index3(groupDimension.X, 1, 1),
                  sharedMemorySize)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index2 gridDimension, Index2 groupDimension)
            : this(gridDimension, groupDimension, 0)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemorySize">The dynamic shared memory size in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index2 gridDimension,
            Index2 groupDimension,
            int sharedMemorySize)
            : this(
                  new Index3(gridDimension.X, gridDimension.Y, 1),
                  new Index3(groupDimension.X, groupDimension.Y, 1),
                  sharedMemorySize)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(Index3 gridDimension, Index3 groupDimension)
            : this(gridDimension, groupDimension, 0)
        { }

        /// <summary>
        /// Constructs a new kernel configuration that does not use any dynamically
        /// specified shared memory.
        /// </summary>
        /// <param name="gridDimX">The grid dimension in X dimension.</param>
        /// <param name="gridDimY">The grid dimension in Y dimension.</param>
        /// <param name="gridDimZ">The grid dimension in Z dimension.</param>
        /// <param name="groupDimX">The group dimension in X dimension.</param>
        /// <param name="groupDimY">The group dimension in Y dimension.</param>
        /// <param name="groupDimZ">The group dimension in Z dimension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            int gridDimX, int gridDimY, int gridDimZ,
            int groupDimX, int groupDimY, int groupDimZ)
            : this(
                  new Index3(gridDimX, gridDimY, gridDimZ),
                  new Index3(groupDimX, groupDimY, groupDimZ),
                  0)
        { }

        /// <summary>
        /// Constructs a new kernel configuration.
        /// </summary>
        /// <param name="gridDimension">The grid dimension to use.</param>
        /// <param name="groupDimension">The group dimension to use.</param>
        /// <param name="sharedMemorySize">The dynamic shared memory size in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KernelConfig(
            Index3 gridDimension,
            Index3 groupDimension,
            int sharedMemorySize)
        {
            Debug.Assert(gridDimension.Size >= 0, "Invalid grid dimension");
            Debug.Assert(groupDimension.Size >= 0, "Invalid group dimension");
            Debug.Assert(sharedMemorySize >= 0, "Invalid shared memory size");

            GridDimension = gridDimension;
            GroupDimension = groupDimension;
            DynamicSharedMemorySize = sharedMemorySize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the global grid dimension.
        /// </summary>
        public Index3 GridDimension { get; }

        /// <summary>
        /// Returns the global group dimension of each group.
        /// </summary>
        public Index3 GroupDimension { get; }

        /// <summary>
        /// Returns the associated dynamic memory size in bytes.
        /// </summary>
        public int DynamicSharedMemorySize { get; }

        /// <summary>
        /// Returns true if the current configuration uses dynamic shared memory.
        /// </summary>
        public bool UsesDynamicSharedMemory => DynamicSharedMemorySize > 0;

        /// <summary>
        /// Returns true if this configuration is a valid launch configuration.
        /// </summary>
        public bool IsValid =>
            (GridDimension.X > 0 & GridDimension.Y > 0 & GridDimension.Z > 0) &&
            (GroupDimension.X > 0 & GroupDimension.Y > 0 & GroupDimension.Z > 0) &&
            DynamicSharedMemorySize >= 0;

        #endregion

        #region Methods

        /// <summary>
        /// Converts the current instance into a dimension tuple.
        /// </summary>
        /// <returns>A dimension tuple representing this kernel configuration.</returns>
        public (Index3, Index3) ToDimensions() => (GridDimension, GroupDimension);

        /// <summary>
        /// Converts the current instance into a value tuple.
        /// </summary>
        /// <returns>A value tuple representing this kernel configuration.</returns>
        public (Index3, Index3, int) ToValueTuple() =>
            (GridDimension, GroupDimension, DynamicSharedMemorySize);

        /// <summary>
        /// Deconstructs the current instance into a dimension tuple.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Index3 gridDimension, out Index3 groupDimension)
        {
            gridDimension = GridDimension;
            groupDimension = GroupDimension;
        }

        /// <summary>
        /// Deconstructs the current instance into a value tuple.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        /// <param name="sharedMemorySize">The shared memory size in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Index3 gridDimension,
            out Index3 groupDimension,
            out int sharedMemorySize)
        {
            gridDimension = GridDimension;
            groupDimension = GroupDimension;
            sharedMemorySize = DynamicSharedMemorySize;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3(dimensions.Item2, 1, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index2) dimensions) =>
            new KernelConfig(dimensions.Item1, new Index3(dimensions.Item2, 1));

        /// <summary>
        /// Converts the given dimension tuple into an equivalent kernel configuration.
        /// </summary>
        /// <param name="dimensions">The kernel dimensions.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KernelConfig((Index3, Index3) dimensions) =>
            new KernelConfig(dimensions.Item1, dimensions.Item2);

        /// <summary>
        /// Converts the given kernel configuration into an equivalent dimension tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator (Index3, Index3)(KernelConfig config) =>
            config.ToDimensions();

        /// <summary>
        /// Converts the given kernel configuration into an equivalent value tuple.
        /// </summary>
        /// <param name="config">The kernel configuration to convert.</param>
        public static implicit operator (Index3, Index3, int)(KernelConfig config) =>
            config.ToValueTuple();

        #endregion
    }
}
