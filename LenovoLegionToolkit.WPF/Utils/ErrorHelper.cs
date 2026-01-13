using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// Helper class for displaying user-friendly error messages with optional retry functionality.
/// Maps technical exceptions to human-readable messages.
/// Uses modern Windows 11 style dialogs.
/// </summary>
public static class ErrorHelper
{
    /// <summary>
    /// Common error categories with user-friendly messages and suggested actions.
    /// </summary>
    private static readonly Dictionary<Type, (string Title, string Message, string? RetryHint)> ErrorMappings = new()
    {
        // Network errors
        { typeof(HttpRequestException), ("Connection Error", "Unable to connect to the server. Please check your internet connection and try again.", "Retry") },
        { typeof(TaskCanceledException), ("Request Timed Out", "The operation took too long to complete. This might be due to slow internet or server issues.", "Try Again") },
        
        // File errors
        { typeof(System.IO.FileNotFoundException), ("File Not Found", "The requested file could not be found. It may have been moved or deleted.", null) },
        { typeof(System.IO.DirectoryNotFoundException), ("Folder Not Found", "The specified folder does not exist.", null) },
        { typeof(System.IO.IOException), ("File Access Error", "Unable to access the file. It may be in use by another program.", "Close other programs and retry") },
        { typeof(UnauthorizedAccessException), ("Permission Denied", "You don't have permission to perform this action. Try running the app as administrator.", null) },
        { typeof(System.IO.InvalidDataException), ("Invalid Data", "The data is corrupted or in an unexpected format.", null) },
        
        // Operation errors
        { typeof(InvalidOperationException), ("Operation Failed", "The requested operation could not be completed. Please try again.", "Retry") },
        { typeof(NotSupportedException), ("Not Supported", "This feature is not supported on your device.", null) },
        { typeof(ArgumentException), ("Invalid Input", "The provided input is not valid. Please check and try again.", null) },
        { typeof(TimeoutException), ("Timed Out", "The operation took too long. Please try again.", "Retry") },
        
        // System/Win32 errors
        { typeof(System.Runtime.InteropServices.COMException), ("System Error", "A Windows system component reported an error. This might be due to incorrect settings or lack of permissions.", "Check permissions") },
        { typeof(System.ComponentModel.Win32Exception), ("Windows Error", "A low-level Windows error occurred while performing this action.", "Restart the app as Admin") },
    };

    /// <summary>
    /// Shows a user-friendly error message based on the exception type.
    /// Uses modern Windows 11 style dialogs.
    /// </summary>
    public static async Task ShowErrorAsync(Exception ex, string? context = null, Func<Task>? retryAction = null)
    {
        // Log the technical error
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Error occurred: {context ?? "Unknown context"}", ex);

        // Get user-friendly message
        var (title, message, retryHint) = GetErrorInfo(ex);

        // Add context if provided
        if (!string.IsNullOrEmpty(context))
        {
            message = $"{context}\n\n{message}";
        }

        // If retry action is available, offer it
        if (retryAction != null && retryHint != null)
        {
            var shouldRetry = await ModernDialog.ShowErrorAsync(title, message + "\n\nWould you like to try again?", showRetry: true);
            
            if (shouldRetry)
            {
                try
                {
                    await retryAction();
                    return;
                }
                catch (Exception retryEx)
                {
                    // Show error again if retry failed
                    await ShowErrorAsync(retryEx, context);
                    return;
                }
            }
        }
        else
        {
            // Just show snackbar notification for simple errors
            await SnackbarHelper.ShowAsync(title, message, SnackbarType.Error);
        }
    }

    /// <summary>
    /// Shows a user-friendly error message synchronously.
    /// Uses snackbar for non-blocking display.
    /// </summary>
    public static void ShowError(Exception ex, string? context = null)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Error occurred: {context ?? "Unknown context"}", ex);

        var (title, message, _) = GetErrorInfo(ex);

