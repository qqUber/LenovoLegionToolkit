using LenovoLegionToolkit.WPF.Compat;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Compat;

public class SymbolButton : Button
{
    public new SymbolRegular Icon
    {
        get
        {
            if (base.Icon is SymbolRegular symbol)
            {
                return symbol;
            }

            return (base.Icon as IconElement).ToSymbolRegular();
        }
        set => base.Icon = value;
    }
}
