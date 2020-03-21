using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace FFVideoConverter
{
    public partial class RangeSlider : UserControl
    {
        public double Minimum
        {
            get { return MiddleSlider.Minimum; }
            set 
            { 
                LowerSlider.Minimum = value;
                MiddleSlider.Minimum = value;
                UpperSlider.Minimum = value;
            }
        }

        public double Maximum
        {
            get { return MiddleSlider.Maximum; }
            set
            {
                LowerSlider.Maximum = value;
                MiddleSlider.Maximum = value;
                UpperSlider.Maximum = value;
            }
        }

        public double LowerValue
        {
            get { return LowerSlider.Value; }
            set { LowerSlider.Value = value; }
        }

        public double MiddleValue
        {
            get { return MiddleSlider.Value; }
            set { MiddleSlider.Value = value; }
        }

        public double UpperValue
        {
            get { return UpperSlider.Value; }
            set { UpperSlider.Value = value; }
        }

        public Visibility RangeSelectorVisibility
        {
            get { return LowerSlider.Visibility; }
            set
            {
                LowerSlider.Visibility = value;
                UpperSlider.Visibility = value;
                SelectionRangeLeft.Visibility = value;
                SelectionRangeRight.Visibility = value;
                MiddleSlider.IsSelectionRangeEnabled = value != Visibility.Visible;
            }
        }

        public event RoutedPropertyChangedEventHandler<double> MiddleSliderValueChanged;
        public event RoutedPropertyChangedEventHandler<double> LowerSliderValueChanged;
        public event RoutedPropertyChangedEventHandler<double> UpperSliderValueChanged;
        public event DragStartedEventHandler MiddleSliderDragStarted;
        public event DragCompletedEventHandler MiddleSliderDragCompleted;
        public event DragCompletedEventHandler LowerSliderDragCompleted;


        public RangeSlider()
        {
            InitializeComponent();
        }

        private void MiddleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RangeSelectorVisibility == Visibility.Visible)
            {
                if (MiddleSlider.Value - LowerSlider.Value > UpperSlider.Value - MiddleSlider.Value)
                    MiddleSlider.Value = Math.Min(MiddleSlider.Value, UpperSlider.Value);
                else MiddleSlider.Value = Math.Max(MiddleSlider.Value, LowerSlider.Value);

                Rectangle partSelectionRange = MiddleSlider.Template.FindName("PART_SelectionRange", MiddleSlider) as Rectangle;
                Rectangle partBackground = MiddleSlider.Template.FindName("PART_Background", MiddleSlider) as Rectangle;
                SelectionRangeLeft.Margin = new Thickness(LowerValue * ActualWidth / Maximum + 7, 0, ActualWidth - partSelectionRange.ActualWidth, 0);
                SelectionRangeRight.Margin = new Thickness(ActualWidth - partBackground.ActualWidth - 10, 0, (Maximum - UpperValue) * ActualWidth / Maximum + 7, 0);
            }
            MiddleSliderValueChanged?.Invoke(sender, e);
        }

        private void LowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UpperValue - LowerValue < 2)
            {
                LowerValue = e.OldValue;
            }
            else
            {
                LowerSliderValueChanged?.Invoke(sender, e);
                MiddleSlider.Value = e.NewValue;
            }
        }

        private void UpperSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UpperValue - LowerValue < 2)
            {
                UpperValue = e.OldValue;
            }
            else
            {
                UpperSliderValueChanged?.Invoke(sender, e);
                MiddleSlider.Value = e.NewValue;
            }
        }

        private void MiddleSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            MiddleSliderDragStarted?.Invoke(sender, e);
        }

        private void MiddleSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            MiddleSliderDragCompleted?.Invoke(sender, e);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Rectangle partSelectionRange = MiddleSlider.Template.FindName("PART_SelectionRange", MiddleSlider) as Rectangle;
            Rectangle partBackground = MiddleSlider.Template.FindName("PART_Background", MiddleSlider) as Rectangle;
            if (Maximum > 0)
            {
                SelectionRangeLeft.Margin = new Thickness(LowerValue * ActualWidth / Maximum + 7, 0, ActualWidth - partSelectionRange.ActualWidth, 0);
                SelectionRangeRight.Margin = new Thickness(ActualWidth - partBackground.ActualWidth - 10, 0, (Maximum - UpperValue) * ActualWidth / Maximum + 7, 0);
            }
        }

        private void LowerSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            LowerSliderDragCompleted?.Invoke(sender, e);
        }
    }
}