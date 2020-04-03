using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace FFVideoConverter
{
    public partial class ComparisonWindow : Window
    {
        private readonly MediaInfo mediaFile;
        private readonly Encoder encoder;
        private readonly FFmpegEngine ffmpegEngine;
        private ConversionOptions conversionOptions;
        private TimeSpan start, end;
        private const int PREVIEW_COUNT = 6;
        private int currentPreview = 0;
        private bool isSeeking = false, userInput = true, wasPlaying = false;


        public ComparisonWindow(MediaInfo mediaFile, Encoder encoder)
        {
            InitializeComponent();

            this.mediaFile = mediaFile;
            this.encoder = encoder;

            ffmpegEngine = new FFmpegEngine();
            ffmpegEngine.ConversionCompleted += FFmpegEngine_ConversionCompleted;
            ffmpegEngine.ProgressChanged += FFmpegEngine_ProgressChanged;

            mediaElementOriginal.Open(new Uri(mediaFile.Source));
            labelTitle.Content += encoder == Encoder.H264 ? " (H264)" : " (H265)";
            sliderPreview.Maximum = mediaFile.Duration.TotalSeconds;
            Height -= 30; //compensate for setting window chrome height to 0;

            foreach (var item in MainWindow.QUALITY)
            {
                comboBoxQuality.Items.Add(item);
            }
            comboBoxQuality.SelectedIndex = 3;

            UpdateStartEndTimes();
        }

        private void UpdateStartEndTimes()
        {
            if (mediaFile.Duration.TotalSeconds <= 4)
            {
                start = TimeSpan.FromSeconds(0);
                end = TimeSpan.FromSeconds(mediaFile.Duration.TotalSeconds);
            }
            else if (sliderPreview.Value < mediaFile.Duration.TotalSeconds - 4)
            {
                start = TimeSpan.FromSeconds(sliderPreview.Value);
                end = start.Add(TimeSpan.FromSeconds(4));
            }
            else
            {
                start = TimeSpan.FromSeconds(mediaFile.Duration.TotalSeconds - 4);
                end = TimeSpan.FromSeconds(mediaFile.Duration.TotalSeconds);
            }
            textBlockPreviewTimespan.Text = $"Preview timespan: {start.ToString(@"hh\:mm\:ss")} - {end.ToString(@"hh\:mm\:ss")}";
        }

        private void FFmpegEngine_ProgressChanged(ProgressData progressData)
        {
            if (!progressData.IsFastCut)
            {
                double processedSeconds = progressData.CurrentTime.TotalSeconds;
                double totalSeconds = progressData.TotalTime.TotalSeconds;
                double relativePercentage = processedSeconds * 100 / totalSeconds;
                double totalPercentage = 100 / PREVIEW_COUNT * (currentPreview - 1) + relativePercentage / PREVIEW_COUNT;

                Dispatcher.Invoke(() =>
                {
                    DoubleAnimation progressAnimation = new DoubleAnimation(totalPercentage, TimeSpan.FromSeconds(0.5));
                    progressBarPreview.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                }, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private void FFmpegEngine_ConversionCompleted(ProgressData progressData)
        {
            if (currentPreview < PREVIEW_COUNT)
            {
                string quality = comboBoxQuality.Items[currentPreview].ToString();
                conversionOptions.Crf = MainWindow.GetCRFFromQuality(quality, encoder);
                conversionOptions.SkipAudio = true;
                ffmpegEngine.Convert(mediaFile, $"temp\\preview_{currentPreview}.mkv", conversionOptions);
                currentPreview++;
                textBlockPreviewTimespan.Text = $"Creating preview ({currentPreview}/{PREVIEW_COUNT})";
            }
            else
            {
                DoubleAnimation progressAnimation = new DoubleAnimation(100, TimeSpan.FromSeconds(0.5));
                progressAnimation.Completed += ProgressAnimation_Completed;
                progressBarPreview.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
            }
        }

        private void ProgressAnimation_Completed(object sender, EventArgs e)
        {
            if (progressBarPreview.Value == 100)
            {
                mediaElementOriginal.Open(new Uri(Environment.CurrentDirectory + "\\temp\\source.mkv"));
                mediaElementConverted.Open(new Uri($"{Environment.CurrentDirectory}\\temp\\preview_{comboBoxQuality.SelectedIndex}.mkv"));
                sliderComparison.Visibility = Visibility.Visible;
                textBlockConverted.Visibility = Visibility.Visible;
                textBlockOriginal.Visibility = Visibility.Visible;
                blurEffect.Radius = 0;
                buttonPlayPause.IsEnabled = true;
                sliderPreview.Visibility = Visibility.Hidden;
                textBlockPreviewTimespan.Text = "Quality";
                Storyboard storyboard = FindResource("ProgressAnimationOut") as Storyboard;
                storyboard.Begin();
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private async void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            ffmpegEngine.StopConversion();
            await mediaElementConverted.Close();
            await mediaElementOriginal.Close();
            if (Directory.Exists($"{Environment.CurrentDirectory}\\temp"))
                Directory.Delete($"{Environment.CurrentDirectory}\\temp", true);
            Close();
        }

        private async void ButtonCreatePreview_Click(object sender, RoutedEventArgs e)
        {
            if (buttonCreatePreview.Content.ToString() == "Create preview")
            {
                await mediaElementOriginal.Pause();
                blurEffect.Radius = 10;
                buttonPlayPause.IsEnabled = false;
                sliderPreview.Visibility = Visibility.Hidden;
                buttonCreatePreview.Content = "Cancel";
                textBlockPreviewTimespan.Text = "Cutting original...";
                Storyboard storyboard = FindResource("ProgressAnimationIn") as Storyboard;
                storyboard.Begin();
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\temp");

                double keyFrameBefore = (await mediaFile.GetNearestBeforeAndAfterKeyFrames(start.TotalSeconds)).before;
                start = TimeSpan.FromSeconds(keyFrameBefore);
                end = start.Add(TimeSpan.FromSeconds(4));
                conversionOptions = new ConversionOptions(encoder, 6, 16)
                {
                    Start = start,
                    End = end
                }; 
                ffmpegEngine.FastCut(mediaFile, Environment.CurrentDirectory + "\\temp\\source.mkv", start.Add(TimeSpan.FromSeconds(0.1)), end);
            }
            else
            {
                ButtonClose_Click(null, null);
            }
        }

        private async void ButtonPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (buttonPlayPause.Content.ToString() == " ▶️")
            {
                buttonPlayPause.Content = " ❚❚";
                await mediaElementConverted.Play();
                await mediaElementOriginal.Play();
            }
            else
            {
                buttonPlayPause.Content = " ▶️";
                await mediaElementConverted.Pause();
                await mediaElementOriginal.Pause();
                await mediaElementConverted.Seek(mediaElementOriginal.Position);
            }
        }

        private async void ComboBoxQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (currentPreview == 6)
            {
                bool wasPlaying = mediaElementConverted.IsPlaying;
                await mediaElementOriginal.Pause();
                await mediaElementConverted.Open(new Uri($"{AppDomain.CurrentDomain.BaseDirectory}\\temp\\preview_{comboBoxQuality.SelectedIndex}.mkv"));
                await mediaElementConverted.Pause();
                await mediaElementConverted.Seek(mediaElementOriginal.Position);
                if (wasPlaying)
                {
                    await mediaElementConverted.Play();
                    await mediaElementOriginal.Play();
                }
            }
        }

        private void CheckBoxZoom_Checked(object sender, RoutedEventArgs e)
        {
            mediaElementOriginal.StretchDirection = StretchDirection.DownOnly;
            mediaElementConverted.StretchDirection = StretchDirection.DownOnly;
        }

        private void CheckBoxZoom_Unchecked(object sender, RoutedEventArgs e)
        {
            mediaElementOriginal.StretchDirection = StretchDirection.UpOnly;
            mediaElementConverted.StretchDirection = StretchDirection.UpOnly;
        }

        private void SliderPreview_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderPreview.IsVisible) UpdateStartEndTimes();
            if (userInput) mediaElementOriginal.Seek(TimeSpan.FromSeconds(sliderPreview.Value));
        }

        private void SliderPreview_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isSeeking = false;
            if (wasPlaying) mediaElementOriginal.Play();
        }

        private async void ButtonNextFrame_Click(object sender, RoutedEventArgs e)
        {
            await mediaElementOriginal.Pause();
            await mediaElementConverted.Pause();
            await mediaElementOriginal.StepForward();
            await mediaElementConverted.StepForward();
        }

        private async void ButtonPreviousFrame_Click(object sender, RoutedEventArgs e)
        {
            await mediaElementOriginal.Pause();
            await mediaElementConverted.Pause();
            await mediaElementOriginal.StepBackward();
            await mediaElementConverted.Seek(mediaElementOriginal.Position);
        }

        private void SliderPreview_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isSeeking = true;
            wasPlaying = mediaElementOriginal.IsPlaying;
            mediaElementOriginal.Pause();
        }

        private void MediaElementOriginal_PositionChanged(object sender, Unosquare.FFME.Common.PositionChangedEventArgs e)
        {
            if (!isSeeking)
            {
                userInput = false;
                sliderPreview.Value = e.Position.TotalSeconds;
                userInput = true;
            }
        }

        private async void MediaElementOriginal_MediaEnded(object sender, EventArgs e)
        {
            await mediaElementOriginal.Seek(TimeSpan.FromSeconds(0));
            await mediaElementConverted.Seek(TimeSpan.FromSeconds(0));
            await mediaElementOriginal.Play();
            await mediaElementConverted.Play();
        }
    }
}