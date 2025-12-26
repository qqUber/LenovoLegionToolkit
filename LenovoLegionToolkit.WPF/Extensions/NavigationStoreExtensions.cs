using System.Linq;
using Wpf.Ui.Controls;
using CompatNavigationItem = LenovoLegionToolkit.WPF.Compat.NavigationItem;

namespace LenovoLegionToolkit.WPF.Extensions;

public static class NavigationStoreExtensions
{
    public static void NavigateToNext(this NavigationStore navigationStore)
    {
        var navigationItems = navigationStore.Items.OfType<CompatNavigationItem>().ToList();
        var current = navigationStore.Current as CompatNavigationItem ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        var index = (navigationItems.IndexOf(current) + 1) % navigationItems.Count;
        var next = navigationItems[index];

        if (next.PageTag is not null)
            navigationStore.Navigate(next.PageTag);
    }

    public static void NavigateToPrevious(this NavigationStore navigationStore)
    {
        var navigationItems = navigationStore.Items.OfType<CompatNavigationItem>().ToList();
        var current = navigationStore.Current as CompatNavigationItem ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        var index = navigationItems.IndexOf(current) - 1;
        if (index < 0)
            index = navigationItems.Count - 1;
        var next = navigationItems[index];

        if (next.PageTag is not null)
            navigationStore.Navigate(next.PageTag);
    }
}
