namespace ClickForge.Core;

// Selected-window setups use the existing selected gate and never change the
// user's saved ordinary-mode restriction.
public sealed class SetupTargetGate(ITargetGate ordinary, IVariableTargetGate selected, Func<bool> requiresSelection) : ITargetGate
{
    public bool CanClick => requiresSelection() ? selected.CanClick : ordinary.CanClick;
}
