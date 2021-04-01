using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace FFVideoConverter.Controls
{
    public partial class EncodeSegmentControl : UserControl
    {
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
                    timeSpanTextBoxStart.ClearValue(TextBox.ForegroundProperty);
                }
            }
        }

        public TimeSpan Start
        {
            get
            {
                return timeSpanTextBoxStart.Value;
            }
            set
            {
                if (userInput && mediaInfo != null && value >= TimeSpan.Zero && value <= End)
                {
                    userInput = false;
                    timeSpanTextBoxStart.Value = value;
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
                return timeSpanTextBoxEnd.Value;
            }
            set
            {
                if (userInput && mediaInfo != null && value <= mediaInfo.Duration)
                {
                    userInput = false;
                    timeSpanTextBoxEnd.Value = value;
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

        MediaPlayer mediaPlayer;
        MediaInfo mediaInfo;
        Task updateKeyFramesTask;
        TimeSpan keyframeToCompute;
        bool userInput = true;


        public EncodeSegmentControl(MediaPlayer mediaPlayer, MediaInfo mediaInfo)
        {
            InitializeComponent();

            this.mediaPlayer = mediaPlayer;
            this.mediaInfo = mediaInfo;
            timeSpanTextBoxStart.MaxTime = mediaInfo.Duration;
            timeSpanTextBoxEnd.MaxTime = mediaInfo.Duration;
            rangeSelector.Maximum = mediaInfo.Duration.TotalSeconds;
            Start = TimeSpan.Zero;
            End = mediaInfo.Duration;
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            ItemsControl itemsControl = Parent as ItemsControl;
            if (itemsControl != null) itemsControl.Items.Remove(this);
            Removed?.Invoke(this);
        }

        private void TextBoxStart_TextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (mediaInfo != null)
            {
                if (userInput)
                {
                    if (Start < End)
                    {
                        Start = timeSpanTextBoxStart.Value;
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new Action(timeSpanTextBoxStart.Undo));
                    }
                }

                if (ShowKeyframesSuggestions)
                {
                    timeSpanTextBoxStart.ClearValue(ForegroundProperty);
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

        private void TextBoxEnd_TextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (userInput && mediaInfo != null)
            {
                if (End > Start && End <= mediaInfo.Duration)
                {
                    End = timeSpanTextBoxEnd.Value;
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => timeSpanTextBoxEnd.Undo()));
                }
            }
        }

        private void TextBlockStartBefore_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (textBlockStartBefore.Text != "...")
            {
                timeSpanTextBoxStart.Value = TimeSpan.Parse(textBlockStartBefore.Text);
                timeSpanTextBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
            }
        }

        private void TextBlockStartAfter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (textBlockStartAfter.Text != "...")
            {
                timeSpanTextBoxStart.Value = TimeSpan.Parse(textBlockStartAfter.Text);
                timeSpanTextBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
            }
        }

        private async Task UpdateKeyFrameSuggestions(TimeSpan t)
        {
            textBlockStartBefore.Text = "...";
            textBlockStartAfter.Text = "...";
            var (before, after, isKeyFrame) = await FFProbe.GetNearestBeforeAndAfterKeyFrames(mediaInfo, t.TotalSeconds);
            t = TimeSpan.FromSeconds(before);
            textBlockStartBefore.Text = t.ToFormattedString(true);
            t = TimeSpan.FromSeconds(after);
            textBlockStartAfter.Text = t.ToFormattedString(true);
            timeSpanTextBoxStart.Foreground = new BrushConverter().ConvertFromString(isKeyFrame ? "#FF109320" : "#FFC12222") as SolidColorBrush;
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

        private void ButtonPlayerPosition_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            if (button.Name == "buttonPlayerPosition_Start")
            {
                timeSpanTextBoxStart.Value = mediaPlayer.Position;
            }
            else
            {
                timeSpanTextBoxEnd.Value = mediaPlayer.Position;
            }
        }
    }
}