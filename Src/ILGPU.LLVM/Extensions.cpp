// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Extensions.cpp
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------
//
// This file uses LLVM includes. See README.md and LICENSE-3RD-PARTY.txt for
// details about the LLVM license.
//
// -----------------------------------------------------------------------------


#include "ILGPU.LLVM.h"
#include <llvm/Pass.h>
#include <llvm/IR/Value.h>
#include <llvm/IR/PassManager.h>
#include <llvm/InitializePasses.h>
#include <llvm/Transforms/IPO.h>
#include <llvm/IR/LegacyPassManager.h>

namespace llvm
{
    extern FunctionPass *createNVVMReflectPass(const StringMap<int> &Mapping);
}

using namespace llvm;

FunctionPass* CreateNVVMReflectPass(LLVMBool ftz, LLVMBool fm)
{
    StringMap<int> mapping;
    if (ftz)
        mapping["__CUDA_FTZ"] = 1;
    if (fm)
    {
        mapping["__CUDA_PREC_DIV"] = 0;
        mapping["__CUDA_PREC_SQRT"] = 0;
    }
    return createNVVMReflectPass(mapping);
}

extern "C"
{
    void ILGPULLVM_API ILGPU_RunNVVMReflectPassOnFunction(LLVMModuleRef module, LLVMValueRef func, LLVMBool ftz, LLVMBool fm)
    {
        auto target = unwrap(func);
        legacy::FunctionPassManager fpm(unwrap(module));
        fpm.add(CreateNVVMReflectPass(ftz, fm));
        fpm.run(*(Function*)target);
    }

    void ILGPULLVM_API ILGPU_RunNVVMReflectPass(LLVMModuleRef module, LLVMBool ftz, LLVMBool fm)
    {
        legacy::PassManager pm;
        pm.add(CreateNVVMReflectPass(ftz, fm));
        pm.run(*unwrap(module));
    }

    void ILGPULLVM_API ILGPU_PreparePTXModule(LLVMModuleRef module, LLVMValueRef entry, LLVMBool ftz, LLVMBool fm)
    {
        auto entryPoint = unwrap<GlobalValue>(entry);
        legacy::PassManager pm;
        pm.add(createInternalizePass([&](const GlobalValue &val)
        {
            return &val == entryPoint;
        }));
        pm.add(createGlobalDCEPass());
        pm.add(CreateNVVMReflectPass(ftz, fm));
        pm.run(*unwrap(module));
    }
}
