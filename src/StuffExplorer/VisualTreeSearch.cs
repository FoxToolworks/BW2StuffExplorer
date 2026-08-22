using System.Windows;
using System.Windows.Media;

namespace StuffExplorer;

internal static class VisualTreeSearch
{
    internal static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                return match;

            if (FindDescendant<T>(child) is { } descendant)
                return descendant;
        }

        return null;
    }
}
