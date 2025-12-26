using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Compat;

public static class IconExtensions
{
    public static IconElement ToIconElement(this SymbolRegular symbol)
    {
        return new SymbolIcon { Symbol = symbol };
    }

    public static SymbolRegular ToSymbolRegular(this IconElement? icon)
    {
        return icon is SymbolIcon symbolIcon ? symbolIcon.Symbol : SymbolRegular.Empty;
    }
}
