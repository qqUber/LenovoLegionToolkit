using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using LenovoLegionToolkit.WPF.Windows;
using Wpf.Ui.Controls;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfStackPanel = System.Windows.Controls.StackPanel;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// Modern Windows 11 style message dialog helper.
/// Uses WPF-UI's ContentDialog for modern, styled dialogs.
/// </summary>
public static class ModernDialog
{
    /// <summary>
    /// Shows an error dialog with Windows 11 styling.
    /// </summary>
    public static async Task<bool> ShowErrorAsync(string title, string message, bool showRetry = false)
    {
        return await ShowDialogAsync(
            title, 
            message, 
            SymbolRegular.ErrorCircle24,
            Color.FromRgb(232, 17, 35),
            showRetry ? "Retry" : "OK",
            showRetry ? "Cancel" : null,
            ControlAppearance.Danger);
    }

    /// <summary>
    /// Shows a warning dialog with Windows 11 styling.
    /// </summary>
    public static async Task<bool> ShowWarningAsync(string title, string message, bool showCancelButton = true)
    {
        return await ShowDialogAsync(
            title,
            message,
            SymbolRegular.Warning24,
            Color.FromRgb(255, 185, 0),
            "Continue",
            showCancelButton ? "Cancel" : null,
            ControlAppearance.Caution);
    }

    /// <summary>
    /// Shows a confirmation dialog with Windows 11 styling.
    /// </summary>
    public static async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        return await ShowDialogAsync(
            title,
            message,
            SymbolRegular.QuestionCircle24,
            Color.FromRgb(0, 120, 212),
            confirmText,
            cancelText,
            ControlAppearance.Primary);
    }

    /// <summary>
    /// Shows a destructive action confirmation with Windows 11 styling.
    /// </summary>
    public static async Task<bool> ShowDestructiveConfirmAsync(string title, string message, string destructiveAction = "Delete")
    {
        return await ShowDialogAsync(
            title,
            message,
            SymbolRegular.Delete24,
            Color.FromRgb(232, 17, 35),
            destructiveAction,
            "Cancel",
            ControlAppearance.Danger);
    }

    /// <summary>
    /// Shows an information dialog with Windows 11 styling.
    /// </summary>
    public static async Task ShowInfoAsync(string title, string message)
    {
        await ShowDialogAsync(
            title,
            message,
            SymbolRegular.Info24,
            Color.FromRgb(0, 120, 212),
            "OK",
            null,
            ControlAppearance.Info);
    }

    private static async Task<bool> ShowDialogAsync(
        string title,
        string message,
        SymbolRegular icon,
        Color iconColor,
        string primaryButtonText,
        string? secondaryButtonText,
        ControlAppearance primaryAppearance)
    {
        try
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                return ShowFallbackMessageBox(title, message, secondaryButtonText != null);
            }

            // Find the ContentPresenter
            var dialogHost = mainWindow.FindName("RootContentDialogPresenter") as System.Windows.Controls.ContentPresenter;
            if (dialogHost == null)
            {
                return ShowFallbackMessageBox(title, message, secondaryButtonText != null);
            }

            // Create dialog content with modern styling
            var content = CreateDialogContent(title, message, icon, iconColor);

            // Create the ContentDialog
            var dialog = new ContentDialog
            {
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = secondaryButtonText ?? "",
                PrimaryButtonAppearance = primaryAppearance,
                IsPrimaryButtonEnabled = true,
            };
            
            // Set dialog host (suppress obsolete warning - still works in WPF-UI 4.x)
#pragma warning disable CS0618
            dialog.DialogHost = dialogHost;
#pragma warning restore CS0618

            // Show the dialog
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        catch (Exception)
        {
            // Fallback to standard MessageBox
            return ShowFallbackMessageBox(title, message, secondaryButtonText != null);
        }
    }

    private static UIElement CreateDialogContent(string title, string message, SymbolRegular icon, Color iconColor)
    {
        var container = new WpfStackPanel
        {
            Margin = new Thickness(0)
        };

        // Header with icon and title
        var header = new WpfStackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var iconElement = new SymbolIcon
        {
            Symbol = icon,
            FontSize = 32,
            Foreground = new SolidColorBrush(iconColor),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleBlock = new WpfTextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        header.Children.Add(iconElement);
        header.Children.Add(titleBlock);

        // Message
        var messageBlock = new WpfTextBlock
        {
            Text = message,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            Opacity = 0.8
        };

        container.Children.Add(header);
        container.Children.Add(messageBlock);

        return container;
    }

    private static bool ShowFallbackMessageBox(string title, string message, bool hasSecondButton)
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            hasSecondButton ? System.Windows.MessageBoxButton.YesNo : System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
        
        return result == System.Windows.MessageBoxResult.Yes || result == System.Windows.MessageBoxResult.OK;
    }
}
