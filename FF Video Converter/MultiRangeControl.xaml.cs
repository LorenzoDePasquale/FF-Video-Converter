using System;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter
{

    public partial class MultiRangeControl : UserControl
    {
        public MultiRangeControl()
        {
            InitializeComponent();
        }

        public void UpdateIntervalCollection(TimeIntervalCollection intervalCollection, TimeSpan duration)
        {
            gridCutSections.Children.Clear();
            if (intervalCollection.Count > 0)
            {
                foreach (var timeInterval in intervalCollection)
                {
                    Border b = new Border();
                    b.Style = Resources["cutSectionBorder"] as Style;
                    double max = duration.TotalSeconds;
                    double leftMargin = timeInterval.Start.TotalSeconds * ActualWidth / max;
                    double rightMargin = (max - timeInterval.End.TotalSeconds) * ActualWidth / max;
                    b.Margin = new Thickness(leftMargin, 0, rightMargin, 0);
                    gridCutSections.Children.Add(b);
                }
            }
            else
            {
                Border b = new Border();
                b.Style = Resources["cutSectionBorder"] as Style;
                b.Margin = new Thickness(0, 0, 0, 0);
                gridCutSections.Children.Add(b);
            }
        }
    }
}
