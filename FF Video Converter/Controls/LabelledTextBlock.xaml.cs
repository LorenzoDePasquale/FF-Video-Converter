using System.Windows;
using System.Windows.Controls;


namespace FFVideoConverter.Controls
{
    public partial class LabelledTextBlock : UserControl
    {
        public readonly static DependencyProperty LabelProperty = DependencyProperty.Register("Label", typeof(string), typeof(LabelledTextBlock), new UIPropertyMetadata(""));
        public readonly static DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(LabelledTextBlock), new UIPropertyMetadata(""));

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public LabelledTextBlock()
        {
            InitializeComponent();
        }

        public LabelledTextBlock(string label, string text)
        {
            InitializeComponent();

            Label = label;
            Text = text;
        }
    }
}