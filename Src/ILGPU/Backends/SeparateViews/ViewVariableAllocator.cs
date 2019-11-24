// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ViewVariableAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Backends.SeparateViews
{
    /// <summary>
    /// Represents a variable allocator that uses the <see cref="ViewImplementation"/>
    /// as native view representation.
    /// </summary>
    public abstract class ViewVariableAllocator : PointerViews.ViewVariableAllocator
    { }
}
