using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace FFVideoConverter.Controls
{
    public partial class TimeSpanTextBox : UserControl
    {
		public static readonly DependencyProperty MaxTimeProperty = DependencyProperty.RegisterAttached("MaxTime", typeof(TimeSpan), typeof(TimeSpanTextBox), new UIPropertyMetadata(TimeSpan.FromHours(10)));
		public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(TimeSpan), typeof(TimeSpanTextBox), new UIPropertyMetadata(TimeSpan.FromSeconds(1), OnValueChanged));
		public static readonly DependencyProperty ShowMillisecondsProperty = DependencyProperty.RegisterAttached("ShowMilliseconds", typeof(bool), typeof(bool), new UIPropertyMetadata(true, OnShowMillisecondsChanged));

		public TimeSpan Value
		{
			get => (TimeSpan)GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}
		public TimeSpan MaxTime
        {
			get => (TimeSpan)GetValue(MaxTimeProperty);
			set => SetValue(MaxTimeProperty, value);
		}
		public bool ShowMilliseconds
		{
			get => (bool)GetValue(ShowMillisecondsProperty);
			set => SetValue(ShowMillisecondsProperty, value);
		}

		public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;

		string formatString = "hh:mm:ss.ff";
		const string maxCharacterValues = "99:59:59.999";
		double millisecondAdjustment = 10;


		public TimeSpanTextBox()
        {
            InitializeComponent();

			textBox.PreviewTextInput += OnTextInput;
			textBox.PreviewKeyDown += OnPreviewKeyDown;
			textBox.SelectionChanged += OnSelectionChanged;

			DataObject.AddPastingHandler(textBox, OnPaste);
			// Disables the possibility to drag text inside the textBox
			DataObject.AddCopyingHandler(this, (sender, e) => { if (e.IsDragDrop) e.CancelCommand(); });
		}

		public void Undo()
        {
			textBox.Undo();
			Value = TimeSpanParse(textBox.Text);
        }

		private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			((TimeSpanTextBox)dependencyObject).OnValueChanged(e);
		}

		private static void OnShowMillisecondsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			((TimeSpanTextBox)dependencyObject).OnShowMillisecondsChanged(e);
		}

		private void OnValueChanged(DependencyPropertyChangedEventArgs e)
		{
			textBox.Text = Value.ToFormattedString(true);
			ValueChanged?.Invoke(this, e);
		}

		private void OnShowMillisecondsChanged(DependencyPropertyChangedEventArgs _)
        {
			formatString = ShowMilliseconds ? "hh:mm:ss.ff" : "hh:mm:ss";
		}

		private void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.Text))
			{
				try
				{
					var currentTimeSpan = TimeSpanParse(Convert.ToString(e.DataObject.GetData(DataFormats.Text)).Trim());
					if (currentTimeSpan > MaxTime) currentTimeSpan = MaxTime;
					if (currentTimeSpan < TimeSpan.Zero) currentTimeSpan = TimeSpan.Zero;
					Value = currentTimeSpan;
					textBox.Text = currentTimeSpan.ToFormattedString(ShowMilliseconds);
					textBox.SelectionStart = 0;
				}
				catch { }
			}
			e.CancelCommand();
		}

		private void OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
			{
				ReplaceDigit(textBox.SelectionStart - 1, '0', false);
				e.Handled = true;
			}
			else if (e.Key == Key.Delete)
			{
				ReplaceDigit(textBox.SelectionStart, '0', true);
				e.Handled = true;
			}
			else if (e.Key == Key.Right)
			{
				if (textBox.Text.Length > textBox.SelectionStart - 1)
					textBox.SelectionStart++;
				e.Handled = true;
			}
			else if (e.Key == Key.Left)
			{
				var caret = textBox.SelectionStart - 1;
				if (caret < 0) return;
				textBox.SelectionStart = (":.".Contains(textBox.Text[caret]))
								? textBox.SelectionStart - 2
								: textBox.SelectionStart - 1;
				e.Handled = true;
			}
			else if (e.Key == Key.Up)
			{
				ChangeValueDependingOnLocaton(1);
				e.Handled = true;
			}
			else if (e.Key == Key.Down)
			{
				ChangeValueDependingOnLocaton(-1);
				e.Handled = true;
			}
		}

		private void OnSelectionChanged(object sender, RoutedEventArgs e)
		{
			if (textBox.Text.Length > textBox.SelectionStart)
			{
				if (":.".Contains(textBox.Text[textBox.SelectionStart]))
					textBox.SelectionStart++;
				if (textBox.SelectionLength != 1 && textBox.SelectionStart < textBox.Text.Length)
					textBox.SelectionLength = 1;
			}
		}

		private void OnTextInput(object sender, TextCompositionEventArgs e)
		{
			if (char.IsDigit(e.Text[0]))
            {
                if (e.Text.Any(c => TestCaretAtEnd(c, textBox.SelectionStart)))
                {
                    ReplaceDigit(textBox.SelectionStart, e.Text[0], true);
                }
                else if (textBox.Text.Substring(0, textBox.SelectionStart - 1).Replace(':', '0').All(i => i == '0'))
                {
                    ChangeValueDependingOnLocaton(e.Text[0] - textBox.Text[textBox.SelectionStart]);
                }
            }

            e.Handled = true;
		}

		private bool TestCaretAtEnd(char value, int position)
		{
			return value <= maxCharacterValues[position];
		}

		private void ReplaceDigit(int position, char digit, bool normal)
		{
			if (position >= textBox.Text.Length || position < 0)
				return;

			string firstPart, lastPart;
			if (!":.".Contains(textBox.Text[position]))
			{
				firstPart = textBox.Text.Substring(0, position);
				lastPart = textBox.Text.Substring(position + 1);
			}
			else if (normal)
			{
				firstPart = textBox.Text.Substring(0, position + 1);
				lastPart = textBox.Text.Substring(position + 2);
			}
			else
			{
				firstPart = textBox.Text.Substring(0, position - 1);
				lastPart = textBox.Text.Substring(position);
				position--;
			}
			firstPart = firstPart + digit + lastPart;

			var value = TimeSpanParse(firstPart);
			if (value <= MaxTime)
            {
				Value = TimeSpanParse(firstPart);
				textBox.SelectionStart = normal ? position + 1 : position;
			}
		}

		private void ChangeValueDependingOnLocaton(int amount)
		{
			var selectionStart = textBox.SelectionStart;
			var addValue = (formatString[selectionStart] != formatString[selectionStart + 1]
					? 1 : (formatString[selectionStart] != formatString[selectionStart + 2])
					? 10 : 100) * amount;
			var newValue = Value;
			switch (formatString[selectionStart])
			{
				case 'm':
					newValue = newValue.Add(TimeSpan.FromMinutes(addValue));
					break;
				case 'h':
					newValue = newValue.Add(TimeSpan.FromHours(addValue));
					break;
				case 's':
					newValue = newValue.Add(TimeSpan.FromSeconds(addValue));
					break;
				case 'f':
					newValue = newValue.Add(TimeSpan.FromMilliseconds(addValue * millisecondAdjustment));
					break;
			}

			if (newValue >= TimeSpan.Zero && newValue <= MaxTime)
            {
				Value = newValue;
				textBox.SelectionStart = selectionStart;
			}
		}

		private static TimeSpan TimeSpanParse(string value)
		{
			int hours = 0, minutes = 0;
            string[] timePieces = value.Split(':');
			double secondsWithFraction = double.Parse(timePieces[^1]);
			double seconds = Math.Floor(secondsWithFraction);
			int milliseconds = (int)((secondsWithFraction - seconds) * 1000);
			if (timePieces.Length > 1)
				minutes = Int32.Parse(timePieces[^2]);
			if (timePieces.Length > 2)
				hours = Int32.Parse(timePieces[^3]);
			return new TimeSpan(0, hours, minutes, (int)seconds, milliseconds);
		}
	}
}