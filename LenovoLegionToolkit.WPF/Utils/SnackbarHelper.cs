using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.WPF.Compat;
using LenovoLegionToolkit.WPF.Windows;

using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

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
        SetTitleAndMessage(snackBar, title, message);
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
            SnackbarType.Success => TimeSpan.FromSeconds(2),
            _ => TimeSpan.FromSeconds(Math.Clamp(GetTimeoutSeconds(title, message), 5, 10))
        };
    }

    private static void SetTitleAndMessage(FrameworkElement snackBar, string title, string? message)
    {
        if (snackBar.FindName("_snackbarTitle") is TextBlock snackbarTitle)
            snackbarTitle.Text = title;

        if (snackBar.FindName("_snackbarMessage") is TextBlock snackbarMessage)
        {
            snackbarMessage.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            snackbarMessage.Text = message;
        }
    }

    private static int GetTimeoutSeconds(string title, string? message)
    {
        // Base 2 seconds + 1 second per 10 characters
        var charCount = title.Length + (message?.Length ?? 0);
        return 2 + charCount / 10;
    }
}
