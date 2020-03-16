using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace FFVideoConverter
{
    class TimespanTextbox
    {
		#region static

		public static readonly DependencyProperty MaxTimeProperty =	DependencyProperty.RegisterAttached("MaxTime", typeof(string),	typeof(TimespanTextbox), new UIPropertyMetadata(null, OnMaxTimeChanged));

		public static string GetMaxTime(Control o)
		{
			return (string)o.GetValue(MaxTimeProperty);
		}

		public static void SetMaxTime(Control o, string value)
		{
			o.SetValue(MaxTimeProperty, value);
		}

		private static void OnMaxTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			var timeTextBoxBehaviour = GetTimeTextBoxBehaviour(dependencyObject);
			var timeString = (string)e.NewValue;
			var timeSpan = TimeSpanParse(timeString, false);
			timeTextBoxBehaviour.MaxTimeSpanChanged(timeSpan);
		}

		public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(TimeSpan), typeof(TimespanTextbox), new UIPropertyMetadata(TimeSpan.Zero, OnValueChanged));

		public static TimeSpan GetValue(Control o)
		{
			return (TimeSpan)o.GetValue(ValueProperty);
		}

		public static void SetValue(Control o, TimeSpan value)
		{
			o.SetValue(ValueProperty, value);
		}

		public static readonly DependencyProperty TimeFormatProperty = DependencyProperty.RegisterAttached("TimeFormat", typeof(TimerFormats), typeof(TimerFormats), new UIPropertyMetadata(TimerFormats.Seconds10Ths, OnTimeFormatChanged));

		public static TimerFormats GetTimeFormat(Control o)
		{
			return (TimerFormats)o.GetValue(TimeFormatProperty);
		}

		public static void SetTimeFormat(Control o, TimerFormats TimeFormat)
		{
			o.SetValue(TimeFormatProperty, TimeFormat);
		}

		private static void OnTimeFormatChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			var timeTextBoxBehaviour = GetTimeTextBoxBehaviour(dependencyObject);
			timeTextBoxBehaviour.TimeFormatChanged((TimerFormats)e.NewValue);
		}

		public static TimespanTextbox GetTimeTextBoxBehaviour(object textBox)
		{
			var castTextBox = (TextBox)textBox;
			var control = GetTimeTextBoxBehaviour(castTextBox);
			if (control == null)
			{
				control = new TimespanTextbox(castTextBox);
				SetTimeTextBoxBehaviour(castTextBox, control);
			}
			return control;
		}

		private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			var timeTextBoxBehaviour = GetTimeTextBoxBehaviour(dependencyObject);
		}

		/// <summary>
		/// This is the private DependencyProperty where the instance to handle the time behaviour is kept
		/// </summary>
		private static readonly DependencyProperty TimeTextBoxBehaviourProperty = DependencyProperty.RegisterAttached("TimeTextBoxBehaviour", typeof(TimespanTextbox), typeof(TimespanTextbox), new UIPropertyMetadata(null, OnValueChanged));

		private static TimespanTextbox GetTimeTextBoxBehaviour(TextBox o)
		{
			return (TimespanTextbox)o.GetValue(TimeTextBoxBehaviourProperty);
		}

		private static void SetTimeTextBoxBehaviour(TextBox o, TimespanTextbox value)
		{
			o.SetValue(TimeTextBoxBehaviourProperty, value);
		}

		#endregion

		#region instance

		private const string DefaultMask = "hh:mm:ss.ff";
		private readonly TextBox _textBox;
		private TimeSpan _maxTimeSpan;
		string _maxCharacterValues = string.Empty;
		private double _millisecondAdjustment = 100;
		private string _formatString;
		private TimerFormats _timerFormat;

		public TimespanTextbox(TextBox textBox)
		{
			_textBox = textBox;
			_formatString = DefaultMask;
			textBox.PreviewTextInput += OnTextInput;
			textBox.PreviewKeyDown += OnPreviewKeyDown;
			textBox.SelectionChanged += OnSelectionChanged;
			DataObject.AddPastingHandler(textBox, OnPaste);
			_timerFormat = GetTimeFormat(textBox); //Get default
		}

		#region Event Handlers
		private void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.Text))
			{
				try
				{
					var currentTimeSpan = TimeSpanParse(Convert.ToString(e.DataObject.GetData(DataFormats.Text)).Trim(), _timerFormat == TimerFormats.Minutes);
					if (currentTimeSpan > _maxTimeSpan) currentTimeSpan = _maxTimeSpan;
					if (currentTimeSpan < TimeSpan.Zero) currentTimeSpan = TimeSpan.Zero;
					SetValue(_textBox, currentTimeSpan);
					_textBox.Text = TimeSpanFormat(currentTimeSpan, _formatString);
					_textBox.SelectionStart = 0;
				}
				catch { }
			}
			e.CancelCommand();
		}

		private void OnPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space) e.Handled = true;
			else if (e.Key == Key.Back)
			{
				ReplaceDigit(_textBox.SelectionStart - 1, '0', false);
				e.Handled = true;
			}
			else if (e.Key == Key.Delete)
			{
				ReplaceDigit(_textBox.SelectionStart, '0', true);
				e.Handled = true;
			}
			else if (e.Key == Key.Right)
			{
				if (_textBox.Text.Length > _textBox.SelectionStart - 1)
					_textBox.SelectionStart++;
				e.Handled = true;
			}
			else if (e.Key == Key.Left)
			{
				var caret = _textBox.SelectionStart - 1;
				if (caret < 0) return;
				_textBox.SelectionStart = (":.".Contains(_textBox.Text[caret]))
								? _textBox.SelectionStart - 2
								: _textBox.SelectionStart - 1;
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
			if (_textBox.Text.Length > _textBox.SelectionStart)
			{
				if (":.".Contains(_textBox.Text[_textBox.SelectionStart]))
					_textBox.SelectionStart++;
				if (_textBox.SelectionLength != 1 && _textBox.SelectionStart < _textBox.Text.Length)
					_textBox.SelectionLength = 1;
			}
		}

		private void OnTextInput(object sender, TextCompositionEventArgs e)
		{
			if (char.IsDigit(e.Text[0]))
				if (e.Text.Any(c => TestCaretAtEnd(c, _textBox.SelectionStart)))
					ReplaceDigit(_textBox.SelectionStart, e.Text[0], true);
				else if (_textBox.Text.Substring(0, _textBox.SelectionStart - 1).Replace(':', '0').All(i => i == '0'))
					ChangeValueDependingOnLocaton(e.Text[0] - _textBox.Text[_textBox.SelectionStart]);
			e.Handled = true;
		}
		#endregion

		private bool TestCaretAtEnd(char value, int position)
		{
			return value <= _maxCharacterValues[position];
		}

		private void ReplaceDigit(int position, char digit, bool normal)
		{
			if (position >= _textBox.Text.Length || position < 0) return;
			string firstPart, lastPart;
			if (!":.".Contains(_textBox.Text[position]))
			{
				firstPart = _textBox.Text.Substring(0, position);
				lastPart = _textBox.Text.Substring(position + 1);
			}
			else if (normal)
			{
				firstPart = _textBox.Text.Substring(0, position + 1);
				lastPart = _textBox.Text.Substring(position + 2);
			}
			else
			{
				firstPart = _textBox.Text.Substring(0, position - 1);
				lastPart = _textBox.Text.Substring(position);
				position--;
			}
			firstPart = firstPart + digit + lastPart;
			var value = TimeSpanParse(firstPart, _timerFormat == TimerFormats.Minutes);
			if (value > _maxTimeSpan)
				firstPart = TimeSpanFormat(_maxTimeSpan, _formatString);
			_textBox.Text = firstPart;
			SetValue(_textBox, TimeSpanParse(firstPart, _timerFormat == TimerFormats.Minutes));

			_textBox.SelectionStart = normal ? position + 1 : position;
		}

		private void ChangeValueDependingOnLocaton(int amount)
		{
			var selectionStart = _textBox.SelectionStart;
			var addValue = (_formatString[selectionStart] != _formatString[selectionStart + 1]
					? 1 : (_formatString[selectionStart] != _formatString[selectionStart + 2])
					? 10 : 100) * amount;
			var currentTimeSpan = TimeSpanParse(_textBox.Text, _timerFormat == TimerFormats.Minutes);
			switch (_formatString[selectionStart])
			{
				case 'm':
					currentTimeSpan = currentTimeSpan.Add(TimeSpan.FromMinutes(addValue));
					break;
				case 'h':
					currentTimeSpan = currentTimeSpan.Add(TimeSpan.FromHours(addValue));
					break;
				case 's':
					currentTimeSpan = currentTimeSpan.Add(TimeSpan.FromSeconds(addValue));
					break;
				case 'f':
					currentTimeSpan = currentTimeSpan.Add(TimeSpan.FromMilliseconds(addValue * _millisecondAdjustment));
					break;
			}

			if (currentTimeSpan > _maxTimeSpan) currentTimeSpan = _maxTimeSpan;
			if (currentTimeSpan < TimeSpan.Zero) currentTimeSpan = TimeSpan.Zero;
			SetValue(_textBox, currentTimeSpan);
			_textBox.Text = TimeSpanFormat(currentTimeSpan, _formatString);
			_textBox.SelectionStart = selectionStart;
		}

		private void TimeFormatChanged(TimerFormats timerFormat)
		{
			_timerFormat = timerFormat;
			MaxTimeSpanChanged(_maxTimeSpan);
		}

		private void MaxTimeSpanChanged(TimeSpan timeSpan)
		{
			_maxTimeSpan = timeSpan;
			if (timeSpan.TotalSeconds >= (100 * 60 * 60))
			{
				_formatString = "hhh:mm:ss";
				_maxCharacterValues = timeSpan.Hours.ToString().Substring(0, 1) + "99:59:59.999";
			}
			if (timeSpan.TotalSeconds >= (10 * 60 * 60))
			{
				_formatString = "hh:mm:ss";
				_maxCharacterValues = timeSpan.Hours.ToString().Substring(0, 1) + "9:59:59.999";
			}
			else if (timeSpan.TotalSeconds >= (60 * 60))
			{
				_formatString = "h:mm:ss";
				_maxCharacterValues = timeSpan.Hours.ToString() + ":59:59.999";
			}
			else if (timeSpan.TotalSeconds >= (10 * 60))
			{
				_formatString = "mm:ss";
				_maxCharacterValues = timeSpan.Minutes.ToString().Substring(1) + "9:59.999";
			}
			else if (timeSpan.TotalSeconds >= (60))
			{
				_formatString = "m:ss";
				_maxCharacterValues = timeSpan.Minutes.ToString() + ":59.999";
			}
			else if (timeSpan.TotalSeconds >= (10))
			{
				_formatString = "ss";
				_maxCharacterValues = timeSpan.Seconds.ToString().Substring(1) + "9.999";
			}
			else
			{
				_formatString = "s";
				_maxCharacterValues = timeSpan.Seconds.ToString() + ".999";
			}

			switch (GetTimeFormat(_textBox))
			{
				case TimerFormats.Seconds10Ths:
					_formatString += ".f   ";
					_millisecondAdjustment = 100;
					break;
				case TimerFormats.Seconds100Ths:
					_formatString += ".ff  ";
					_millisecondAdjustment = 10;
					break;
				case TimerFormats.Seconds1000Ths:
					_formatString += ".fff ";
					_millisecondAdjustment = 1;
					break;
				case TimerFormats.Minutes:
					_formatString = _formatString.Substring(0, _formatString.Length - 3) + " ";
					break;
			}
			var valueTimeSpan = GetValue(_textBox);
			_textBox.Text = TimeSpanFormat(valueTimeSpan, _formatString);
		}

		#endregion

		#region private methods

		private static string TimeSpanFormat(TimeSpan timeSpan, string formatString)
		{
			var hours = Math.Floor(timeSpan.TotalHours).ToString("000");
			var minutes = timeSpan.Minutes.ToString("00");
			var seconds = timeSpan.Seconds.ToString("00");
			var milliseconds = timeSpan.Milliseconds.ToString("000");
			var newString = formatString;
			if (formatString.Contains("hhh")) newString
							= newString.Replace("hhh", hours);
			if (formatString.Contains("hh")) newString
							= newString.Replace("hh", hours.Substring(1));
			else if (formatString.Contains("h")) newString
							= newString.Replace('h', hours[2]);
			if (formatString.Contains("mm")) newString
							= newString.Replace("mm", minutes);
			else if (formatString.Contains("m")) newString
							= newString.Replace("m", minutes.Substring(1));
			if (formatString.Contains("ss")) newString
							= newString.Replace("ss", seconds);
			else if (formatString.Contains("s")) newString
							= newString.Replace("s", seconds.Substring(1));
			if (formatString.Contains("fff")) newString
							= newString.Replace("fff", milliseconds);
			else if (formatString.Contains("ff")) newString
							= newString.Replace("ff", milliseconds.Substring(0, 2));
			else if (formatString.Contains("f")) newString
							= newString.Replace("f", milliseconds.Substring(0, 1));
			return newString.Trim();
		}

		private static TimeSpan TimeSpanParse(string value, bool minutesFormat)
		{
			if (minutesFormat) value += ":00";
			int hours = 0, minutes = 0;
			var timePieces = value.Split(':');
			double secondsWithFraction = double.Parse(timePieces.Last());
			double seconds = Math.Floor(secondsWithFraction);
			int milliseconds = (int)((secondsWithFraction - seconds) * 1000);
			if (timePieces.Length > 1)
				minutes = Int32.Parse(timePieces[timePieces.Length - 2]);
			if (timePieces.Length > 2)
				hours = Int32.Parse(timePieces[timePieces.Length - 3]);
			return new TimeSpan(0, hours, minutes, (int)seconds, milliseconds);
		}

		#endregion

		public enum TimerFormats
		{ 
			Seconds, Seconds10Ths, Seconds100Ths, Seconds1000Ths, Minutes 
		}

	}
}
