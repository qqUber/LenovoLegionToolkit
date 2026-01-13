using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using FluentWindowBase = Wpf.Ui.Controls.FluentWindow;
using NavigationViewBase = Wpf.Ui.Controls.NavigationView;
using NavigationViewItemBase = Wpf.Ui.Controls.NavigationViewItem;
using TitleBarBase = Wpf.Ui.Controls.TitleBar;
using ButtonBase = Wpf.Ui.Controls.Button;
using Wpf.Ui.Controls;

// Note: We do NOT register LenovoLegionToolkit.WPF.Compat to wpfui namespace
// because it causes ambiguity with actual Wpf.Ui.Controls types.
// Use xmlns:compat="clr-namespace:LenovoLegionToolkit.WPF.Compat" instead.

namespace LenovoLegionToolkit.WPF.Compat
{
    // Minimal compatibility shims to keep existing XAML working after upgrading to WPF UI 3.x.
    public class UiPage : Page
    {
        public UiPage()
        {
            // Set up for smooth page transition animation
            Opacity = 0;
            RenderTransform = new System.Windows.Media.TranslateTransform(0, 12);
            
            Loaded += UiPage_Loaded;
        }

        private void UiPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Animate page entrance
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            var slideUp = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeIn);
            RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
        }
    }

    public class UiWindow : FluentWindowBase
    {
        public static readonly DependencyProperty UseSnapLayoutProperty = DependencyProperty.Register(
            nameof(UseSnapLayout),
            typeof(bool),
            typeof(UiWindow),
            new PropertyMetadata(false)
        );

        public bool UseSnapLayout
        {
            get => (bool)GetValue(UseSnapLayoutProperty);
            set => SetValue(UseSnapLayoutProperty, value);
        }
    }

    public class TitleBar : TitleBarBase
    {
        public static readonly DependencyProperty UseSnapLayoutProperty = DependencyProperty.Register(
            nameof(UseSnapLayout),
            typeof(bool),
            typeof(TitleBar),
            new PropertyMetadata(false)
        );

        public bool UseSnapLayout
        {
            get => (bool)GetValue(UseSnapLayoutProperty);
            set => SetValue(UseSnapLayoutProperty, value);
        }
    }

    public class Button : ButtonBase
    {
        public static new readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(Button),
            new PropertyMetadata(null, OnIconChanged)
        );

        public new object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button control)
            {
                try
                {
                    if (e.NewValue is SymbolRegular symbol)
                    {
                        ((ButtonBase)control).Icon = symbol.ToIconElement();
                    }
                    else if (e.NewValue is IconElement icon)
                    {
                        ((ButtonBase)control).Icon = icon;
                    }
                }
                catch { }
            }
        }
    }

    public class Hyperlink : ButtonBase
    {
        public static readonly DependencyProperty NavigateUriProperty = DependencyProperty.Register(
            nameof(NavigateUri),
            typeof(Uri),
            typeof(Hyperlink),
            new PropertyMetadata(null)
        );

        public static new readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(Hyperlink),
            new PropertyMetadata(null)
        );

        public Uri? NavigateUri
        {
            get => (Uri?)GetValue(NavigateUriProperty);
            set => SetValue(NavigateUriProperty, value);
        }

        public new string? Icon
        {
            get => (string?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public Hyperlink()
        {
            // Set transparent appearance for link-like styling
            Appearance = ControlAppearance.Transparent;
            Cursor = System.Windows.Input.Cursors.Hand;
            Padding = new Thickness(8, 4, 8, 4);
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
            Background = System.Windows.Media.Brushes.Transparent;
            Foreground = System.Windows.Media.Brushes.White;
            BorderThickness = new Thickness(0);
        }

        protected override void OnClick()
        {
            base.OnClick();

            if (NavigateUri is null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(NavigateUri.ToString())
                {
                    UseShellExecute = true,
                });
            }
            catch
            {
                // Ignore navigation failures to keep compatibility behavior minimal.
            }
        }
    }

    public class Snackbar : ContentControl
    {
        public static readonly DependencyProperty CloseButtonEnabledProperty = DependencyProperty.Register(
            nameof(CloseButtonEnabled),
            typeof(bool),
            typeof(Snackbar),
            new PropertyMetadata(true)
        );


        public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
            nameof(Appearance),
            typeof(ControlAppearance),
            typeof(Snackbar),
            new PropertyMetadata(ControlAppearance.Secondary)
        );

        public ControlAppearance Appearance
        {
            get => (ControlAppearance)GetValue(AppearanceProperty);
            set => SetValue(AppearanceProperty, value);
        }

        public bool CloseButtonEnabled
        {
            get => (bool)GetValue(CloseButtonEnabledProperty);
            set => SetValue(CloseButtonEnabledProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(SymbolRegular),
            typeof(Snackbar),
            new PropertyMetadata(SymbolRegular.Empty)
        );

        public SymbolRegular Icon
        {
            get => (SymbolRegular)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(
            nameof(Timeout),
            typeof(TimeSpan),
            typeof(Snackbar),
            new PropertyMetadata(TimeSpan.FromMilliseconds(2000))
        );

        public TimeSpan Timeout
        {
            get => (TimeSpan)GetValue(TimeoutProperty);
            set => SetValue(TimeoutProperty, value);
        }

        public Task ShowAsync(string title, string message)
        {
            Content = message;
            Show();
            return Task.CompletedTask;
        }

        public Task ShowAsync()
        {
            Show();
            return Task.CompletedTask;
        }

        public void Show() 
        {
            Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
        }
    }

    public class NavigationItem : NavigationViewItemBase, Wpf.Ui.Controls.Interfaces.INavigationItem
    {
        public static new readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(SymbolRegular),
            typeof(NavigationItem),
            new PropertyMetadata(SymbolRegular.Empty, OnIconChanged)
        );

        public static readonly DependencyProperty IconElementProperty = DependencyProperty.Register(
            nameof(IconElement),
            typeof(IconElement),
            typeof(NavigationItem),
            new PropertyMetadata(null)
        );

        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
            nameof(Image),
            typeof(object),
            typeof(NavigationItem),
            new PropertyMetadata(null)
        );

        public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
            nameof(Symbol),
            typeof(SymbolRegular),
            typeof(NavigationItem),
            new PropertyMetadata(SymbolRegular.Empty, OnSymbolChanged)
        );

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationItem item && e.NewValue is SymbolRegular symbol)
            {
                var iconElement = symbol.ToIconElement();
                ((NavigationViewItemBase)item).Icon = iconElement;
                item.IconElement = iconElement;
            }
        }

        private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationItem item && e.NewValue is SymbolRegular symbol)
            {
                var iconElement = symbol.ToIconElement();
                ((NavigationViewItemBase)item).Icon = iconElement;
                item.IconElement = iconElement;
            }
        }

        public string? PageTag { get; set; }
        public Type? PageType { get; set; }
        public bool Cache { get; set; }
        public object? Accessory { get; set; }

        public IconElement? IconElement
        {
            get => (IconElement?)GetValue(IconElementProperty);
            set => SetValue(IconElementProperty, value);
        }

        public SymbolRegular Symbol
        {
            get => (SymbolRegular)GetValue(SymbolProperty);
            set => SetValue(SymbolProperty, value);
        }

        public new SymbolRegular Icon
        {
            get => (SymbolRegular)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public object? Image
        {
            get => GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }
    }

    public class NavigationSeparator : Separator
    {
    }

    public class NavigationHeader : ContentControl
    {
    }

    public class CardControl : LenovoLegionToolkit.WPF.Controls.Custom.CardControl
    {
    }

    public class CardExpander : LenovoLegionToolkit.WPF.Controls.Custom.CardExpander
    {
    }

    public class MenuItem : System.Windows.Controls.MenuItem
    {
        public static readonly DependencyProperty SymbolIconProperty = DependencyProperty.Register(
            nameof(SymbolIcon),
            typeof(SymbolRegular),
            typeof(MenuItem),
            new PropertyMetadata(SymbolRegular.Empty, OnSymbolIconChanged)
        );

        public SymbolRegular SymbolIcon
        {
            get => (SymbolRegular)GetValue(SymbolIconProperty);
            set => SetValue(SymbolIconProperty, value);
        }

        private static void OnSymbolIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Icons are optional in the compatibility layer; we do not render them.
        }
    }

    public class NavigationStore : Control, Wpf.Ui.Controls.Interfaces.INavigation
    {
        private const string ItemsPartName = "PART_Items";
        private const string FooterPartName = "PART_Footer";

        private ItemsControl? _itemsPresenter;
        private ItemsControl? _footerPresenter;

        public object? Current { get; private set; }

        public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
            nameof(Frame),
            typeof(Frame),
            typeof(NavigationStore),
            new PropertyMetadata(null)
        );

        public static readonly DependencyProperty SelectedPageIndexProperty = DependencyProperty.Register(
            nameof(SelectedPageIndex),
            typeof(int),
            typeof(NavigationStore),
            new PropertyMetadata(0, OnSelectedPageIndexChanged)
        );

        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items),
            typeof(Collection<NavigationItem>),
            typeof(NavigationStore),
            new PropertyMetadata(null)
        );

        public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
            nameof(Footer),
            typeof(Collection<NavigationItem>),
            typeof(NavigationStore),
            new PropertyMetadata(null)
        );

        public Frame? Frame
        {
            get => (Frame?)GetValue(FrameProperty);
            set => SetValue(FrameProperty, value);
        }

        public int SelectedPageIndex
        {
            get => (int)GetValue(SelectedPageIndexProperty);
            set => SetValue(SelectedPageIndexProperty, value);
        }

        public Collection<NavigationItem> Footer
        {
            get => (Collection<NavigationItem>)GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public Collection<NavigationItem> Items
        {
            get => (Collection<NavigationItem>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        IEnumerable<object> Wpf.Ui.Controls.Interfaces.INavigation.Items => Items.Cast<object>();

        public NavigationStore()
        {
            SetValue(ItemsProperty, new Collection<NavigationItem>());
            SetValue(FooterProperty, new Collection<NavigationItem>());
            Loaded += NavigationStore_Loaded;
        }

        private void NavigationStore_Loaded(object sender, RoutedEventArgs e)
        {
            // Navigate to the initial page once everything is loaded
            if (Items.Count > 0 && Frame != null)
            {
                NavigateByIndex(SelectedPageIndex);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _itemsPresenter = GetTemplateChild(ItemsPartName) as ItemsControl;
            _footerPresenter = GetTemplateChild(FooterPartName) as ItemsControl;

            AttachInputHandlers(_itemsPresenter);
            AttachInputHandlers(_footerPresenter);
            UpdateActiveVisual();
        }

        public void NavigateToNext()
        {
            var items = Items.OfType<NavigationItem>().ToList();
            if (items.Count == 0)
            {
                return;
            }

            var nextIndex = SelectedPageIndex + 1;
            if (nextIndex >= items.Count)
            {
                nextIndex = 0;
            }

            NavigateByIndex(nextIndex);
        }

        public void NavigateToPrevious()
        {
            var items = Items.OfType<NavigationItem>().ToList();
            if (items.Count == 0)
            {
                return;
            }

            var prevIndex = SelectedPageIndex - 1;
            if (prevIndex < 0)
            {
                prevIndex = items.Count - 1;
            }

            NavigateByIndex(prevIndex);
        }

        public bool Navigate(string pageTag)
        {
            var target = Items.OfType<NavigationItem>().FirstOrDefault(i => string.Equals(i.PageTag, pageTag, StringComparison.OrdinalIgnoreCase))
                ?? Footer.FirstOrDefault(i => string.Equals(i.PageTag, pageTag, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return false;
            }

            var items = Items.OfType<NavigationItem>().ToList();
            var index = items.IndexOf(target);
            if (index >= 0)
            {
                SelectedPageIndex = index;
            }

            Current = target;
            return Navigate(target);
        }

        private static void OnSelectedPageIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationStore store)
            {
                store.NavigateByIndex((int)e.NewValue);
            }
        }

        private bool Navigate(NavigationItem item)
        {
            if (Frame is null || item.PageType is null)
            {
                return false;
            }

            SetActiveItem(item);
            
            // Clear navigation journal to prevent caching issues
            while (Frame.CanGoBack)
            {
                Frame.RemoveBackEntry();
            }
            
            var pageInstance = Activator.CreateInstance(item.PageType);
            
            // Add fade transition animation
            if (pageInstance is FrameworkElement page)
            {
                page.Opacity = 0;
                Frame.Navigate(pageInstance);
                
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                page.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                Frame.Navigate(pageInstance);
            }
            
            Current = item;
            return true;
        }

        private void NavigateByIndex(int index)
        {
            var items = Items.OfType<NavigationItem>().ToList();
            if (index < 0 || index >= items.Count)
            {
                return;
            }

            var target = items[index];
            Navigate(target);
        }

        private void AttachInputHandlers(ItemsControl? presenter)
        {
            if (presenter is null)
            {
                return;
            }

            presenter.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnItemClicked), true);
            presenter.AddHandler(KeyUpEvent, new KeyEventHandler(OnItemKeyUp), true);
        }

        private void OnItemClicked(object sender, MouseButtonEventArgs e)
        {
            // Walk up visual tree to find NavigationItem - the source might be a child element
            var item = FindParent<NavigationItem>(e.OriginalSource as DependencyObject);
            if (item is not null)
            {
                NavigateFromItem(item);
                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child is not null)
            {
                if (child is T found)
                    return found;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void OnItemKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space))
            {
                return;
            }

            var item = FindParent<NavigationItem>(e.OriginalSource as DependencyObject);
            if (item is not null)
            {
                NavigateFromItem(item);
                e.Handled = true;
            }
        }

        private void NavigateFromItem(NavigationItem item)
        {
            var items = Items?.OfType<NavigationItem>().ToList() ?? [];
            var index = items.IndexOf(item);
            if (index >= 0)
            {
                SelectedPageIndex = index;
                Navigate(item);
                return;
            }

            if (Footer?.Contains(item) == true)
            {
                SetActiveItem(item);
                Navigate(item);
            }
        }

        private void SetActiveItem(NavigationItem? active)
        {
            foreach (var nav in Items?.Concat(Footer ?? []) ?? Enumerable.Empty<NavigationItem>())
            {
                nav.IsActive = ReferenceEquals(nav, active);
            }
        }

        private void UpdateActiveVisual()
        {
            var items = Items?.OfType<NavigationItem>().ToList() ?? [];
            if (SelectedPageIndex >= 0 && SelectedPageIndex < items.Count)
            {
                SetActiveItem(items[SelectedPageIndex]);
            }
        }
    }
}






namespace Wpf.Ui.Controls.Interfaces
{
    public interface INavigation
    {
        IEnumerable<object> Items { get; }
        bool Navigate(string pageTag);
    }

    public interface INavigationItem
    {
        string? PageTag { get; set; }
        SymbolRegular Icon { get; set; }
        object? Content { get; set; }
    }
}

namespace Wpf.Ui.Controls
{
    public class UiWindow : LenovoLegionToolkit.WPF.Compat.UiWindow
    {
    }

    public class NavigationStore : LenovoLegionToolkit.WPF.Compat.NavigationStore
    {
    }

    public class NavigationItem : LenovoLegionToolkit.WPF.Compat.NavigationItem
    {
    }

    public class NavigationSeparator : LenovoLegionToolkit.WPF.Compat.NavigationSeparator
    {
    }

    public class NavigationHeader : LenovoLegionToolkit.WPF.Compat.NavigationHeader
    {
    }

    public class MenuItem : LenovoLegionToolkit.WPF.Compat.MenuItem
    {
    }


}
