using System;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter.Controls
{

    public partial class MultiRangeControl : UserControl
    {
        TimeIntervalCollection intervalCollection;
        TimeSpan duration;
        static Style borderStyle;
        
        public MultiRangeControl()
        {
            InitializeComponent();

            borderStyle = Resources["cutSectionBorder"] as Style;
        }

        public void UpdateIntervalCollection(TimeIntervalCollection intervalCollection, TimeSpan duration)
        {
            this.intervalCollection = intervalCollection;
            this.duration = duration;
            
            gridCutSections.Children.Clear();
            if (intervalCollection?.Count > 0)
            {
                foreach (var timeInterval in intervalCollection)
                {
                    Border b = new Border();
                    b.Style = borderStyle;
                    double max = duration.TotalSeconds;
                    double leftMargin = timeInterval.Start.TotalSeconds * ActualWidth / max;
                    double rightMargin = (max - timeInterval.End.TotalSeconds) * ActualWidth / max;
                    b.Margin = new Thickness(leftMargin, 0, rightMargin, 0);
                    b.ToolTip = $"{timeInterval.Start.ToFormattedString(true)} - {timeInterval.End.ToFormattedString(true)}";
                    gridCutSections.Children.Add(b);
                }
            }
            else
            {
                Border b = new Border();
                b.Style = borderStyle;
                b.Margin = new Thickness(0, 0, 0, 0);
                gridCutSections.Children.Add(b);
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            UpdateIntervalCollection(intervalCollection, duration);
        }
    }
}