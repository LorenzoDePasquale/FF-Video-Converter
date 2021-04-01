using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;


namespace FFVideoConverter.Controls
{
    public partial class Expander : UserControl
    {
        public UIElement Header { get; set; }
        public UIElement Child { get; set; }

        public Expander()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            DoubleAnimation animation = new DoubleAnimation(Child.RenderSize.Height, new Duration(TimeSpan.FromSeconds(0.2)));
            contentControl.BeginAnimation(ContentControl.HeightProperty, animation);
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            DoubleAnimation animation = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.2)));
            contentControl.BeginAnimation(ContentControl.HeightProperty, animation);
        }
    }
}
