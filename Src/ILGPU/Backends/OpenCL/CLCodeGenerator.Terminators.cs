// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.Terminators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(ReturnTerminator)"/>
        public void Visit(ReturnTerminator returnTerminator)
        {
            using (var statement = BeginStatement(CLInstructions.ReturnStatement))
            {
                if (!returnTerminator.IsVoidReturn)
                {
                    var resultRegister = LoadIntrinsic(returnTerminator.ReturnValue);
                    statement.AppendArgument(resultRegister);
                }
            }
        }

        /// <summary cref="IValueVisitor.Visit(UnconditionalBranch)"/>
        public void Visit(UnconditionalBranch branch)
        {
            // TODO: implement
            throw new NotImplementedException();
        }

        /// <summary cref="IValueVisitor.Visit(ConditionalBranch)"/>
        public void Visit(ConditionalBranch branch)
        {
            // TODO: implement
            throw new NotImplementedException();
        }

        /// <summary cref="IValueVisitor.Visit(SwitchBranch)"/>
        public void Visit(SwitchBranch branch)
        {
            // TODO: implement
            throw new NotImplementedException();
        }
    }
}
