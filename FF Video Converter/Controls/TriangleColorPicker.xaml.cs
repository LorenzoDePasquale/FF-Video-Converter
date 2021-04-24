using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FFVideoConverter.Controls
{
    public partial class TriangleColorPicker : UserControl
    {
        // public (double red, double green, double blue) RGBValues
        // {
        //    get
        //    {
        //        Point2 p = (Canvas.GetLeft(thumb), Canvas.GetTop(thumb));

        // }
        // }

        public TriangleColorPicker()
        {
            InitializeComponent();
        }

        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            UIElement thumb = e.Source as UIElement;
            double newLeft = Canvas.GetLeft(thumb) + e.HorizontalChange;
            double newTop = Canvas.GetTop(thumb) + e.VerticalChange;
            if (PointInTriangle((newLeft + thumb.RenderSize.Width / 2, newTop + thumb.RenderSize.Height / 2), (Width / 2, 0), (0, Height), (Width, Height)))
            {
                Canvas.SetLeft(thumb, Canvas.GetLeft(thumb) + e.HorizontalChange);
                Canvas.SetTop(thumb, Canvas.GetTop(thumb) + e.VerticalChange);
            }
        }

        // To test if a point p is inside the triangle p0p1p2 it solves this equation: p = p0 + (p1 - p0) * s + (p2 - p0) * t
        // The point p is inside the triangle if 0 <= s <= 1 and 0 <= t <= 1 and s + t <= 1, where s, t and 1-s-t are the barycentric coordinates of the point p
        // Code adapted to C# from this js snippet http://jsfiddle.net/PerroAZUL/zdaY8/1/
        private bool PointInTriangle(Point2 p, Point2 p0, Point2 p1, Point2 p2)
        {
            double area = 0.5 * (-p1.y * p2.x + p0.y * (-p1.x + p2.x) + p0.x * (p1.y - p2.y) + p1.x * p2.y);
            int sign = area < 0 ? -1 : 1;
            double s = (p0.y * p2.x - p0.x * p2.y + (p2.y - p0.y) * p.x + (p0.x - p2.x) * p.y) * sign;
            double t = (p0.x * p1.y - p0.y * p1.x + (p0.y - p1.y) * p.x + (p1.x - p0.x) * p.y) * sign;

            return s > 0 && t > 0 && (s + t) < 2 * area * sign;
        }
    }

    struct Point2
    {
        public double x;
        public double y;

        public static implicit operator Point2((double x, double y) coordinates)
        {
            return new Point2
            {
                x = coordinates.x,
                y = coordinates.y
            };
        }
    }
}
