using System;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter
{
    public partial class RangeSelector : UserControl
    {
        public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register("LowerValue", typeof(double), typeof(RangeSelector), new UIPropertyMetadata(0d, null, LowerValueCoerceValueCallback));
        public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register("UpperValue", typeof(double), typeof(RangeSelector), new UIPropertyMetadata(1d, null, UpperValueCoerceValueCallback));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(RangeSelector), new UIPropertyMetadata(0d));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(RangeSelector), new UIPropertyMetadata(1d));

        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public double LowerValue
        {
            get { return (double)GetValue(LowerValueProperty); }
            set { SetValue(LowerValueProperty, value); }
        }

        public double UpperValue
        {
            get { return (double)GetValue(UpperValueProperty); }
            set { SetValue(UpperValueProperty, value); }
        }

        public event RoutedPropertyChangedEventHandler<double> LowerValueChanged;
        public event RoutedPropertyChangedEventHandler<double> UpperValueChanged;


        public RangeSelector()
        {
            InitializeComponent();
        }

        //Forces LowerValue to be under UpperValue
        private static object LowerValueCoerceValueCallback(DependencyObject target, object valueObject)
        {
            RangeSelector targetSlider = (RangeSelector)target;
            double value = (double)valueObject;
            return Math.Min(value, targetSlider.UpperValue);
        }

        //Forces UpperValue to be above LowerValue
        private static object UpperValueCoerceValueCallback(DependencyObject target, object valueObject)
        {
            RangeSelector targetSlider = (RangeSelector)target;
            double value = (double)valueObject;
            return Math.Max(value, targetSlider.LowerValue);
        }

        private void LowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSelectionRangeMargin();
            LowerValueChanged?.Invoke(this, e);
        }

        private void UpperSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSelectionRangeMargin();
            UpperValueChanged?.Invoke(this, e);
        }

        private void UpdateSelectionRangeMargin()
        {
            double leftMargin = lowerSlider.Value * lowerSlider.ActualWidth / Maximum;
            double rightMargin = (Maximum - upperSlider.Value) * upperSlider.ActualWidth / Maximum;
            selectionRange.Margin = new Thickness(leftMargin, 0, rightMargin, 0);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSelectionRangeMargin();
        }
    }
}