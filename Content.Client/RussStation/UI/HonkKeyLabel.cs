using Robust.Client.Input;
using Robust.Shared.Input;

namespace Content.Client.RussStation.UI;

/// <summary>
///     Fork-standard short keybind-label builder. Returns the same compact
///     short-form the upstream <c>BoundKeyHelper.ShortKeyName</c> produces for
///     unmodified keys (Del, Spc, Esc, Dwn…) and keeps that short form when
///     the binding has modifiers, prefixing them (S+Del, C+Spc).
/// </summary>
/// <remarks>
///     Upstream drops modifier bindings entirely because the original callers
///     couldn't fit the extra text in a fixed tile; the fork autofits so we can
///     and should show the real binding. Unknown keys fall back to the input
///     manager's canonical string.
/// </remarks>
public static class HonkKeyLabel
{
    public static string For(BoundKeyFunction function)
    {
        var input = IoCManager.Resolve<IInputManager>();
        if (!input.TryGetKeyBinding(function, out var binding))
            return " ";

        var baseLabel = ShortBase(binding.BaseKey) ?? input.GetKeyFunctionButtonString(function);
        if (binding.Mod1 == Keyboard.Key.Unknown
            && binding.Mod2 == Keyboard.Key.Unknown
            && binding.Mod3 == Keyboard.Key.Unknown)
        {
            return baseLabel;
        }

        var parts = new List<string>(4);
        if (binding.Mod1 != Keyboard.Key.Unknown) parts.Add(ModShort(binding.Mod1));
        if (binding.Mod2 != Keyboard.Key.Unknown) parts.Add(ModShort(binding.Mod2));
        if (binding.Mod3 != Keyboard.Key.Unknown) parts.Add(ModShort(binding.Mod3));
        parts.Add(baseLabel);
        return string.Join("+", parts);
    }

    private static string ModShort(Keyboard.Key key) => key switch
    {
        Keyboard.Key.Shift => "Shift",
        Keyboard.Key.Control => "Ctrl",
        Keyboard.Key.Alt => "Alt",
        _ => ShortBase(key) ?? key.ToString(),
    };

    // Mirrors upstream BoundKeyHelper's short-form table but usable for any binding, modified or not.
    private static string? ShortBase(Keyboard.Key key) => key switch
    {
        Keyboard.Key.Apostrophe => "'",
        Keyboard.Key.Comma => ",",
        Keyboard.Key.Delete => "Del",
        Keyboard.Key.Down => "Dwn",
        Keyboard.Key.Escape => "Esc",
        Keyboard.Key.Equal => "=",
        Keyboard.Key.Home => "Hom",
        Keyboard.Key.Insert => "Ins",
        Keyboard.Key.Left => "Lft",
        Keyboard.Key.Menu => "Men",
        Keyboard.Key.Minus => "-",
        Keyboard.Key.Num0 => "0",
        Keyboard.Key.Num1 => "1",
        Keyboard.Key.Num2 => "2",
        Keyboard.Key.Num3 => "3",
        Keyboard.Key.Num4 => "4",
        Keyboard.Key.Num5 => "5",
        Keyboard.Key.Num6 => "6",
        Keyboard.Key.Num7 => "7",
        Keyboard.Key.Num8 => "8",
        Keyboard.Key.Num9 => "9",
        Keyboard.Key.Pause => "||",
        Keyboard.Key.Period => ".",
        Keyboard.Key.Return => "Ret",
        Keyboard.Key.Right => "Rgt",
        Keyboard.Key.Slash => "/",
        Keyboard.Key.Space => "Spc",
        Keyboard.Key.Tab => "Tab",
        Keyboard.Key.Tilde => "~",
        Keyboard.Key.BackSlash => "\\",
        Keyboard.Key.BackSpace => "Bks",
        Keyboard.Key.LBracket => "[",
        Keyboard.Key.MouseButton4 => "M4",
        Keyboard.Key.MouseButton5 => "M5",
        Keyboard.Key.MouseButton6 => "M6",
        Keyboard.Key.MouseButton7 => "M7",
        Keyboard.Key.MouseButton8 => "M8",
        Keyboard.Key.MouseButton9 => "M9",
        Keyboard.Key.MouseLeft => "ML",
        Keyboard.Key.MouseMiddle => "MM",
        Keyboard.Key.MouseRight => "MR",
        Keyboard.Key.NumpadDecimal => "N.",
        Keyboard.Key.NumpadDivide => "N/",
        Keyboard.Key.NumpadEnter => "Ent",
        Keyboard.Key.NumpadMultiply => "*",
        Keyboard.Key.NumpadNum0 => "N0",
        Keyboard.Key.NumpadNum1 => "N1",
        Keyboard.Key.NumpadNum2 => "N2",
        Keyboard.Key.NumpadNum3 => "N3",
        Keyboard.Key.NumpadNum4 => "N4",
        Keyboard.Key.NumpadNum5 => "N5",
        Keyboard.Key.NumpadNum6 => "N6",
        Keyboard.Key.NumpadNum7 => "N7",
        Keyboard.Key.NumpadNum8 => "N8",
        Keyboard.Key.NumpadNum9 => "N9",
        Keyboard.Key.NumpadSubtract => "N-",
        Keyboard.Key.PageDown => "PgD",
        Keyboard.Key.PageUp => "PgU",
        Keyboard.Key.RBracket => "]",
        Keyboard.Key.SemiColon => ";",
        _ => null,
    };
}
