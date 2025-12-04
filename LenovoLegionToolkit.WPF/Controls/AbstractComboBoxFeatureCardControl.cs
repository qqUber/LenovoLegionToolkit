using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls;

public abstract class AbstractComboBoxFeatureCardControl<T> : AbstractRefreshingControl where T : struct
{
    protected readonly IFeature<T> Feature = IoCContainer.Resolve<IFeature<T>>();

    private readonly CardControl _cardControl = new();

    private readonly CardHeaderControl _cardHeaderControl = new();

    protected readonly ComboBox InternalComboBox = new();

    protected SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    protected string Title
    {
        get => _cardHeaderControl.Title;
        set
        {
            _cardHeaderControl.Title = value;
            AutomationProperties.SetName(InternalComboBox, value);
        }
    }

    protected string Subtitle
    {
        get => _cardHeaderControl.Subtitle;
        set => _cardHeaderControl.Subtitle = value;
    }

    protected string Warning
    {
        get => _cardHeaderControl.Warning;
        set => _cardHeaderControl.Warning = value;
    }

    protected AbstractComboBoxFeatureCardControl() => InitializeComponent();

    private void InitializeComponent()
    {
        InternalComboBox.SelectionChanged += ComboBox_SelectionChanged;
        InternalComboBox.MinWidth = 165;
        InternalComboBox.Visibility = Visibility.Hidden;
        InternalComboBox.Margin = new(8, 0, 0, 0);

        _cardHeaderControl.Accessory = GetAccessory(InternalComboBox);
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await OnStateChangeAsync(InternalComboBox, Feature, e.GetNewValue<T>(), e.GetOldValue<T>());
    }

    protected bool TryGetSelectedItem(out T value) => InternalComboBox.TryGetSelectedItem(out value);

    protected int ItemsCount => InternalComboBox.Items.Count;

    protected virtual FrameworkElement GetAccessory(ComboBox comboBox) => comboBox;

    protected virtual string ComboBoxItemDisplayName(T value) => value switch
    {
        IDisplayName dn => dn.DisplayName,
        Enum e => e.GetDisplayName(),
        _ => value.ToString() ?? throw new InvalidOperationException("Unsupported type")
    };

    protected override async Task OnRefreshAsync()
    {
        if (!await Feature.IsSupportedAsync())
            throw new NotSupportedException();

        var items = await Feature.GetAllStatesAsync();
        var selectedItem = await Feature.GetStateAsync();

        InternalComboBox.SetItems(items, selectedItem, ComboBoxItemDisplayName);
        InternalComboBox.IsEnabled = items.Length != 0;
        InternalComboBox.Visibility = Visibility.Visible;
    }

    protected override void OnFinishedLoading()
    {
        MessagingCenter.Subscribe<FeatureStateMessage<T>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshAsync();
        }));
    }

    protected virtual async Task OnStateChangeAsync(ComboBox comboBox, IFeature<T> feature, T? newValue, T? oldValue)
    {
        var exceptionOccurred = false;

        try
        {
            if (IsRefreshing)
                return;

            InternalComboBox.IsEnabled = false;

            if (oldValue is null)
                return;

            if (!comboBox.TryGetSelectedItem(out T selectedState))
                return;

            var currentState = await feature.GetStateAsync();

            if (selectedState.Equals(currentState))
                return;

            await feature.SetStateAsync(selectedState);
        }
        catch (Exception ex)
        {
            exceptionOccurred = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to change state. [feature={GetType().Name}]", ex);

            OnStateChangeException(ex);
        }
        finally
        {
            var delay = AdditionalStateChangeDelay(oldValue, newValue);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            InternalComboBox.IsEnabled = true;
        }

        if (exceptionOccurred)
            await RefreshAsync();
    }

    protected virtual void OnStateChangeException(Exception exception) { }

    protected virtual TimeSpan AdditionalStateChangeDelay(T? oldValue, T? newValue) => TimeSpan.Zero;
}
