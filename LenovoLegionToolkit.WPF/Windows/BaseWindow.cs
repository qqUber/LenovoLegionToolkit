using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows;

/// <summary>
/// Base window using FluentWindow from WPF-UI 4.x for Mica backdrop and rounded corners.
/// </summary>
public class BaseWindow : FluentWindow
{
    protected BaseWindow()
    {
        SnapsToDevicePixels = true;
        
        // Extend content into title bar area - we provide our own title bar
        ExtendsContentIntoTitleBar = true;
        
        // Try Mica backdrop, fall back if not supported
        try
        {
            WindowBackdropType = WindowBackdropType.Mica;
        }
        catch
        {
            WindowBackdropType = WindowBackdropType.None;
        }
        
        // Windows 11 rounded corners
        WindowCornerPreference = WindowCornerPreference.Round;
        
        // Use custom window chrome for unified look
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
        Loaded += BaseWindow_Loaded;
    }

    private void BaseWindow_DpiChanged(object sender, DpiChangedEventArgs e) => VisualTreeHelper.SetRootDpi(this, e.NewDpi);
    
    private void BaseWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Ensure window is visible and activated after load
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
            
        Activate();
        Focus();
    }
}

