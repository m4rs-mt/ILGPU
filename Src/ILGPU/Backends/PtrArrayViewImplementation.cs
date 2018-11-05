// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PtrArrayViewImpl.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents an array view that is implemented with the help of
    /// native pointers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct PtrArrayViewImplementation<T>
        where T : struct
    {
        #region Instance

        /// <summary>
        /// The base pointer.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051: DoNotDeclareVisibleInstanceFields",
            Justification = "Implementation type that simplifies code generation")]
        [SuppressMessage("Microsoft.Security", "CA2104: DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "This structure is used for marshalling purposes only. The reference will not be accessed using this structure.")]
        public readonly void* Ptr;

        /// <summary>
        /// The length.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1051: DoNotDeclareVisibleInstanceFields",
            Justification = "Implementation type that simplifies code generation")]
        public readonly int Length;

        /// <summary>
        /// Constructs a new array view implementation.
        /// </summary>
        /// <param name="ptr">The base pointer.</param>
        /// <param name="length">The length information.</param>
        public PtrArrayViewImplementation(void* ptr, int length)
        {
            Ptr = ptr;
            Length = length;
        }

        /// <summary>
        /// Constructs a new array view implementation.
        /// </summary>
        /// <param name="source">The abstract source view.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PtrArrayViewImplementation(ArrayView<T> source)
            : this(source.IsValid ? source.LoadEffectiveAddress() : null, source.Length)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public ref T this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref LoadElementAddress(index);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T LoadElementAddress(Index index) =>
            ref Unsafe.Add(ref Unsafe.AsRef<T>(Ptr), index);

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="length">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PtrArrayViewImplementation<T> GetSubView(Index offset, Index length) =>
            new PtrArrayViewImplementation<T>(
                Unsafe.AsPointer(ref this[offset]),
                length);

        /// <summary>
        /// Casts the view into another view with a different element type.
        /// </summary>
        /// <typeparam name="TOther">The other element type.</typeparam>
        /// <returns>The casted view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PtrArrayViewImplementation<TOther> Cast<TOther>()
            where TOther : struct =>
            new PtrArrayViewImplementation<TOther>(Ptr, Length);

        #endregion
    }

    /// <summary>
    /// Maps array view to pointer implementations.
    /// </summary>
    public abstract class PtrArrayViewImplementationArgumentMapper : KernelArgumentMapper
    {
        #region Nested Types

        /// <summary>
        /// Represents the internal mapping.
        /// </summary>
        private sealed class ViewMapping : Mapping
        {
            public ViewMapping(
                int ptrId,
                int lengthId,
                Type sourceType,
                Type targetType)
                : base(ptrId)
            {
                LengthId = lengthId;
                SourceType = sourceType;
                TargetType = targetType;
                ConstructorInfo =
                    PtrArrayViewImplementation.GetViewConstructor(targetType);
            }

            /// <summary>
            /// The length tuple id.
            /// </summary>
            public int LengthId { get; }

            /// <summary>
            /// Returns the source type.
            /// </summary>
            public Type SourceType { get; }

            /// <summary>
            /// Returns the target type.
            /// </summary>
            public Type TargetType { get; }

            /// <summary>
            /// Returns the associated constructor info.
            /// </summary>
            public ConstructorInfo ConstructorInfo { get; }

            /// <summary cref="KernelArgumentMapper.Mapping.EmitConversion{TILEmitter, TSource, TTarget}(in TILEmitter, in TSource, in TTarget)"/>
            public override void EmitConversion<TILEmitter, TSource, TTarget>(
                in TILEmitter emitter,
                in TSource source,
                in TTarget target)
            {
                var tempLocal = emitter.DeclareLocal(TargetType);
                source.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Ldobj, SourceType);
                emitter.EmitNewObject(ConstructorInfo);
                emitter.Emit(LocalOperation.Store, tempLocal);

                target.EmitLoadTarget(emitter, TargetId);
                emitter.Emit(LocalOperation.LoadAddress, tempLocal);
                emitter.Emit(
                    OpCodes.Ldfld,
                    PtrArrayViewImplementation.GetPtrField(TargetType));
                emitter.Emit(OpCodes.Stobj, typeof(void*));

                target.EmitLoadTarget(emitter, LengthId);
                emitter.Emit(LocalOperation.LoadAddress, tempLocal);
                emitter.Emit(
                    OpCodes.Ldfld,
                    PtrArrayViewImplementation.GetLengthField(TargetType));
                emitter.Emit(OpCodes.Stobj, typeof(int));
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        protected PtrArrayViewImplementationArgumentMapper(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary cref="KernelArgumentMapper.CreateViewTypeMapping{TTarget}(ref TTarget, Type, Type)"/>
        protected override Mapping CreateViewTypeMapping<TTarget>(
            ref TTarget target,
            Type viewType,
            Type elementType)
        {
            var ptr = target.DeclareType(typeof(void).MakePointerType());
            var length = target.DeclareType(typeof(int));

            return new ViewMapping(
                ptr,
                length,
                viewType,
                PtrArrayViewImplementation.GetArrayViewImplType(elementType));
        }

        #endregion
    }

    /// <summary>
    /// General extensions for pointer-based array view implementations.
    /// </summary>
    public static class PtrArrayViewImplementation
    {
        /// <summary>
        /// The generic implementation type.
        /// </summary>
        public static readonly Type ImplType = typeof(PtrArrayViewImplementation<>);

        /// <summary>
        /// Returns a specialized implementation type.
        /// </summary>
        /// <param name="elementType">The view element type.</param>
        /// <returns></returns>
        public static Type GetArrayViewImplType(Type elementType) =>
            ImplType.MakeGenericType(elementType);

        /// <summary>
        /// Returns a specialized pointer constructor.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved pointer constructor.</returns>
        public static ConstructorInfo GetPointerConstructor(Type implType)
        {
            return implType.GetConstructor(new Type[]
            {
                typeof(void).MakePointerType(),
                typeof(Index),
            });
        }

        /// <summary>
        /// Returns a specialized view constructor.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved view constructor.</returns>
        public static ConstructorInfo GetViewConstructor(Type implType)
        {
            return implType.GetConstructor(new Type[]
            {
                typeof(ArrayView<>).MakeGenericType(implType.GetGenericArguments()[0])
            });
        }

        /// <summary>
        /// Returns the pointer field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetPtrField(Type implType) =>
            implType.GetField(nameof(PtrArrayViewImplementation<int>.Ptr));

        /// <summary>
        /// Returns the length field of a view implementation.
        /// </summary>
        /// <param name="implType">The view implementation type.</param>
        /// <returns>The resolved field.</returns>
        public static FieldInfo GetLengthField(Type implType) =>
            implType.GetField(nameof(PtrArrayViewImplementation<int>.Length));
    }
}
