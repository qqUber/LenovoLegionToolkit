using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// Standalone modern error window that can be shown during startup and crash scenarios.
/// Doesn't require MainWindow to be loaded.
/// </summary>
public partial class ErrorWindow : FluentWindow
{
    public enum ErrorType
    {
        Error,
        Warning,
        Info
    }

    public bool Result { get; private set; }

    public ErrorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a modern error dialog. Can be called before MainWindow is loaded.
    /// </summary>
    public static void ShowError(string title, string message, string? details = null)
    {
        var window = new ErrorWindow();
        window.ConfigureDialog(ErrorType.Error, title, message, details, "OK", null);
        window.ShowDialog();
    }

    /// <summary>
    /// Shows a modern error dialog with options. Returns true if primary button clicked.
    /// </summary>
    public static bool ShowErrorWithChoice(string title, string message, string primaryButton = "OK", string? secondaryButton = null)
    {
        var window = new ErrorWindow();
        window.ConfigureDialog(ErrorType.Error, title, message, null, primaryButton, secondaryButton);
        window.ShowDialog();
        return window.Result;
    }

    /// <summary>
    /// Shows a modern warning dialog. Returns true if primary button clicked.
    /// </summary>
    public static bool ShowWarning(string title, string message, string primaryButton = "Continue", string? secondaryButton = "Cancel")
    {
        var window = new ErrorWindow();
        window.ConfigureDialog(ErrorType.Warning, title, message, null, primaryButton, secondaryButton);
        window.ShowDialog();
        return window.Result;
    }

    /// <summary>
    /// Shows a modern info dialog.
    /// </summary>
    public static void ShowInfo(string title, string message)
    {
        var window = new ErrorWindow();
        window.ConfigureDialog(ErrorType.Info, title, message, null, "OK", null);
        window.ShowDialog();
    }

    private void ConfigureDialog(ErrorType type, string title, string message, string? details, string primaryButton, string? secondaryButton)
    {
        Title = type switch
        {
            ErrorType.Error => "Error",
            ErrorType.Warning => "Warning",
            ErrorType.Info => "Information",
            _ => "Error"
        };

        _titleText.Text = title;
        _messageText.Text = string.IsNullOrEmpty(details) ? message : $"{message}\n\n{details}";
        _primaryButton.Content = primaryButton;

        // Configure icon and color based on type
        switch (type)
        {
            case ErrorType.Error:
                _icon.Symbol = SymbolRegular.ErrorCircle24;
                _icon.Foreground = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                _primaryButton.Appearance = ControlAppearance.Danger;
                break;
            case ErrorType.Warning:
                _icon.Symbol = SymbolRegular.Warning24;
                _icon.Foreground = new SolidColorBrush(Color.FromRgb(255, 185, 0));
                _primaryButton.Appearance = ControlAppearance.Caution;
                break;
            case ErrorType.Info:
                _icon.Symbol = SymbolRegular.Info24;
                _icon.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                _primaryButton.Appearance = ControlAppearance.Primary;
                break;
        }

        // Secondary button
        if (!string.IsNullOrEmpty(secondaryButton))
        {
            _secondaryButton.Content = secondaryButton;
            _secondaryButton.Visibility = Visibility.Visible;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_messageText.Text);
        }
        catch { }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
