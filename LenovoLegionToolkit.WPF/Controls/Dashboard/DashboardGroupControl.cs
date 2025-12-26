using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class DashboardGroupControl : UserControl
{
    private static readonly Thickness HeaderMargin = new(0, 16, 0, 16);
    private const double HeaderFontSize = 22;
    private const double AccentWidth = 4;
    private const double AccentHeight = 24;
    private const double ControlBottomMargin = 8;

    private readonly TaskCompletionSource _initializedTaskCompletionSource = new();

    private readonly DashboardGroup _dashboardGroup;

    public Task InitializedTask => _initializedTaskCompletionSource.Task;

    public DashboardGroupControl(DashboardGroup dashboardGroup)
    {
        _dashboardGroup = dashboardGroup;

        Initialized += DashboardGroupControl_Initialized;
    }

    private void DashboardGroupControl_Initialized(object? sender, EventArgs e)
    {
        Initialized -= DashboardGroupControl_Initialized;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var mainPanel = new StackPanel { Margin = new(0, 0, 16, 0) };

            mainPanel.Children.Add(CreateHeader());

            var controls = await LoadControlsAsync().ConfigureAwait(true);

            var itemsControl = CreateItemsControl(controls);
            mainPanel.Children.Add(itemsControl);

            Content = mainPanel;

            _initializedTaskCompletionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            _initializedTaskCompletionSource.TrySetException(ex);
        }
    }

    private UIElement CreateHeader()
    {
        // Create header with accent bar and title
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = HeaderMargin
        };

        var accentBar = new Border
        {
            Width = AccentWidth,
            Height = AccentHeight,
            CornerRadius = new CornerRadius(2),
            Margin = new(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        accentBar.SetResourceReference(Border.BackgroundProperty, "SystemAccentColorPrimaryBrush");
        headerPanel.Children.Add(accentBar);

        var textBlock = new TextBlock
        {
            Text = _dashboardGroup.GetName(),
            Focusable = true,
            FontSize = HeaderFontSize,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        AutomationProperties.SetName(textBlock, textBlock.Text);
        headerPanel.Children.Add(textBlock);

        return headerPanel;
    }

    private async Task<IReadOnlyList<UIElement>> LoadControlsAsync()
    {
        // Load controls in parallel for faster initialization
        var controlsTasks = _dashboardGroup.Items.Select(i => i.GetControlAsync());
        var controls = await Task.WhenAll(controlsTasks).ConfigureAwait(true);

        var allControls = new List<UIElement>();

        foreach (var control in controls.SelectMany(c => c))
        {
            if (control is FrameworkElement fe)
            {
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right, ControlBottomMargin);
            }

            allControls.Add(control);
        }

        return allControls;
    }

    private ItemsControl CreateItemsControl(IReadOnlyList<UIElement> controls)
    {
        var itemsControl = new ItemsControl
        {
            ItemsSource = controls
        };

        itemsControl.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        itemsControl.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        itemsControl.SetValue(ScrollViewer.CanContentScrollProperty, true);

        var itemsPanelFactory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
        itemsPanelFactory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Vertical);
        itemsControl.ItemsPanel = new ItemsPanelTemplate(itemsPanelFactory);

        return itemsControl;
    }
}
