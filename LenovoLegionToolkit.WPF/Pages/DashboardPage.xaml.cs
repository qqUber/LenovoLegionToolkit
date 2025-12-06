using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Dashboard;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class DashboardPage
{
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    private readonly List<DashboardGroupControl> _dashboardGroupControls = [];
    
    // Cache for layout state
    private bool _isExpanded;
    private double _lastWidth;

    public DashboardPage() => InitializeComponent();

    private async void DashboardPage_Initialized(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        // Reduce minimum loading time for snappier feel
        var loadingTask = Task.Delay(TimeSpan.FromMilliseconds(300));

        ScrollHost?.ScrollToTop();

        _sensors.Visibility = _dashboardSettings.Store.ShowSensors ? Visibility.Visible : Visibility.Collapsed;

        _dashboardGroupControls.Clear();
        _content.ColumnDefinitions.Clear();
        _content.RowDefinitions.Clear();
        _content.Children.Clear();

        var groups = _dashboardSettings.Store.Groups ?? DashboardGroup.DefaultGroups;

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Groups:");
            foreach (var group in groups)
                Log.Instance.Trace($" - {group}");
        }

        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });

        // Create controls on UI thread (WPF controls must be created on STA thread)
        var controls = groups.Select(group => new DashboardGroupControl(group)).ToArray();

        var initializedTasks = new List<Task>(controls.Length + 1) { loadingTask };

        // Add enough row definitions for single-column layout (we'll reuse them for 2-column)
        for (var i = 0; i < controls.Length; i++)
        {
            _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });
        }

        for (var index = 0; index < controls.Length; index++)
        {
            var control = controls[index];
            // Set initial row/column (will be recalculated in LayoutGroups)
            Grid.SetRow(control, index);
            Grid.SetColumn(control, 0);
            _content.Children.Add(control);
            _dashboardGroupControls.Add(control);
            initializedTasks.Add(control.InitializedTask);
        }

        _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });

        var editDashboardHyperlink = new Hyperlink
        {
            Icon = SymbolRegular.Edit24,
            Content = Resource.DashboardPage_Customize,
            Margin = new(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        editDashboardHyperlink.Click += (_, _) =>
        {
            var window = new EditDashboardWindow { Owner = Window.GetWindow(this) };
            window.Apply += async (_, _) => await RefreshAsync();
            window.ShowDialog();
        };

        Grid.SetRow(editDashboardHyperlink, groups.Length);
        Grid.SetColumn(editDashboardHyperlink, 0);
        Grid.SetColumnSpan(editDashboardHyperlink, 2);

        _content.Children.Add(editDashboardHyperlink);

        // Force layout calculation
        _lastWidth = 0;
        LayoutGroups(ActualWidth);

        // Wait for all controls to initialize
        await Task.WhenAll(initializedTasks);
        _loader.IsLoading = false;
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        LayoutGroups(e.NewSize.Width);
    }

    private void LayoutGroups(double width)
    {
        // Skip if width hasn't changed meaningfully (hysteresis)
        if (Math.Abs(_lastWidth - width) < 50)
            return;

        _lastWidth = width;
        var shouldExpand = width > 1000;

        // Skip if state hasn't changed
        if (shouldExpand == _isExpanded && _dashboardGroupControls.Count > 0)
            return;

        _isExpanded = shouldExpand;

        if (shouldExpand)
            Expand();
        else
            Collapse();
    }

    private void Expand()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(1, GridUnitType.Star);

        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index / 2);
            Grid.SetColumn(control, index % 2);
        }

        // Update hyperlink row position for 2-column layout
        var hyperlink = _content.Children.OfType<Hyperlink>().FirstOrDefault();
        if (hyperlink is not null)
        {
            var rowCount = (_dashboardGroupControls.Count + 1) / 2;
            Grid.SetRow(hyperlink, rowCount);
        }
    }

    private void Collapse()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(0, GridUnitType.Pixel);

        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index);
            Grid.SetColumn(control, 0);
        }

        // Update hyperlink row position for single-column layout
        var hyperlink = _content.Children.OfType<Hyperlink>().FirstOrDefault();
        if (hyperlink is not null)
        {
            Grid.SetRow(hyperlink, _dashboardGroupControls.Count);
        }
    }
}
