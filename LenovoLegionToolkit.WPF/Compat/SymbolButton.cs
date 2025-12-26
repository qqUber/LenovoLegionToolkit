using LenovoLegionToolkit.WPF.Compat;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Compat;

public class SymbolButton : Button
{
    public new SymbolRegular Icon
    {
        get => base.Icon.ToSymbolRegular();
        set => base.Icon = value.ToIconElement();
    }
}
