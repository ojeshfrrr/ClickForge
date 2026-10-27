namespace ClickForge.Core;

public enum OutputInputType { Mouse, Keyboard }

// The existing Button discriminator persists the type without a second setting
// that could disagree with it. Version-1 profiles and older settings stay valid.
public static class OutputSelection
{
    public static OutputInputType TypeOf(ClickButton button)
    {
        ClickButtons.Validate(button);
        return button == ClickButton.CustomKey ? OutputInputType.Keyboard : OutputInputType.Mouse;
    }
    public static Settings ChangeType(Settings current, OutputInputType type, ClickButton mouseButton = ClickButton.Left)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Choose Mouse or Keyboard output.");
        if (type == OutputInputType.Mouse && !ClickButtons.MouseChoices.Contains(mouseButton))
            throw new ArgumentException("Choose Left, Right, Mouse 4 (Back), or Mouse 5 (Forward).");
        var candidate = current with { Button = type == OutputInputType.Keyboard ? ClickButton.CustomKey : mouseButton };
        candidate.Validate(); // Includes retained key validity and global shortcut conflicts.
        return candidate;
    }
}
