using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsTrayInvert.UI.Helpers;

/// <summary>Shift + mouse wheel scrolls horizontally in every DataGrid/ListBox/TreeView of the app.</summary>
public static class HorizontalScrollHelper
{
    public static void Register()
    {
        foreach (Type type in new[] { typeof(DataGrid), typeof(ListBox), typeof(TreeView) })
            EventManager.RegisterClassHandler(type, UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnPreviewMouseWheel));
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift || sender is not DependencyObject root)
            return;

        ScrollViewer? viewer = FindScrollViewer(root);
        if (viewer is null || viewer.ScrollableWidth <= 0)
            return;

        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer)
            return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            ScrollViewer? found = FindScrollViewer(VisualTreeHelper.GetChild(node, i));
            if (found is not null)
                return found;
        }
        return null;
    }
}
