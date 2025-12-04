using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class DashboardGroupControl : UserControl
{
    private readonly TaskCompletionSource _initializedTaskCompletionSource = new();

    private readonly DashboardGroup _dashboardGroup;

    public Task InitializedTask => _initializedTaskCompletionSource.Task;

    public DashboardGroupControl(DashboardGroup dashboardGroup)
    {
        _dashboardGroup = dashboardGroup;

        Initialized += DashboardGroupControl_Initialized;
    }

    private async void DashboardGroupControl_Initialized(object? sender, System.EventArgs e)
    {
        var stackPanel = new StackPanel { Margin = new(0, 0, 16, 0) };

        // Create header with icon and title
        var headerPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Margin = new(0, 16, 0, 16)
        };

        // Add a decorative accent bar
        var accentBar = new Border
        {
            Width = 4,
            Height = 24,
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
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        AutomationProperties.SetName(textBlock, textBlock.Text);
        headerPanel.Children.Add(textBlock);

        stackPanel.Children.Add(headerPanel);

        // Load controls in parallel for faster initialization
        var controlsTasks = _dashboardGroup.Items.Select(i => i.GetControlAsync());
        var controls = await Task.WhenAll(controlsTasks).ConfigureAwait(true);

        var allControls = controls.SelectMany(c => c).ToList();
        
        // Batch add controls to reduce layout passes
        foreach (var control in allControls)
        {
            if (control is FrameworkElement fe)
            {
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right, 8);
            }
            stackPanel.Children.Add(control);
        }

        Content = stackPanel;

        _initializedTaskCompletionSource.TrySetResult();
    }
}