        if (!string.IsNullOrEmpty(context))
        {
            message = $"{context}\n\n{message}";
        }

        SnackbarHelper.Show(title, message, SnackbarType.Error);
    }

    /// <summary>
    /// Shows a warning message using modern dialog.
    /// </summary>
    public static async Task<bool> ShowWarningAsync(string title, string message)
    {
        return await ModernDialog.ShowWarningAsync(title, message);
    }

    /// <summary>
    /// Shows a success message with optional details.
    /// </summary>
    public static async Task ShowSuccessAsync(string title, string? message = null)
    {
        await SnackbarHelper.ShowAsync(title, message, SnackbarType.Success);
    }

    /// <summary>
    /// Shows an informational message.
    /// </summary>
    public static async Task ShowInfoAsync(string title, string? message = null)
    {
        await SnackbarHelper.ShowAsync(title, message, SnackbarType.Info);
    }

    /// <summary>
    /// Shows a confirmation dialog using modern Windows 11 style.
    /// </summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        return await ModernDialog.ShowConfirmAsync(title, message, confirmText, cancelText);
    }

    /// <summary>
    /// Shows a destructive action confirmation using modern Windows 11 style.
    /// </summary>
    public static async Task<bool> ConfirmDestructiveAsync(string title, string message, string destructiveAction = "Delete")
    {
        return await ModernDialog.ShowDestructiveConfirmAsync(title, message, destructiveAction);
    }

    /// <summary>
    /// Gets user-friendly error information based on exception type.
    /// </summary>
    private static (string Title, string Message, string? RetryHint) GetErrorInfo(Exception ex)
    {
        var exceptionType = ex.GetType();

        // Check for exact match
        if (ErrorMappings.TryGetValue(exceptionType, out var mapping))
        {
            return mapping;
        }

        // Check for base type match
        foreach (var kvp in ErrorMappings)
        {
            if (kvp.Key.IsAssignableFrom(exceptionType))
            {
                return kvp.Value;
            }
        }

        // Default fallback message
        return GetDefaultErrorInfo(ex);
    }

    /// <summary>
    /// Gets a default error message when no specific mapping exists.
    /// </summary>
    private static (string Title, string Message, string? RetryHint) GetDefaultErrorInfo(Exception ex)
    {
        // Try to extract a reasonable message from the exception
        var message = ex.Message;

        // Make common technical messages more friendly
        if (message.Contains("access", StringComparison.OrdinalIgnoreCase) && 
            message.Contains("denied", StringComparison.OrdinalIgnoreCase))
        {
            return ("Access Denied", "You don't have permission to perform this action. Try running as administrator.", null);
        }

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ("Operation Timed Out", "The operation took too long to complete. Please try again.", "Retry");
        }

        if (message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("internet", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return ("Connection Error", "Unable to establish a network connection. Please check your internet.", "Retry");
        }

        if (message.Contains("driver", StringComparison.OrdinalIgnoreCase))
        {
            return ("Driver Error", "A problem occurred with a system driver. Try restarting your computer.", null);
        }

        if (message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not available", StringComparison.OrdinalIgnoreCase))
        {
            return ("Not Available", "This feature is not available on your device.", null);
        }

        // Generic fallback
        return ("Something Went Wrong", 
            "An unexpected error occurred. If this keeps happening, please check the logs or report the issue.",
            null);
    }

    /// <summary>
    /// Wraps an async operation with automatic error handling.
    /// </summary>
    public static async Task<bool> TryExecuteAsync(
        Func<Task> action, 
        string context,
        bool showSuccessMessage = false,
        string? successMessage = null)
    {
        try
        {
            await action();
            
            if (showSuccessMessage)
            {
                await ShowSuccessAsync("Success", successMessage ?? $"{context} completed successfully.");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex, $"Failed to {context.ToLowerInvariant()}");
            return false;
        }
    }
}
