using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using LenovoLegionToolkit.WPF.Compat;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

public class CardControl : Wpf.Ui.Controls.CardControl
{
    public static new readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(SymbolRegular),
        typeof(CardControl),
        new PropertyMetadata(SymbolRegular.Empty, OnIconChanged));

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CardControl control && e.NewValue is SymbolRegular symbol)
        {
            try
            {
                ((Wpf.Ui.Controls.CardControl)control).Icon = symbol.ToIconElement();
            }
            catch (Exception ex)
            {
                if (LenovoLegionToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                    LenovoLegionToolkit.Lib.Utils.Log.Instance.Trace($"Failed to set CardControl Icon.", ex);
            }
        }
    }

    public new SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardControlAutomationPeer(this);

    private class CardControlAutomationPeer(CardControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CardControl);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        protected override AutomationPeer? GetLabeledByCore()
        {
            if (owner.Header is UIElement element)
                return CreatePeerForElement(element);

            return base.GetLabeledByCore();
        }

        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            if (result == string.Empty && owner.Header is DependencyObject d)
                result = AutomationProperties.GetName(d);

            if (result == string.Empty && owner.Header is string s)
                result = s;

            return result;
        }
    }
}
