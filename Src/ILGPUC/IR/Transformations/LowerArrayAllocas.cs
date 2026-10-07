// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerArrayAllocas.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Turns every stack allocation of several elements into a per-thread
/// <see cref="Global"/> in local memory, which every backend allocates.
/// </summary>
/// <remarks>
/// The frontend allocates local memory per thread on the stack when its length
/// is not a constant yet, like the length computed from the extent of a 2D or
/// 3D allocation, which inlining and constant folding make a constant. This
/// pass runs after the inlining of the backend pipeline and rejects an
/// allocation whose length is still not a compile-time constant.
/// </remarks>
sealed class LowerArrayAllocas(TransformationArgs args) : Transformation(args)
{
    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapBasicBlockValue<Alloca>(Lower);
    }

    /// <summary>
    /// Lowers an allocation of several elements to a global.
    /// </summary>
    private static Value? Lower(BasicBlockTransform transform, Alloca alloca)
    {
        int arrayLength;
        if (alloca.IsStaticAllocation(out var staticLength))
        {
            if (staticLength.Int32Value <= 1)
                return alloca;
            arrayLength = staticLength.Int32Value;
        }
        else if (!TryGetConstantLength(
            alloca.ArrayLengthValue.AsNotNull(),
            out arrayLength))
        {
            throw alloca.Location.GetNotSupportedException(
                "Group.GetLocalMemoryPerThread requires a length that is a " +
                "compile-time constant. Expressions involving kernel " +
                "parameters or other runtime values are not supported — " +
                "declare the length or the extent as constants.");
        }

        return transform.ModuleBuilder.CreateGlobal(
            alloca.Location,
            transform.Rewrite(alloca.AllocType),
            MemoryAddressSpace.Local,
            transform.CreatePrimitiveValue(alloca.Location, arrayLength));
    }

    /// <summary>
    /// Determines a positive constant length, also through phi values whose
    /// arguments are all the same constant.
    /// </summary>
    private static bool TryGetConstantLength(Value value, out int length)
    {
        length = 0;
        switch (value)
        {
            case PrimitiveValue primitive:
                length = primitive.Int32Value;
                return length > 0;
            case PhiValue phi:
                for (int i = 0; i < phi.NumArguments; ++i)
                {
                    var argument = phi.Arguments[i];
                    if (argument == phi)
                        continue;
                    if (argument is not PrimitiveValue primitiveArgument ||
                        length != 0 && primitiveArgument.Int32Value != length)
                    {
                        return false;
                    }
                    length = primitiveArgument.Int32Value;
                }
                return length > 0;
            default:
                return false;
        }
    }
}
