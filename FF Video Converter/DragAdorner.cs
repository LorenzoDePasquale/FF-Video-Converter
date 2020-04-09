using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;


namespace FFVideoConverter
{
	public class DragAdorner : Adorner
	{
		private readonly Rectangle rectangleAdorner;
		private double offsetLeft;
		public double OffsetLeft
		{
			get 
			{ 
				return offsetLeft;
			}
			set
			{
				offsetLeft = value;
				UpdateLocation();
			}
		}
		private double offsetTop;
		public double OffsetTop
		{
			get
			{
				return offsetTop;
			}
			set
			{
				offsetTop = value;
				UpdateLocation();
			}
		}
		protected override int VisualChildrenCount { get { return 1; } }


		public DragAdorner(UIElement adornedElement, Size size, Brush brush) : base(adornedElement)
		{
			rectangleAdorner = new Rectangle();
			rectangleAdorner.Fill = brush;
			rectangleAdorner.Width = size.Width;
			rectangleAdorner.Height = size.Height;
			rectangleAdorner.IsHitTestVisible = false;
		}

		public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
		{
			GeneralTransformGroup generalTransformGroup = new GeneralTransformGroup();
			generalTransformGroup.Children.Add(base.GetDesiredTransform( transform));
			generalTransformGroup.Children.Add(new TranslateTransform(offsetLeft, offsetTop));
			return generalTransformGroup;
		}

		public void SetOffsets(double left, double top)
		{
			offsetLeft = left;
			offsetTop = top;
			UpdateLocation();
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			rectangleAdorner.Measure(availableSize);
			return rectangleAdorner.DesiredSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			rectangleAdorner.Arrange(new Rect(finalSize));
			return finalSize;
		}

		protected override Visual GetVisualChild(int index)
		{
			return rectangleAdorner;
		}

		private void UpdateLocation()
		{
			AdornerLayer adornerLayer = Parent as AdornerLayer;
			if (adornerLayer != null)
			{
				adornerLayer.Update(AdornedElement);
			}
		}
	}
}