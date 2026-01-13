using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;


namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class RefreshRateControl : AbstractComboBoxFeatureCardControl<RefreshRate>
{
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    public RefreshRateControl()
    {
        Icon = SymbolRegular.DesktopPulse24;
        Title = Resource.RefreshRateControl_Title;
        Subtitle = Resource.RefreshRateControl_Message;

        _listener.Changed += Listener_Changed;
    }

    protected override async Task OnRefreshAsync()
    {
        if (!await Feature.IsSupportedAsync())
            throw new NotSupportedException();

        var allItems = await Feature.GetAllStatesAsync();
        // Filter to only 60Hz and 144Hz
        var items = allItems.Where(r => r.Frequency == 60 || r.Frequency == 144).ToArray();
        var selectedItem = await Feature.GetStateAsync();
        
        // If current rate is not in filtered list, find closest match
        if (!items.Any(i => i.Frequency == selectedItem.Frequency))
        {
            selectedItem = items.OrderBy(i => Math.Abs(i.Frequency - selectedItem.Frequency)).FirstOrDefault();
        }

        InternalComboBox.SetItems(items, selectedItem, ComboBoxItemDisplayName);
        InternalComboBox.IsEnabled = items.Length != 0;
        InternalComboBox.Visibility = System.Windows.Visibility.Visible;
        
        Visibility = items.Length < 2 ? Visibility.Collapsed : Visibility.Visible;
    }

    protected override string ComboBoxItemDisplayName(RefreshRate value)
    {
        var str = base.ComboBoxItemDisplayName(value);
        return LocalizationHelper.ForceLeftToRight(str);
    }

    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.BeginInvoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}
