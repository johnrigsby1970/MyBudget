using System.Windows;
using System.Windows.Media;

namespace StayOnTarget.Helpers;

public static class VisualTreeUtils
{
    /// <summary>
    /// Recursively searches the visual tree downwards from a parent DependencyObject 
    /// to find all child elements of a specific type T.
    /// </summary>
    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
}