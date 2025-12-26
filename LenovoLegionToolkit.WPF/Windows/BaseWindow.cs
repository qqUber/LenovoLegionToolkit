using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows;

/// <summary>
/// Base window using FluentWindow from WPF-UI 4.x for Mica backdrop and rounded corners.
/// We use custom title bar with WindowChrome for full control over the title bar area.
/// </summary>
public class BaseWindow : FluentWindow
{
    protected BaseWindow()
    {
        SnapsToDevicePixels = true;
        
        // Extend content into title bar area - we provide our own title bar
        ExtendsContentIntoTitleBar = true;
        
        // Enable Mica backdrop for modern Windows 11 look
        WindowBackdropType = WindowBackdropType.Mica;
        
        // Windows 11 rounded corners
        WindowCornerPreference = WindowCornerPreference.Round;
        
        // Configure WindowChrome for custom title bar
        var chrome = new WindowChrome
        {
            CaptionHeight = 32,
            GlassFrameThickness = new Thickness(-1),
            ResizeBorderThickness = new Thickness(6),
            UseAeroCaptionButtons = false,
            CornerRadius = new CornerRadius(8)
        };
        WindowChrome.SetWindowChrome(this, chrome);
        
        DpiChanged += BaseWindow_DpiChanged;
    }

    private void BaseWindow_DpiChanged(object sender, DpiChangedEventArgs e) => VisualTreeHelper.SetRootDpi(this, e.NewDpi);
}
