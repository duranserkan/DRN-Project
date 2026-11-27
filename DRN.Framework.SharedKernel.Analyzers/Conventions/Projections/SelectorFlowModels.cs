using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct SelectorScopeKey(
    IAnonymousFunctionOperation Lambda,
    SelectorScope? ParentScope);

internal readonly record struct BranchWriteDiff(
    ILocalSymbol Local,
    IOperation? Target);

internal readonly record struct ReturnBranchGuard(
    IOperation Condition,
    bool LocalExistedUnreplaced);

internal readonly record struct RecordedReturn(
    IOperation Value,
    ILocalSymbol? ReturnedLocal,
    ImmutableArray<ReturnBranchGuard> Guards);

internal readonly record struct EnclosingFunctionBody(
    IBlockOperation Body,
    IAnonymousFunctionOperation? OwnerLambda);

internal readonly record struct ExpressionWalkEntry(
    IOperation Operation,
    bool Unconditional,
    bool IsExit = false);

internal readonly record struct UnstructuredWalkEntry(
    IOperation Operation,
    FlowState? Before = null,
    ImmutableArray<IOperation> Conditions = default,
    ISwitchOperation? SwitchOwner = null,
    ISwitchCaseOperation? SwitchSection = null,
    ISwitchExpressionOperation? SwitchExpressionOwner = null,
    ISwitchExpressionArmOperation? SwitchExpressionArm = null);

internal readonly record struct BranchGuard(
    FlowState Before,
    IOperation Condition);

internal readonly record struct MaterializedLambda(
    IAnonymousFunctionOperation Lambda,
    ImmutableArray<IOperation> Conditions);

internal readonly record struct MemoizedBodyFlow(
    Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> Incoming,
    SelectorFlowResult Result);
