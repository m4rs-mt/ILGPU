// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPUC.IR;

interface IValueContext
{
    bool TryGetReplaced(Value? oldValue, out Value? newValue);

    ReadOnlyMemory<Value> GetValues(Value value);

    int GetNumUses(Value value);

    UseCollection GetUses(Value value);
}

// abstract class ValueContext : IValueContext
// {
//     public abstract bool TryGetReplaced(Value? oldValue, out Value? newValue);
// }

