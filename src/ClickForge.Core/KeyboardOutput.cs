namespace ClickForge.Core;

public static class KeyboardOutput
{
    public static bool IsValid(int key) => key is 8 or 9 or 13 or 27 or 32 or >= 33 and <= 40 or 45 or 46 or >= 48 and <= 57 or >= 65 and <= 90 or >= 96 and <= 111 or >= 112 and <= 135 or >= 186 and <= 192 or >= 219 and <= 222;
    public static void Validate(int key) { if (!IsValid(key)) throw new ArgumentException("Choose a single letter, number, function key, navigation key, Space or punctuation key. Modifier and system keys are not supported."); }
    public static uint Flags(int key, bool up) { Validate(key); return (up ? 2u : 0u) | (key is >= 33 and <= 40 or 45 or 46 or 111 ? 1u : 0u); }
    public static string Label(int key) => key switch
    {
        >= 65 and <= 90 or >= 48 and <= 57 => ((char)key).ToString(), >= 112 and <= 135 => "F"+(key-111),
        >= 96 and <= 105 => "Num "+(key-96), 8=>"Backspace",9=>"Tab",13=>"Enter",27=>"Escape",32=>"Space",33=>"Page Up",34=>"Page Down",35=>"End",36=>"Home",37=>"Left arrow",38=>"Up arrow",39=>"Right arrow",40=>"Down arrow",45=>"Insert",46=>"Delete",
        106=>"Num *",107=>"Num +",108=>"Num separator",109=>"Num -",110=>"Num decimal",111=>"Num /", _=>IsValid(key)?$"OEM key 0x{key:X2}":"Unassigned"
    };
    public static void ValidateOutput(ClickButton button, int key)
    {
        ClickButtons.Validate(button);
        if (key != 0 || button == ClickButton.CustomKey) Validate(key);
    }
}
