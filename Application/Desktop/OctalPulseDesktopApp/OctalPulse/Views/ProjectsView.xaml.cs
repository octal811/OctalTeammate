using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace OctalPulse.Views;

public partial class ProjectsView : UserControl
{
    private const double MinCardWidth = 430;
    private const double CardGap = 18;

    public ProjectsView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateColumns();
        SizeChanged += (_, _) => UpdateColumns();
    }

    private void UpdateColumns()
    {
        var width = ProjectsControl.ActualWidth;
        if (width <= 0) return;

        var columns = Math.Max(1, (int)((width + CardGap) / (MinCardWidth + CardGap)));
        var panel = FindUniformGridDescendant(ProjectsControl);
        if (panel != null && panel.Columns != columns)
            panel.Columns = columns;
    }

    private static UniformGrid? FindUniformGridDescendant(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UniformGrid grid) return grid;
            var found = FindUniformGridDescendant(child);
            if (found != null) return found;
        }
        return null;
    }
}