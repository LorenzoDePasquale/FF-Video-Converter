using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace FFVideoConverter
{
    public partial class IntegerTextBox : UserControl
    {
        public readonly static DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(int), typeof(IntegerTextBox), new UIPropertyMetadata(int.MaxValue));
        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public readonly static DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(int), typeof(IntegerTextBox), new UIPropertyMetadata(int.MinValue));
        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public readonly static DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(int), typeof(IntegerTextBox), new UIPropertyMetadata(0, OnValueChanged));
        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetCurrentValue(ValueProperty, value); }
        }

        public readonly static DependencyProperty StepProperty = DependencyProperty.Register("Step", typeof(int), typeof(IntegerTextBox), new UIPropertyMetadata(1));
        public int Step
        {
            get { return (int)GetValue(StepProperty); }
            set { SetValue(StepProperty, value); }
        }

        public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;
        private void RaiseValueChangedEvent(DependencyPropertyChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }


        public IntegerTextBox()
        {
            InitializeComponent();
        }

        private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            IntegerTextBox integerTextBox = (IntegerTextBox)sender;
            integerTextBox.RaiseValueChangedEvent(e);
        }

        private void ButtonIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (Value + Step < Maximum)
            {
                Value += Step;
            }
            else
            {
                Value = Maximum;
            }
        }

        private void ButtonDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (Value - Step > Minimum)
            {
                Value -= Step;
            }
            else
            {
                Value = Minimum;
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (Int32.TryParse(e.Text, out _))
            {
                int number = Int32.Parse(textBox.Text + e.Text);
                if (number > Maximum || number < Minimum)
                    e.Handled = true;
            }
            else e.Handled = true;
        }

        private void TextBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }
    }
}