// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Backends;

/// <summary>
/// Handles emission of method declarations and implementations.
/// </summary>
/// <remarks>
/// Constructs a new method emitter.
/// </remarks>
sealed class MethodEmitter(
    GenerationContext context,
    TypeEmitter typeEmitter,
    ExpressionEmitter expressionEmitter)
{
    /// <summary>
    /// Tracks the current loop being emitted, for break detection.
    /// </summary>
    private LoopStatement? _currentLoop;

    /// <summary>
    /// Emits method declarations (forward declarations).
    /// </summary>
    public void EmitMethodDeclarations()
    {
        foreach (var method in context.Module.MethodsInReversePostOrder)
        {
            if (method == context.Module.EntryPoint)
                continue; // Entry point declaration is different

            // Prepend the device-function attribute (CUDA/HIP __device__)
            // so the forward declaration matches the definition. nvcc
            // rejects mismatched function attributes between decl and def.
            var deviceAttr = context.LanguageConfig.DeviceFunctionAttribute;
            if (!string.IsNullOrEmpty(deviceAttr))
                context.Write(deviceAttr + " ");

            EmitMethodSignature(method);
            context.WriteLine(";");
            context.WriteLine();
        }
    }

    /// <summary>
    /// Emits all method implementations.
    /// </summary>
    public void EmitMethodImplementations()
    {
        foreach (var method in context.Module.MethodsInReversePostOrder)
        {
            EmitMethod(method);
            context.WriteLine();
        }
    }

    /// <summary>
    /// Emits a complete method implementation.
    /// </summary>
    private void EmitMethod(Method method)
    {
        // Add kernel attribute for the entry point, device attribute for
        // every other method. CUDA / HIP need __device__ on non-entry
        // functions so the kernel can call them; Metal / OpenCL / CPU
        // return empty here and behave as before.
        if (method == context.Module.EntryPoint)
            context.Write(context.LanguageConfig.KernelAttribute + " ");
        else
        {
            var deviceAttr = context.LanguageConfig.DeviceFunctionAttribute;
            if (!string.IsNullOrEmpty(deviceAttr))
                context.Write(deviceAttr + " ");
        }

        // Emit signature
        EmitMethodSignature(method);
        context.WriteLine();
        context.OpenScope();

        // Emit method body
        if (method.HasImplementation)
        {
            EmitMethodBody(method);
        }

        context.CloseScope();
    }

    /// <summary>
    /// Emits a method signature.
    /// For the kernel entry point, this also appends language-specific thread
    /// built-in parameters (Metal only).
    /// </summary>
    private void EmitMethodSignature(Method method)
    {
        bool isEntryPoint = method == context.Module.EntryPoint;

        var returnType = typeEmitter.GetTypeName(method.Type);
        var methodName = context.GetValueName(method);

        context.Write($"{returnType} {methodName}(");

        bool first = true;
        int bufferIndex = 0;
        bool skipsIndex = isEntryPoint
            && context.LanguageConfig.SkipsIndexParameter;

        foreach (var param in method.Parameters)
        {
            // GPU backends: skip the first parameter (thread index) —
            // it will be computed from built-in thread attributes
            if (skipsIndex && param.Index == 0)
                continue;

            if (!first) context.Write(", ");
            first = false;
            EmitParameter(param, isEntryPoint, ref bufferIndex);
        }

        // Append language-specific built-in parameters (Metal thread position, etc.)
        if (isEntryPoint)
        {
            foreach (var builtIn in
                context.LanguageConfig.GetKernelBuiltInParameters())
            {
                if (!first) context.Write(", ");
                first = false;
                context.Write(builtIn);
            }
        }

        context.Write(")");
    }

    /// <summary>
    /// Emits a parameter declaration.
    /// When emitting the kernel entry point and the backend requires buffer index
    /// attributes (Metal), delegates to
    /// <see cref="LanguageConfiguration.EmitNonFlattenedKernelParam"/>.
    /// </summary>
    private void EmitParameter(Parameter param, bool isEntryPoint, ref int bufferIndex)
    {
        var typeName = typeEmitter.GetTypeName(param.Type);
        var paramName = context.GetValueName(param);

        if (isEntryPoint)
        {
            var paramCode = context.LanguageConfig.EmitNonFlattenedKernelParam(
                param.Type,
                typeName,
                paramName,
                bufferIndex);
            context.Write(paramCode);
            ++bufferIndex;
        }
        else
        {
            context.Write($"{typeName} {paramName}");
        }
    }

    /// <summary>
    /// Emits the body of a method.
    /// </summary>
    private void EmitMethodBody(Method method)
    {
        // GPU backends: compute the thread index from built-in attributes
        // instead of receiving it as a buffer parameter.
        if (method == context.Module.EntryPoint
            && context.LanguageConfig.SkipsIndexParameter
            && method.NumParameters > 0)
        {
            var indexParam = method.Parameters[0];
            var typeName = typeEmitter.GetTypeName(indexParam.Type);
            var paramName = context.GetValueName(indexParam);
            var computation = context.LanguageConfig.EmitIndexComputation(
                typeName, paramName, indexParam.Type);
            if (computation != null)
                context.WriteLine(computation);
        }

        // Emit function-scoped globals (e.g. Metal thread/threadgroup arrays).
        // These can't be at file scope in some languages.
        EmitFunctionScopedGlobals(method);

        // Create code placement analysis to order values correctly
        var codePlacement = method.CreateCodePlacement();

        // Reconstruct control flow
        var reconstructor = new ControlFlowReconstructor(method);
        var controlFlow = reconstructor.ReconstructControlFlow();

        // Allocate variables for SSA values that need them
        var ssaAllocator = new SSAVariableAllocator(context, method);
        ssaAllocator.AllocateVariables();

        // Emit the control flow structure
        EmitControlFlow(controlFlow, codePlacement);
    }

    /// <summary>
    /// Emits global variable declarations that must be function-scoped
    /// for this backend (e.g. Metal thread/threadgroup variables).
    /// </summary>
    private void EmitFunctionScopedGlobals(Method method)
    {
        // Only the entry point owns the globals
        if (method != context.Module.EntryPoint)
            return;

        foreach (var global in context.Module.Globals)
        {
            if (context.LanguageConfig.IsFileScopeAddressSpace(global.AddressSpace))
                continue;
            CodeGenerator.EmitGlobalDeclaration(context, typeEmitter, global);
        }
    }

    /// <summary>
    /// Emits a control flow structure.
    /// </summary>
    private void EmitControlFlow(ControlFlowStructure structure, CodePlacement placement)
    {
        foreach (var element in structure.Elements)
        {
            switch (element)
            {
                case BasicBlock block:
                    EmitBasicBlock(block, placement);
                    break;

                case IfStatement ifStmt:
                    EmitIfStatement(ifStmt, placement);
                    break;

                case LoopStatement loopStmt:
                    EmitLoopStatement(loopStmt, placement);
                    break;

                case SwitchStatement switchStmt:
                    EmitSwitchStatement(switchStmt, placement);
                    break;

                case UnstructuredRegion region:
                    EmitUnstructuredRegion(region, placement);
                    break;
            }
        }
    }

    /// <summary>
    /// Emits a basic block's contents (without control flow).
    /// Labels are only emitted for backends that support labeled statements
    /// (CUDA/HIP/OpenCL); Metal MSL does not permit them.
    /// Note: no <c>goto</c> is ever generated, so labels are dead code in all backends;
    /// but only Metal rejects them as a compile error.
    /// </summary>
    private void EmitBasicBlock(BasicBlock block, CodePlacement placement)
    {
        if (context.LanguageConfig.SupportsFeature(LanguageFeature.LabeledStatements))
        {
            var blockName = context.GetValueName(block);
            context.WriteLine($"{blockName}:");
        }

        // Get the placed block with all values in correct order
        var placedBlock = placement.GetPlacedBlock(block);

        // Emit all values in placement order (excluding phi values)
        foreach (var value in placedBlock)
        {
            // Skip phi values (handled separately below)
            if (value is PhiValue)
                continue;

            EmitValue(value);
        }

        // Emit return statement if this block has return termination
        if (block.TerminationKind == BlockTerminationKind.Return)
        {
            var returnView = block.AsReturnView();
            if (returnView.IsVoidReturn)
                context.WriteLine("return;");
            else
            {
                var returnExpr = expressionEmitter.EmitExpression(returnView.ReturnValue);
                context.WriteLine($"return {returnExpr};");
            }
        }
    }

    /// <summary>
    /// Emits a value (either BasicBlockValue or PureValue).
    /// </summary>
    private void EmitValue(Value<Method> value)
    {
        // Pure values are typically inlined, but if they're placed explicitly,
        // we may need to emit them as variable assignments
        if (value is PureValue pureValue)
        {
            EmitPureValue(pureValue);
            return;
        }

        // Handle basic block values
        if (value is BasicBlockValue bbValue)
        {
            EmitBasicBlockValue(bbValue);
        }
    }

    /// <summary>
    /// Emits a pure value as a variable assignment if needed.
    /// </summary>
    private void EmitPureValue(PureValue value)
    {
        // Only emit assignment if this value was declared as a variable
        if (!context.NeedsVariable(value))
            return;

        var varName = context.GetValueName(value);
        var expr = expressionEmitter.EmitDefinition(value);
        EmitVariableAssignment(value, varName, expr);
    }

    /// <summary>
    /// Emits a basic block value (instruction).
    /// </summary>
    private void EmitBasicBlockValue(BasicBlockValue value)
    {
        // Alloca needs special handling: emit as array + pointer pair
        if (value is Alloca alloca)
        {
            var decl = expressionEmitter.EmitAllocaDeclaration(alloca);
            foreach (var line in decl.Split('\n'))
                context.WriteLine(line);
            return;
        }

        // Check if this value produces a result that needs to be stored
        if (value.Type is not VoidType && context.NeedsVariable(value))
        {
            var varName = context.GetValueName(value);
            var expr = expressionEmitter.EmitDefinition(value);
            EmitVariableAssignment(value, varName, expr);
        }
        else
        {
            // Statement-only instruction (e.g., store, barrier)
            var stmt = expressionEmitter.EmitStatement(value);
            if (!string.IsNullOrEmpty(stmt))
                context.WriteLine(stmt);
        }
    }

    /// <summary>
    /// Emits a variable assignment. All variables are pre-declared at
    /// function scope by SSAVariableAllocator, so this only emits the
    /// assignment. Alloca values never reach this path (they use
    /// ExpressionEmitter.EmitAllocaDeclaration instead).
    /// </summary>
    private void EmitVariableAssignment(Value value, string varName, string expr) =>
        context.WriteLine($"{varName} = {expr};");

    /// <summary>
    /// Emits an if-statement.
    /// </summary>
    private void EmitIfStatement(IfStatement ifStmt, CodePlacement placement)
    {
        var condition = expressionEmitter.EmitExpression(ifStmt.Condition);
        context.WriteLine($"if ({condition})");
        context.OpenScope();
        EmitControlFlow(ifStmt.TrueBranch, placement);
        EmitBranchPhiTransitions(ifStmt.TrueBranch, ifStmt.Block);
        EmitBreakIfExitsLoop(ifStmt.TrueBranch, ifStmt.Block);
        context.CloseScope();

        // Always emit the else block if it has body, phi transitions,
        // or an exit edge (break). Empty false branches (e.g., from
        // `continue`) can still need phi assignments at the merge block.
        bool hasElseBody = ifStmt.FalseBranch.Elements.Count > 0;
        bool hasElsePhis = HasBranchPhiTransitions(
            ifStmt.FalseBranch, ifStmt.Block);
        bool hasElseBreak = BranchExitsLoop(
            ifStmt.FalseBranch, ifStmt.Block);
        if (hasElseBody || hasElsePhis || hasElseBreak)
        {
            context.WriteLine("else");
            context.OpenScope();
            EmitControlFlow(ifStmt.FalseBranch, placement);
            EmitBranchPhiTransitions(ifStmt.FalseBranch, ifStmt.Block);
            EmitBreakIfExitsLoop(ifStmt.FalseBranch, ifStmt.Block);
            context.CloseScope();
        }
    }

    /// <summary>
    /// Returns true if a branch exits the current loop (break edge).
    /// </summary>
    private bool BranchExitsLoop(
        ControlFlowStructure branch,
        BasicBlock conditionBlock)
    {
        if (_currentLoop == null) return false;

        BasicBlock? lastBlock = null;
        foreach (var element in branch.Elements)
        {
            if (element is BasicBlock bb)
                lastBlock = bb;
        }
        var sourceBlock = lastBlock ?? conditionBlock;

        foreach (var succ in sourceBlock.Successors)
        {
            if (!_currentLoop.Loop.AllMembers.Contains(succ))
                return true;
        }
        return false;
    }

    /// <summary>
    /// If a branch exits the current loop, emits exit phi transitions
    /// and a <c>break;</c> statement.
    /// </summary>
    private void EmitBreakIfExitsLoop(
        ControlFlowStructure branch,
        BasicBlock conditionBlock)
    {
        if (_currentLoop == null) return;

        BasicBlock? lastBlock = null;
        foreach (var element in branch.Elements)
        {
            if (element is BasicBlock bb)
                lastBlock = bb;
        }
        var sourceBlock = lastBlock ?? conditionBlock;

        foreach (var succ in sourceBlock.Successors)
        {
            if (!_currentLoop.Loop.AllMembers.Contains(succ))
            {
                // Emit phi transitions for the exit block
                EmitStructuredPhiTransitions(sourceBlock, succ);
                context.WriteLine("break;");
                return;
            }
        }
    }

    /// <summary>
    /// Returns true if the given branch has phi transitions to emit.
    /// </summary>
    private bool HasBranchPhiTransitions(
        ControlFlowStructure branch,
        BasicBlock conditionBlock)
    {
        BasicBlock? lastBlock = null;
        foreach (var element in branch.Elements)
        {
            if (element is BasicBlock bb)
                lastBlock = bb;
        }
        var sourceBlock = lastBlock ?? conditionBlock;

        foreach (var succ in sourceBlock.Successors)
        {
            foreach (var phi in succ.PhiValues)
            {
                for (int i = 0; i < phi.NumArguments; i++)
                {
                    if (phi.Sources[i] == sourceBlock)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Emits phi assignments for a branch's last block → its successor.
    /// If the branch is empty, uses the condition block as the source.
    /// </summary>
    private void EmitBranchPhiTransitions(
        ControlFlowStructure branch,
        BasicBlock conditionBlock)
    {
        // Find the last basic block in the branch
        BasicBlock? lastBlock = null;
        foreach (var element in branch.Elements)
        {
            if (element is BasicBlock bb)
                lastBlock = bb;
        }

        // If branch is empty, the condition block transitions directly
        var sourceBlock = lastBlock ?? conditionBlock;

        foreach (var succ in sourceBlock.Successors)
        {
            // Skip transitions to the current loop header — those are
            // handled by the loop emitter's back-edge phi code (for-loop
            // increment + EmitLoopBackEdgePhis). Emitting them here too
            // would cause double-increments and duplicate phi assignments.
            if (_currentLoop != null &&
                _currentLoop.Loop.Headers.Length > 0 &&
                succ == _currentLoop.Loop.Headers[0])
                continue;

            EmitStructuredPhiTransitions(sourceBlock, succ);
        }
    }

    /// <summary>
    /// Emits a loop statement with proper classification (for, while, do-while).
    /// </summary>
    private void EmitLoopStatement(LoopStatement loopStmt, CodePlacement placement)
    {
        var previousLoop = _currentLoop;
        _currentLoop = loopStmt;

        switch (loopStmt)
        {
            case ForLoop forLoop:
                EmitForLoop(forLoop, placement);
                break;

            case WhileLoop whileLoop:
                EmitWhileLoop(whileLoop, placement);
                break;

            case DoWhileLoop doWhileLoop:
                EmitDoWhileLoop(doWhileLoop, placement);
                break;

            default:
                // Fallback: emit as infinite loop
                context.WriteLine("while (true)");
                context.OpenScope();
                EmitControlFlow(loopStmt.Body, placement);
                context.CloseScope();
                break;
        }

        _currentLoop = previousLoop;
    }

    /// <summary>
    /// Emits a for-loop with induction variable.
    /// </summary>
    private void EmitForLoop(ForLoop forLoop, CodePlacement placement)
    {
        // Self-loops (header == back-edge) have do-while semantics:
        // the body always executes before the condition is checked.
        // Emit as do { body; step } while (condition) to avoid off-by-one.
        if (forLoop.IsSelfLoop)
        {
            EmitSelfLoopAsDoWhile(forLoop, placement);
            return;
        }

        var inductionVar = forLoop.InductionVariable;
        // Use EmitFullyInlined to recursively expand all operands.
        // The condition value may reference variables declared inside the
        // loop body, so we must inline them to avoid forward references.
        var condition = expressionEmitter.EmitFullyInlined(forLoop.Condition);

        // Build the initializer: var = initialValue
        var varName = context.GetValueName(inductionVar.Phi);
        var initial = expressionEmitter.EmitExpression(inductionVar.InitialValue);

        // Build the increment: var += step or var -= step
        var step = expressionEmitter.EmitExpression(inductionVar.StepValue);
        var incrementOp = inductionVar.IsIncrement ? "+=" : "-=";
        var increment = $"{varName} {incrementOp} {step}";

        // Emit initial values for non-induction header phis before the loop
        var header = forLoop.Loop.Headers[0];
        EmitLoopPhiInitializers(header, forLoop.Loop, inductionVar.Phi);

        context.WriteLine($"for ({varName} = {initial}; {condition}; {increment})");
        context.OpenScope();
        EmitControlFlow(forLoop.Body, placement);

        // Emit phi updates for non-induction header phis. The back-edge
        // block is excluded from the body by the reconstructor, so its
        // phi assignments are lost. Emit them at the end of the loop body.
        foreach (var value in header.PhiValues)
        {
            // Skip the induction variable — handled by the for increment
            if (value == inductionVar.Phi) continue;

            // Find the incoming value from a back-edge source
            for (int i = 0; i < value.NumArguments; i++)
            {
                bool isBackEdge = false;
                foreach (var be in forLoop.Loop.BackEdges)
                {
                    if (be == value.Sources[i]) { isBackEdge = true; break; }
                }
                if (!isBackEdge) continue;
                var phiVar = context.GetValueName(value);
                var incoming = expressionEmitter.EmitExpression(
                    value.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }

        context.CloseScope();
    }

    /// <summary>
    /// Emits a self-loop ForLoop as do-while. In a self-loop, the header
    /// block IS the back-edge, so body instructions execute before the
    /// conditional branch. Using a standard for-loop would check the
    /// condition before the body, causing an off-by-one iteration count.
    /// </summary>
    private void EmitSelfLoopAsDoWhile(ForLoop forLoop, CodePlacement placement)
    {
        var header = forLoop.Loop.Headers[0];

        // Emit initial values for ALL header phis before the loop
        EmitLoopPhiInitializers(header, forLoop.Loop);

        // The condition is computed by the header block (part of the body).
        // We must capture it BEFORE the back-edge phi updates overwrite
        // the phi variables with new values.
        var condVar = "_loopCond";
        context.WriteLine($"bool {condVar};");

        context.WriteLine("do");
        context.OpenScope();
        EmitControlFlow(forLoop.Body, placement);

        // Capture condition before phi updates (phis still hold current values)
        var condition = expressionEmitter.EmitExpression(forLoop.Condition);
        context.WriteLine($"{condVar} = {condition};");

        EmitLoopBackEdgePhis(header, forLoop.Loop);
        context.CloseScope();
        context.WriteLine($"while ({condVar});");
    }

    /// <summary>
    /// Emits a while loop.
    /// </summary>
    private void EmitWhileLoop(WhileLoop whileLoop, CodePlacement placement)
    {
        var header = whileLoop.Loop.Headers[0];
        EmitLoopPhiInitializers(header, whileLoop.Loop);

        // Use EmitFullyInlined to avoid forward references to body-scoped vars.
        var condition = expressionEmitter.EmitFullyInlined(whileLoop.Condition);
        context.WriteLine($"while ({condition})");
        context.OpenScope();
        EmitControlFlow(whileLoop.Body, placement);
        EmitLoopBackEdgePhis(header, whileLoop.Loop);
        context.CloseScope();
    }

    /// <summary>
    /// Emits a do-while loop.
    /// </summary>
    private void EmitDoWhileLoop(DoWhileLoop doWhileLoop, CodePlacement placement)
    {
        var header = doWhileLoop.Loop.Headers[0];
        EmitLoopPhiInitializers(header, doWhileLoop.Loop);

        context.WriteLine("do");
        context.OpenScope();
        EmitControlFlow(doWhileLoop.Body, placement);
        EmitLoopBackEdgePhis(header, doWhileLoop.Loop);
        context.CloseScope();

        // Use EmitDefinition to inline the condition expression directly.
        var condition = expressionEmitter.EmitDefinition(doWhileLoop.Condition);
        context.WriteLine($"while ({condition});");
    }

    /// <summary>
    /// Emits phi assignments for back-edge transitions into the loop header.
    /// Used at the end of while/do-while loop bodies.
    /// </summary>
    private void EmitLoopBackEdgePhis(
        BasicBlock header,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop)
    {
        foreach (var value in header.PhiValues)
        {
            for (int i = 0; i < value.NumArguments; i++)
            {
                bool isBackEdge = false;
                foreach (var be in loop.BackEdges)
                {
                    if (be == value.Sources[i])
                    { isBackEdge = true; break; }
                }
                if (!isBackEdge) continue;

                var phiVar = context.GetValueName(value);
                var incoming = expressionEmitter.EmitExpression(
                    value.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    /// <summary>
    /// Emits a switch statement.
    /// </summary>
    private void EmitSwitchStatement(SwitchStatement switchStmt, CodePlacement placement)
    {
        var condition = expressionEmitter.EmitExpression(switchStmt.Condition);
        context.WriteLine($"switch ({condition})");
        context.OpenScope();

        // Emit each case
        foreach (var (caseValue, body) in switchStmt.Cases)
        {
            context.WriteLine($"case {caseValue}:");
            context.PushIndent();
            EmitControlFlow(body, placement);
            // Emit phi transitions for case → merge block
            EmitBranchPhiTransitions(body, switchStmt.Block);
            context.WriteLine("break;");
            context.PopIndent();
        }

        // Emit default case
        if (switchStmt.DefaultCase.Elements.Count > 0)
        {
            context.WriteLine("default:");
            context.PushIndent();
            EmitControlFlow(switchStmt.DefaultCase, placement);
            // Emit phi transitions for default case → merge block
            EmitBranchPhiTransitions(switchStmt.DefaultCase, switchStmt.Block);
            context.WriteLine("break;");
            context.PopIndent();
        }

        context.CloseScope();
    }

    #region Unstructured Region (State Machine)

    /// <summary>
    /// Emits an unstructured region as a while+switch state machine.
    /// Works on all GPU backends (Metal/OpenCL/CUDA/ROCm) without goto.
    /// </summary>
    private void EmitUnstructuredRegion(
        UnstructuredRegion region,
        CodePlacement placement)
    {
        int entryId = region.BlockIds[region.EntryBlock];

        // Initialize phis with external (pre-region) sources
        foreach (var block in region.Blocks)
            EmitRegionExternalPhiInits(block, region);

        context.WriteLine($"int _state = {entryId};");
        context.WriteLine("bool _done = false;");
        context.WriteLine("while (!_done)");
        context.OpenScope();
        context.WriteLine("switch (_state)");
        context.OpenScope();

        foreach (var block in region.Blocks)
        {
            int stateId = region.BlockIds[block];
            context.WriteLine($"case {stateId}:");
            context.OpenScope();
            EmitRegionBlock(block, placement);
            EmitRegionTransition(block, region);
            context.WriteLine("break;");
            context.CloseScope();
        }

        // Exit sentinel cases → _done = true
        foreach (var (_, sentinel) in region.ExitSentinels)
        {
            context.WriteLine($"case {sentinel}:");
            context.PushIndent();
            context.WriteLine("_done = true;");
            context.WriteLine("break;");
            context.PopIndent();
        }

        context.CloseScope(); // switch
        context.CloseScope(); // while
        context.WriteLine();
    }

    /// <summary>
    /// Emits a block's non-phi values within an unstructured region.
    /// </summary>
    private void EmitRegionBlock(BasicBlock block, CodePlacement placement)
    {
        var blockName = context.GetValueName(block);
        context.WriteLine($"// {blockName}");

        var placedBlock = placement.GetPlacedBlock(block);
        foreach (var value in placedBlock)
        {
            if (value is PhiValue) continue;
            EmitValue(value);
        }
    }

    /// <summary>
    /// Emits state transition logic for a block within an unstructured region.
    /// Handles unconditional, conditional, switch, and return termination kinds.
    /// </summary>
    private void EmitRegionTransition(
        BasicBlock block,
        UnstructuredRegion region)
    {
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                {
                    var succ = block.Successors[0];
                    EmitPhiTransitions(block, succ, region);
                    context.WriteLine($"_state = {region.GetTargetState(succ)};");
                    break;
                }

            case BlockTerminationKind.Conditional:
                {
                    var view = block.AsConditionalView();
                    var condition = expressionEmitter.EmitExpression(view.Condition);
                    context.WriteLine($"if ({condition})");
                    context.OpenScope();
                    EmitPhiTransitions(block, view.TrueTarget, region);
                    context.WriteLine(
                        $"_state = {region.GetTargetState(view.TrueTarget)};");
                    context.CloseScope();
                    context.WriteLine("else");
                    context.OpenScope();
                    EmitPhiTransitions(block, view.FalseTarget, region);
                    context.WriteLine(
                        $"_state = {region.GetTargetState(view.FalseTarget)};");
                    context.CloseScope();
                    break;
                }

            case BlockTerminationKind.Switch:
                {
                    var view = block.AsSwitchView();
                    var condition = expressionEmitter.EmitExpression(view.Condition);
                    context.WriteLine($"switch ({condition})");
                    context.OpenScope();
                    for (int i = 0; i < view.NumCases; i++)
                    {
                        var target = view.GetCaseTarget(i);
                        context.WriteLine($"case {i}:");
                        context.PushIndent();
                        EmitPhiTransitions(block, target, region);
                        context.WriteLine(
                            $"_state = {region.GetTargetState(target)};");
                        context.WriteLine("break;");
                        context.PopIndent();
                    }
                    context.WriteLine("default:");
                    context.PushIndent();
                    EmitPhiTransitions(block, view.DefaultTarget, region);
                    context.WriteLine(
                        $"_state = {region.GetTargetState(view.DefaultTarget)};");
                    context.WriteLine("break;");
                    context.PopIndent();
                    context.CloseScope();
                    break;
                }

            case BlockTerminationKind.Return:
                {
                    var returnView = block.AsReturnView();
                    if (returnView.IsVoidReturn)
                        context.WriteLine("return;");
                    else
                    {
                        var retExpr =
                            expressionEmitter.EmitExpression(returnView.ReturnValue);
                        context.WriteLine($"return {retExpr};");
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// Emits phi assignments for the transition edge source → target within
    /// an unstructured region.
    /// </summary>
    private void EmitPhiTransitions(
        BasicBlock source,
        BasicBlock target,
        UnstructuredRegion region)
    {
        // Only emit phis for blocks inside the region
        if (region.IsExit(target))
            return;

        foreach (var value in target.Values)
        {
            if (value is not PhiValue phi) continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (phi.Sources[i] != source) continue;

                var phiVar = context.GetValueName(phi);
                var incoming = expressionEmitter.EmitExpression(phi.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    /// <summary>
    /// Emits phi assignments for a transition edge from source to target
    /// in structured control flow. Unlike the region-specific variant, this
    /// has no region membership check.
    /// </summary>
    private void EmitStructuredPhiTransitions(
        BasicBlock source,
        BasicBlock target,
        PhiValue? skipPhi = null)
    {
        foreach (var value in target.PhiValues)
        {
            if (value == skipPhi) continue;

            for (int i = 0; i < value.NumArguments; i++)
            {
                if (value.Sources[i] != source) continue;

                var phiVar = context.GetValueName(value);
                var incoming = expressionEmitter.EmitExpression(
                    value.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    /// <summary>
    /// Emits phi initial-value assignments for a loop header's phis,
    /// using values from sources outside the loop (pre-header edges).
    /// </summary>
    private void EmitLoopPhiInitializers(
        BasicBlock header,
        Loops<ReversePostOrder<BasicBlock>, Forwards>.Node loop,
        PhiValue? skipPhi = null)
    {
        foreach (var value in header.PhiValues)
        {
            if (value == skipPhi) continue;

            for (int i = 0; i < value.NumArguments; i++)
            {
                // Only emit for sources OUTSIDE the loop
                bool isInternal = false;
                foreach (var member in loop.AllMembers)
                {
                    if (member == value.Sources[i])
                    { isInternal = true; break; }
                }
                if (isInternal) continue;

                var phiVar = context.GetValueName(value);
                var incoming = expressionEmitter.EmitExpression(
                    value.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    /// <summary>
    /// Emits initial assignments for phis in a block whose sources are outside
    /// the unstructured region. These run before the state machine loop.
    /// </summary>
    private void EmitRegionExternalPhiInits(
        BasicBlock block,
        UnstructuredRegion region)
    {
        foreach (var value in block.Values)
        {
            if (value is not PhiValue phi) continue;

            for (int i = 0; i < phi.NumArguments; i++)
            {
                if (region.BlockIds.ContainsKey(phi.Sources[i]))
                    continue;

                var phiVar = context.GetValueName(phi);
                var incoming = expressionEmitter.EmitExpression(phi.Arguments[i]);
                context.WriteLine($"{phiVar} = {incoming};");
            }
        }
    }

    #endregion

}
