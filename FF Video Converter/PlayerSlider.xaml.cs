using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;


namespace FFVideoConverter
{
    public partial class PlayerSlider : UserControl
    {
        public double Minimum
        {
            get => MiddleSlider.Minimum;
            set => MiddleSlider.Minimum = value;
        }

        public double Maximum
        {
            get => MiddleSlider.Maximum;
            set => MiddleSlider.Maximum = value;
        }

        public double Value
        {
            get => MiddleSlider.Value;
            set => MiddleSlider.Value = value;
        }


        public event RoutedPropertyChangedEventHandler<double> ValueChanged;
        public event DragStartedEventHandler MiddleSliderDragStarted;
        public event DragCompletedEventHandler MiddleSliderDragCompleted;


        public PlayerSlider()
        {
            InitializeComponent();
        }

        private void MiddleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ValueChanged?.Invoke(sender, e);
        }

        private void MiddleSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            MiddleSliderDragStarted?.Invoke(sender, e);
        }

        private void MiddleSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            MiddleSliderDragCompleted?.Invoke(sender, e);
        }
    }
}