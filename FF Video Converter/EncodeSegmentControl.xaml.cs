using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace FFVideoConverter
{
    public partial class EncodeSegmentControl : UserControl
    {
        private MediaInfo mediaInfo;
        public MediaInfo MediaInfo
        {
            get => mediaInfo;
            set
            {
                mediaInfo = value;
                Start = TimeSpan.Zero;
                rangeSelector.Maximum = value.Duration.TotalSeconds;
                End = mediaInfo.Duration;
            }
        }

        public bool ShowKeyframesSuggestions
        {
            get => textBlockStartBefore.Visibility == Visibility.Visible;
            set
            {
                if (value)
                {
                    textBlockStartBefore.Visibility = Visibility.Visible;
                    textBlockStartAfter.Visibility = Visibility.Visible;
                    UpdateKeyFrameSuggestions(Start);
                }
                else
                {
                    textBlockStartBefore.Visibility = Visibility.Collapsed;
                    textBlockStartAfter.Visibility = Visibility.Collapsed;
                    textBoxStart.ClearValue(TextBox.ForegroundProperty);
                }
            }
        }

        public TimeSpan Start
        {
            get
            {
                return TimeSpan.Parse(textBoxStart.Text);
            }
            set
            {
                if (userInput && mediaInfo != null && value >= TimeSpan.Zero && value <= End)
                {
                    userInput = false;
                    textBoxStart.Text = value.ToFormattedString(true);
                    rangeSelector.LowerValue = value.TotalSeconds;
                    userInput = true;
                    StartChanged?.Invoke(this);
                }
            }
        }

        public TimeSpan End
        {
            get
            {
                return TimeSpan.Parse(textBoxEnd.Text);
            }
            set
            {
                if (userInput && mediaInfo != null && value <= mediaInfo.Duration)
                {
                    userInput = false;
                    textBoxEnd.Text = value.ToFormattedString(true);
                    rangeSelector.UpperValue = value.TotalSeconds;
                    userInput = true;
                    EndChanged?.Invoke(this);
                }
            }
        }

        public delegate void EncodeSegmentControlEventHandler(EncodeSegmentControl sender);
        public event EncodeSegmentControlEventHandler Removed;
        public event EncodeSegmentControlEventHandler StartChanged;
        public event EncodeSegmentControlEventHandler EndChanged;

        private Task updateKeyFramesTask;
        private TimeSpan keyframeToCompute;
        private bool userInput = true;


        public EncodeSegmentControl()
        {
            InitializeComponent();
        }

        public EncodeSegmentControl(MediaInfo mediaInfo)
        {
            InitializeComponent();
            MediaInfo = mediaInfo;
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            ItemsControl itemsControl = Parent as ItemsControl;
            if (itemsControl != null) itemsControl.Items.Remove(this);
            Removed?.Invoke(this);
        }

        private void TextBoxStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (mediaInfo != null)
            {
                if (userInput)
                {
                    if (Start < End)
                    {
                        Start = TimeSpan.Parse(textBoxStart.Text);
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new Action(() => textBoxStart.Undo()));
                    }
                }

                if (ShowKeyframesSuggestions)
                {
                    textBoxStart.ClearValue(TextBox.ForegroundProperty);
                    if (updateKeyFramesTask == null || updateKeyFramesTask.IsCompleted)
                    {
                        updateKeyFramesTask = UpdateKeyFrameSuggestions(Start);
                    }
                    else
                    {
                        keyframeToCompute = Start;
                    }
                }
            }
        }

        private void TextBoxEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (userInput && mediaInfo != null)
            {
                if (End > Start && End <= mediaInfo.Duration)
                {
                    End = TimeSpan.Parse(textBoxEnd.Text);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => textBoxEnd.Undo()));
                }
            }
        }

        private void TextBlockStartBefore_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (textBlockStartBefore.Text != "...")
            {
                textBoxStart.Text = textBlockStartBefore.Text;
                textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
            }
        }

        private void TextBlockStartAfter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (textBlockStartAfter.Text != "...")
            {
                textBoxStart.Text = textBlockStartAfter.Text;
                textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
            }
        }

        private async Task UpdateKeyFrameSuggestions(TimeSpan t)
        {
            textBlockStartBefore.Text = "...";
            textBlockStartAfter.Text = "...";
            var (before, after, isKeyFrame) = await mediaInfo.GetNearestBeforeAndAfterKeyFrames(t.TotalSeconds);
            t = TimeSpan.FromSeconds(before);
            textBlockStartBefore.Text = t.ToFormattedString(true);
            t = TimeSpan.FromSeconds(after);
            textBlockStartAfter.Text = t.ToFormattedString(true);
            textBoxStart.Foreground = new BrushConverter().ConvertFromString(isKeyFrame ? "#FF109320" : "#FFC12222") as SolidColorBrush;
            UpdateKeyFramesIfNecessary();
        }

        private void UpdateKeyFramesIfNecessary()
        {
            if (keyframeToCompute != TimeSpan.Zero)
            {
                updateKeyFramesTask = UpdateKeyFrameSuggestions(keyframeToCompute);
                keyframeToCompute = TimeSpan.Zero;
            }
        }

        private void RangeSelector_LowerValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userInput)
            {
                Start = TimeSpan.FromSeconds(rangeSelector.LowerValue);
            }
        }

        private void RangeSelector_UpperValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userInput)
            {
                End = TimeSpan.FromSeconds(rangeSelector.UpperValue);
            }
        }

    }
}