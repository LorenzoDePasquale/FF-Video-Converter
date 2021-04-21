using System;
using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter.Controls
{
    public partial class PercentageSlider : UserControl
    {
        public double Value
        {
            get
            {
                if (Percentage == 0) return 0;
                return slider.Value;
            }
            set
            {
                slider.Value = value;
            }
        }
        public int Percentage => Convert.ToInt32(slider.Value * 100);

        public event EventHandler<RoutedPropertyChangedEventArgs<double>> ValueChanged;

        public PercentageSlider()
        {
            InitializeComponent();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Percentage > 0)
            {
                textBlockPercentage.Text = $"+{Percentage}%";
            }
            else if (Percentage < 0)
            {
                textBlockPercentage.Text = $"{Percentage}%";
            }
            else
            {
                textBlockPercentage.Text = "";
            }

            ValueChanged?.Invoke(this, e);
        }
    }
}