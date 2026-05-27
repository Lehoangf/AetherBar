using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AetherBar.UI;

internal static class MediaIcons
{
    public static UIElement CreatePlay(double size, Brush brush)
    {
        return new Path
        {
            Data = Geometry.Parse("M37.01,20.61l-18.94-10.94c-3.38-1.95-7.61.49-7.61,4.39v21.87c0,3.9,4.23,6.34,7.61,4.39l18.94-10.94c3.38-1.95,3.38-6.83,0-8.78Z"),
            Fill = brush,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(2)
        };
    }

    public static UIElement CreatePause(double size, Brush brush)
    {
        var canvas = new Canvas { Width = size, Height = size };
        var r1 = new Rectangle
        {
            Width = size * 0.326,
            Height = size * 0.634,
            RadiusX = size * 0.06,
            RadiusY = size * 0.06,
            Fill = brush
        };
        Canvas.SetLeft(r1, size * 0.144);
        Canvas.SetTop(r1, size * 0.183);
        canvas.Children.Add(r1);

        var r2 = new Rectangle
        {
            Width = size * 0.326,
            Height = size * 0.634,
            RadiusX = size * 0.06,
            RadiusY = size * 0.06,
            Fill = brush
        };
        Canvas.SetLeft(r2, size * 0.53);
        Canvas.SetTop(r2, size * 0.183);
        canvas.Children.Add(r2);

        canvas.Margin = new Thickness(2);
        return canvas;
    }

    public static UIElement CreatePrevious(double size, Brush brush)
    {
        var group = new GeometryGroup();
        group.Children.Add(Geometry.Parse("M27.99,27.59l11.17,6.45c1.99,1.15,4.48-.29,4.48-2.59v-12.89c0-2.3-2.49-3.74-4.48-2.59l-11.17,6.45c-1.99,1.15-1.99,4.03,0,5.18Z"));
        group.Children.Add(Geometry.Parse("M7.85,27.59l11.17,6.45c1.99,1.15,4.48-.29,4.48-2.59v-12.89c0-2.3-2.49-3.74-4.48-2.59l-11.17,6.45c-1.99,1.15-1.99,4.03,0,5.18Z"));

        return new Path
        {
            Data = group,
            Fill = brush,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(2)
        };
    }

    public static UIElement CreateNext(double size, Brush brush)
    {
        var group = new GeometryGroup();
        group.Children.Add(Geometry.Parse("M22.01,22.41l-11.17-6.45c-1.99-1.15-4.48.29-4.48,2.59v12.89c0,2.3,2.49,3.74,4.48,2.59l11.17-6.45c1.99-1.15,1.99-4.03,0-5.18Z"));
        group.Children.Add(Geometry.Parse("M42.15,22.41l-11.17-6.45c-1.99-1.15-4.48.29-4.48,2.59v12.89c0,2.3,2.49,3.74,4.48,2.59l11.17-6.45c1.99-1.15,1.99-4.03,0-5.18Z"));

        return new Path
        {
            Data = group,
            Fill = brush,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(2)
        };
    }
}
