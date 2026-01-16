using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LenovoLegionToolkit.WPF.Compat;
using LenovoLegionToolkit.WPF.Windows;

using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;

namespace LenovoLegionToolkit.WPF.Utils;

public static class SnackbarHelper
{
    private static Compat.Snackbar? GetSnackbar() =>
        (Application.Current.MainWindow as MainWindow)?.Snackbar;

    public static async Task ShowAsync(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        var snackBar = GetSnackbar();
        if (snackBar is null)
            return;

        ConfigureAndShow(snackBar, title, message, type);
        await snackBar.ShowAsync();
    }

    public static void Show(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        var snackBar = GetSnackbar();
        if (snackBar is null)
            return;

        ConfigureAndShow(snackBar, title, message, type);
        snackBar.Show();
    }

    private static void ConfigureAndShow(Compat.Snackbar snackBar, string title, string? message, SnackbarType type)
    {
        SetupSnackbarAppearance(snackBar, title, message, type);
        SetTitleMessageAndIcon(snackBar, title, message, type);
    }

    private static void SetupSnackbarAppearance(Compat.Snackbar snackBar, string title, string? message, SnackbarType type)
    {
        snackBar.Appearance = type switch
        {
            SnackbarType.Warning => ControlAppearance.Caution,
            SnackbarType.Error => ControlAppearance.Danger,
            _ => ControlAppearance.Secondary
        };
        snackBar.Icon = type switch
        {
            SnackbarType.Warning => SymbolRegular.Warning24,
            SnackbarType.Error => SymbolRegular.ErrorCircle24,
            SnackbarType.Info => SymbolRegular.Info24,
            _ => SymbolRegular.Checkmark24
        };
        snackBar.Timeout = type switch
        {
            SnackbarType.Success => TimeSpan.FromSeconds(3),
            _ => TimeSpan.FromSeconds(Math.Clamp(GetTimeoutSeconds(title, message), 5, 10))
        };
    }

    private static void SetTitleMessageAndIcon(FrameworkElement snackBar, string title, string? message, SnackbarType type)
    {
        // Set title
        if (snackBar.FindName("_snackbarTitle") is TextBlock snackbarTitle)
            snackbarTitle.Text = title;

        // Set message
        if (snackBar.FindName("_snackbarMessage") is TextBlock snackbarMessage)
        {
            snackbarMessage.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            snackbarMessage.Text = message;
        }

        // Set icon symbol
        if (snackBar.FindName("_snackbarIcon") is SymbolIcon symbolIcon)
        {
            symbolIcon.Symbol = type switch
            {
                SnackbarType.Warning => SymbolRegular.Warning24,
                SnackbarType.Error => SymbolRegular.ErrorCircle24,
                SnackbarType.Info => SymbolRegular.Info24,
                _ => SymbolRegular.CheckmarkCircle24
            };
        }

        // Set icon container background based on type
        if (snackBar.FindName("_snackbarIcon") is FrameworkElement iconElement 
            && iconElement.Parent is Border iconBorder)
        {
            iconBorder.Background = type switch
            {
                SnackbarType.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)), // Orange
                SnackbarType.Error => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),   // Red
                SnackbarType.Info => new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),    // Blue
                _ => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))                     // Green
            };
        }
    }

    private static int GetTimeoutSeconds(string title, string? message)
    {
        // Base 2 seconds + 1 second per 10 characters
        var charCount = title.Length + (message?.Length ?? 0);
        return 2 + charCount / 10;
    }
}
