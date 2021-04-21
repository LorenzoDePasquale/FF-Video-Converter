using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter.Controls
{
    public partial class CenteredSlider : Slider
    {
        public double Center { get; set; }

        public CenteredSlider()
        {
            InitializeComponent();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (slider.Value > Center)
            {
                slider.SelectionEnd = slider.Value;
                slider.SelectionStart = Center;
            }
            else
            {
                slider.SelectionEnd = Center;
                slider.SelectionStart = slider.Value;
            }
        }
    }
}