using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;


namespace FFVideoConverter
{
    public partial class MainWindow : Window
    {
        public static readonly string[] QUALITY = { "Best", "Very good", "Good", "Medium", "Low", "Very low" };

        private static readonly string[] SUPPORTED_EXTENSIONS = { ".mkv", ".mp4", ".avi", "m4v", ".webm", ".m3u8" };
        private FFmpegEngine ffmpegEngine = new FFmpegEngine();
        private readonly MethodRunner<TimeSpan> textBoxStartTextChangedMethodRunner;
        private MediaInfo mediaInfo;
        private const int RECT_MIN_SIZE = 20;
        private string currentOutputPath = "";
        private float outputFps;
        private bool isSeeking = false;
        private bool userInput = true;
        private bool wasPlaying = false;
        private bool isPlayerExpanded = false;
        private bool webStream = false;
        private bool isMediaOpen = false;

        enum HitLocation
        {
            None, Body, UpperLeft, UpperRight, LowerRight, LowerLeft, Left, Right, Top, Bottom
        };
        HitLocation MouseHitLocation = HitLocation.None;
        bool isDragging = false;
        Point LastPoint;


        public MainWindow()
        {
            InitializeComponent();

            textBoxStartTextChangedMethodRunner = new MethodRunner<TimeSpan>(UpdateKeyFrameSuggestions, TimeSpan.FromMilliseconds(1500));

            TaskbarItemInfo = new TaskbarItemInfo();
            Height -= 30;
            gridSourceControls.Visibility = Visibility.Collapsed;
            mediaElementInput.PositionChanged += MediaElementInput_PositionChanged;

            comboBoxEncoder.Items.Add("H.264 (x264)");
            comboBoxEncoder.Items.Add("H.265 (x265)");
            comboBoxEncoder.SelectedIndex = 0;
            comboBoxFramerate.Items.Add("Same as source");
            comboBoxFramerate.SelectedIndex = 0;
            foreach (var item in FFmpegEngine.PRESETS)
            {
                comboBoxPreset.Items.Add(item);
            }
            comboBoxPreset.SelectedIndex = 3;
            foreach (var item in QUALITY)
            {
                comboBoxQuality.Items.Add(item);
            }
            comboBoxQuality.SelectedIndex = 2;
            comboBoxResolution.Items.Add("Same as source");
            comboBoxResolution.SelectedIndex = 0;            
        }

        #region Load

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                string sourcePath = Environment.GetCommandLineArgs()[1];
                string extension = Path.GetExtension(sourcePath);
                if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                {
                    mediaInfo = await MediaInfo.Open(sourcePath);
                    OpenSource();
                }
            }
        }

        private void OpenSource()
        {
            isMediaOpen = false;
            string sourcePath = mediaInfo.Source;

            if (mediaInfo.IsLocal)
            {
                string extension = Path.GetExtension(sourcePath);
                textBoxDestination.Text = sourcePath.Remove(sourcePath.LastIndexOf('.')) + " converted" + extension;
                labelTitle.Content = Path.GetFileName(sourcePath);
                webStream = false;
            }
            else
            {
                textBoxDestination.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\stream.mp4";
                labelTitle.Content = !String.IsNullOrEmpty(mediaInfo.Title) ? mediaInfo.Title : "[Network stream]";
                webStream = true;
            }

            mediaElementInput.Open(new Uri(sourcePath));
            mediaElementInput.Background = Brushes.Black;
            borderSource.BorderThickness = new Thickness(0);

            textBlockDuration.Text = mediaInfo.Duration.ToString(@"hh\:mm\:ss\.ff");
            textBlockCodec.Text = $"{mediaInfo.Codec} / {mediaInfo.AudioCodec}";
            double fps = Math.Round(mediaInfo.Framerate, 2);
            textBlockFramerate.Text = !Double.IsNaN(fps) ? fps + " fps" : "-";
            textBlockBitrate.Text = mediaInfo.Bitrate + " Kbps";
            textBlockResolution.Text = mediaInfo.Width != 0 ? $"{mediaInfo.Width}x{mediaInfo.Height}" : "-";
            textBlockInputSize.Text = GetBytesReadable(mediaInfo.Size);
            if (!String.IsNullOrEmpty(mediaInfo.AspectRatio) && mediaInfo.AspectRatio != "N/A") textBlockResolution.Text += $" ({mediaInfo.AspectRatio})";

            checkBoxCrop.IsEnabled = true;
            checkBoxCrop.IsChecked = false;
            checkBoxCut.IsEnabled = true;
            checkBoxFastCut.IsChecked = false;
            checkBoxCut.IsChecked = false;
            textBoxStart.Text = "00:00:00.00";
            textBoxEnd.Text = textBlockDuration.Text;
            rangeSliderCut.Maximum = mediaInfo.Duration.TotalSeconds;
            rangeSliderCut.UpperValue = mediaInfo.Duration.TotalSeconds;
            rangeSliderCut.LowerValue = 0;
            rangeSliderCut.MiddleValue = 0;
            buttonPlayPause.Content = " ▶️";
            shadowEffect.Opacity = 1;
            gridSourceControls.Visibility = Visibility.Visible;

            SetComboBoxFramerate(mediaInfo.Framerate);
            SetComboBoxResolution(mediaInfo.Height);          

            buttonConvert.IsEnabled = true;
            buttonPreview.IsEnabled = true;
            isMediaOpen = true;
        }

        private void SetComboBoxFramerate(double framerate)
        {
            comboBoxFramerate.Items.Clear();
            comboBoxFramerate.Items.Add("Same as source");
            if (framerate >= 23)
                comboBoxFramerate.Items.Add("24");
            if (framerate >= 29)
                comboBoxFramerate.Items.Add("30");
            if (framerate >= 59)
                comboBoxFramerate.Items.Add("60");
            if (framerate >= 119)
                comboBoxFramerate.Items.Add("120");
            if (framerate >= 143)
                comboBoxFramerate.Items.Add("144");
            comboBoxFramerate.SelectedIndex = 0;
        }

        private void SetComboBoxResolution(double height)
        {
            comboBoxResolution.Items.Clear();
            comboBoxResolution.Items.Add("Same as source");
            if (height >= 2160)
                comboBoxResolution.Items.Add("3840×2160 (4K UHD)");
            if (height >= 1440)
                comboBoxResolution.Items.Add("2560x1440 (QHD)");
            if (height >= 1080)
                comboBoxResolution.Items.Add("1920x1080 (FullHD)");
            if (height >= 900)
                comboBoxResolution.Items.Add("1600x900 (HD+)");
            if (height >= 720)
                comboBoxResolution.Items.Add("1280x720 (HD)");
            if (height >= 540)
                comboBoxResolution.Items.Add("960x540 (qHD)");
            if (height >= 480)
                comboBoxResolution.Items.Add("854x480 (FWVGA)");
            if (height >= 360)
                comboBoxResolution.Items.Add("640x360 (nHD)");
            comboBoxResolution.SelectedIndex = 0;
        }

        private void ButtonOpenStream_Click(object sender, RoutedEventArgs e)
        {
            OpenStreamWindow osw = new OpenStreamWindow();
            osw.ShowDialog();
            if (osw.MediaStream != null)
            {
                mediaInfo = osw.MediaStream;
                OpenSource();
            }
        }

        private async void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select the source file";
            ofd.Multiselect = false;
            ofd.Filter = "Video files|";
            foreach (var item in SUPPORTED_EXTENSIONS)
            {
                ofd.Filter += $"*{item};";
            }
            bool? result = ofd.ShowDialog();
            if (result == true)
            {
                mediaInfo = await MediaInfo.Open(ofd.FileName);
                OpenSource();
            }
        }

        private void Rectangle_DragEnter(object sender, DragEventArgs e)
        {
            Storyboard storyboard = FindResource("DragOverAnimation") as Storyboard;
            storyboard.Begin();
        }

        private void Rectangle_DragLeave(object sender, DragEventArgs e)
        {
            Storyboard storyboard = FindResource("DragOverAnimation") as Storyboard;
            storyboard.Stop();
        }

        private async void Rectangle_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                string extension = Path.GetExtension(paths[0]);
                if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                {
                    mediaInfo = await MediaInfo.Open(paths[0]);
                    OpenSource();
                }
                else
                {
                    MessageBox.Show("File not supported!");
                }
            }
            Storyboard storyboard = FindResource("DragOverAnimation") as Storyboard;
            storyboard.Stop();
        }

        #endregion

        #region Title Bar controls

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            ffmpegEngine.StopConversion();
            Close();
        }

        #endregion

        #region Conversion settings

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Select the destination file";
            sfd.Filter = "MKV|*.mkv|MP4|*.mp4";
            sfd.FileName = Path.GetFileNameWithoutExtension(mediaInfo.Source) + "_x264";
            string extension = mediaInfo.Source.Substring(mediaInfo.Source.LastIndexOf('.'));
            if (extension == ".mp4") sfd.FilterIndex = 2;
            bool? result = sfd.ShowDialog();
            if (result == true)
            {
                textBoxDestination.Text = sfd.FileName;
            }
        }

        private void ButtonPreview_Click(object sender, RoutedEventArgs e)
        {
            if (isMediaOpen && mediaInfo.IsLocal && File.Exists(mediaInfo.Source))
            {
                new ComparisonWindow(mediaInfo, comboBoxEncoder.SelectedIndex == 0 ? Encoder.H264 : Encoder.H265).ShowDialog();
            }
        }

        private void TextBoxStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isMediaOpen && TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start) && TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
            {
                if ((end - start).TotalSeconds > 2)
                {
                    textBlockOutputDuration.Text = $"Duration: {(end - start).ToString(@"hh\:mm\:ss")}";
                    userInput = false;
                    rangeSliderCut.LowerValue = start.TotalSeconds;
                    userInput = true;
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => textBoxStart.Undo()));
                }

                if (checkBoxFastCut.IsChecked == true)
                {
                    textBlockStartBefore.Text = "...";
                    textBlockStartAfter.Text = "...";
                    textBoxStart.ClearValue(TextBox.ForegroundProperty);
                    textBoxStartTextChangedMethodRunner.Run(start);
                }
            }            
        }

        private void TextBoxEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isMediaOpen && TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start) && TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
            {
                if ((end - start).TotalSeconds > 2 && end <= mediaInfo.Duration)
                {
                    textBlockOutputDuration.Text = $"Duration: {(end - start).ToString(@"hh\:mm\:ss")}";
                    userInput = false;
                    rangeSliderCut.UpperValue = end.TotalSeconds;
                    userInput = true;
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => textBoxEnd.Undo()));
                }
            }
        }

        private void TextBlockStartBefore_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            textBoxStart.Text = textBlockStartBefore.Text;
            textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
        }

        private void TextBlockStartAfter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            textBoxStart.Text = textBlockStartAfter.Text;
            textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
        }

        private async void UpdateKeyFrameSuggestions(TimeSpan t)
        {
            var (before, after, isKeyFrame) = await mediaInfo.GetNearestBeforeAndAfterKeyFrames(t.TotalSeconds);
            t = TimeSpan.FromSeconds(before == 0 ? 0 : before);
            textBlockStartBefore.Text = t.ToString(@"hh\:mm\:ss\.ff");
            t = TimeSpan.FromSeconds(after);
            textBlockStartAfter.Text = t.ToString(@"hh\:mm\:ss\.ff");
            if (isKeyFrame)
            {
                textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FF109320") as SolidColorBrush;
            }
            else
            {
                textBoxStart.Foreground = new BrushConverter().ConvertFromString("#FFC12222") as SolidColorBrush;
            }
        }

        private void CheckBoxCut_Click(object sender, RoutedEventArgs e)
        {
            if (checkBoxCut.IsChecked == true)
            {
                rangeSliderCut.RangeSelectorVisibility = Visibility.Visible;
                TimeSpan start = TimeSpan.Parse(textBoxStart.Text);
                TimeSpan end = TimeSpan.Parse(textBoxEnd.Text);
                textBlockOutputDuration.Text = $"Duration: {(end - start).ToString(@"hh\:mm\:ss")}";
            }
            else
            {
                rangeSliderCut.RangeSelectorVisibility = Visibility.Hidden;
                checkBoxFastCut.IsChecked = false;
                CheckBoxFastCut_Click(null, null);
            }
        }

        private void CheckBoxFastCut_Click(object sender, RoutedEventArgs e)
        {
            if (checkBoxFastCut.IsChecked == true)
            {
                TextBoxStart_TextChanged(null, null);

                checkBoxCrop.IsChecked = false;
                checkBoxCrop.IsEnabled = false;
                comboBoxEncoder.IsEnabled = false;
                comboBoxFramerate.IsEnabled = false;
                comboBoxPreset.IsEnabled = false;
                comboBoxQuality.IsEnabled = false;
                comboBoxResolution.IsEnabled = false;
            }
            else
            {
                textBoxStart.ClearValue(TextBox.ForegroundProperty);

                checkBoxCrop.IsEnabled = true;
                comboBoxEncoder.IsEnabled = true;
                comboBoxFramerate.IsEnabled = true;
                comboBoxPreset.IsEnabled = true;
                comboBoxQuality.IsEnabled = true;
                comboBoxResolution.IsEnabled = true;
            }
        }

        private void CheckBoxCrop_Click(object sender, RoutedEventArgs e)
        {
            if (checkBoxCrop.IsChecked == true)
            {
                canvasCropVideo.Visibility = Visibility.Visible;
                comboBoxResolution.IsEnabled = false;

                double cropTop = Canvas.GetTop(rectangleCropVideo) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropLeft = Canvas.GetLeft(rectangleCropVideo) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                double cropBottom = (canvasCropVideo.ActualHeight - rectangleCropVideo.Height - Canvas.GetTop(rectangleCropVideo)) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropRight = (canvasCropVideo.ActualWidth - rectangleCropVideo.Width - Canvas.GetLeft(rectangleCropVideo)) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                integerTextBoxCropTop.Value = (int)cropTop;
                integerTextBoxCropLeft.Value = (int)cropLeft;
                integerTextBoxCropBottom.Value = (int)cropBottom;
                integerTextBoxCropRight.Value = (int)cropRight;
            }
            else
            {
                canvasCropVideo.Visibility = Visibility.Hidden;
                comboBoxResolution.IsEnabled = true;
            }
        }

        private void IntegerTextBoxCrop_ValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!isDragging)
            {
                double newLeft = integerTextBoxCropLeft.Value * canvasCropVideo.ActualWidth / mediaInfo.Width;
                double newTop = integerTextBoxCropTop.Value * canvasCropVideo.ActualHeight / mediaInfo.Height;
                double newRight = integerTextBoxCropRight.Value * canvasCropVideo.ActualWidth / mediaInfo.Width;
                double newBottom = integerTextBoxCropBottom.Value * canvasCropVideo.ActualHeight / mediaInfo.Height;
                double newWidth = canvasCropVideo.ActualWidth - newLeft - newRight;
                double newHeight = canvasCropVideo.ActualHeight - newTop - newBottom;

                if (newWidth < RECT_MIN_SIZE || newHeight < RECT_MIN_SIZE)
                {
                    IntegerTextBox itb = (IntegerTextBox)sender;
                    itb.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                    itb.Value = (int)e.OldValue;
                    itb.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                }
                else
                {
                    Canvas.SetLeft(rectangleCropVideo, newLeft);
                    Canvas.SetTop(rectangleCropVideo, newTop);
                    rectangleCropVideo.Width = newWidth;
                    rectangleCropVideo.Height = newHeight;
                    textBlockOutputResolution.Text = $"{(mediaInfo.Width - integerTextBoxCropLeft.Value - integerTextBoxCropRight.Value).ToString("0")}x{(mediaInfo.Height - integerTextBoxCropTop.Value - integerTextBoxCropBottom.Value).ToString("0")}";
                }
            }
        }

        #endregion

        #region Conversion process

        private async void ButtonConvert_Click(object sender, RoutedEventArgs e)
        {
            if (textBoxDestination.Text.EndsWith("mp4") && mediaInfo.AudioCodec.ToLower() == "opus")
            {
                MessageBox.Show("Opus audio in mp4 container is currently unsupported.\nEither use aac audio or mkv container.", "FF Video Converter");
                return;
            }

            textBlockProgress.Text = "Starting conversion process...";

            ffmpegEngine = new FFmpegEngine();
            ffmpegEngine.ProgressChanged += UpdateProgress;
            ffmpegEngine.ConversionCompleted += ConversionCompleted;
            Encoder selectedEncoder = comboBoxEncoder.SelectedIndex == 0 ? Encoder.H264 : Encoder.H265;
            ConversionOptions conversionOptions = new ConversionOptions(selectedEncoder, (byte)comboBoxPreset.SelectedIndex, GetCRFFromQuality(comboBoxQuality.Text, selectedEncoder));
            if (checkBoxCrop.IsChecked == true)
            {
                conversionOptions.CropData = new CropData((short)integerTextBoxCropLeft.Value, (short)integerTextBoxCropTop.Value, (short)integerTextBoxCropRight.Value, (short)integerTextBoxCropBottom.Value);
            }
            else if (comboBoxResolution.SelectedIndex != 0)
            {
                conversionOptions.Resolution = GetResolutionFromString(comboBoxResolution.Text);
            }
            if (comboBoxFramerate.SelectedIndex != 0)
            {
                conversionOptions.Framerate = Convert.ToByte(comboBoxFramerate.SelectedItem);
            }
            outputFps = comboBoxFramerate.SelectedIndex == 0 ? Convert.ToSingle(mediaInfo.Framerate) : Convert.ToSingle(comboBoxFramerate.SelectedItem);

            if (checkBoxCut.IsChecked == true)
            {
                if (!TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start))
                {
                    MessageBox.Show("Enter a valid start time", "FF Video Converter");
                    return;
                }
                if (!TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
                {
                    MessageBox.Show("Enter a valid end time", "FF Video Converter");
                    return;
                }
                if (checkBoxFastCut.IsChecked == true)
                {
                    start = start.Add(TimeSpan.FromSeconds(0.2));
                    ffmpegEngine.FastCut(mediaInfo.Source, textBoxDestination.Text, start.ToString(@"hh\:mm\:ss\.ff"), textBoxEnd.Text);
                    currentOutputPath = textBoxDestination.Text;
                    return;
                }
                else
                {
                    conversionOptions.Start = start;
                    conversionOptions.End = end;
                }
            }

            ffmpegEngine.Convert(mediaInfo, textBoxDestination.Text, conversionOptions);

            currentOutputPath = textBoxDestination.Text;
            buttonPauseResume.IsEnabled = true;
            buttonCancel.IsEnabled = true;
            buttonConvert.IsEnabled = false;
            buttonPreview.IsEnabled = false;
            buttonOpenFile.IsEnabled = false;
            buttonOpenStream.IsEnabled = false;
            checkBoxCrop.IsEnabled = false;
            checkBoxCut.IsEnabled = false;
            await mediaElementInput.Pause();
            buttonPlayPause.Content = " ▶️";
            gridSourceMedia.IsEnabled = false;
            buttonOpenOutput.Visibility = Visibility.Hidden;
            Storyboard storyboard = FindResource("ProgressAnimationIn") as Storyboard;
            storyboard.Begin();
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
        }

        private void UpdateProgress(ProgressData progressData)
        {
            double secondsToEncode = progressData.TotalTime.TotalSeconds - progressData.CurrentTime.TotalSeconds;
            double remainingTime = progressData.EncodingSpeed == 0 ? 0 : (secondsToEncode) / progressData.EncodingSpeed;
            double percentage = Math.Min(progressData.CurrentFrame * 100 / (outputFps * progressData.TotalTime.TotalSeconds), 99.4);
            long approximateOutputByteSize = progressData.CurrentByteSize + (long)(progressData.AverageBitrate * 1000 * secondsToEncode / 8);

            Dispatcher.Invoke(() =>
            {
                DoubleAnimation progressAnimation = new DoubleAnimation(percentage, TimeSpan.FromSeconds(0.5));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                TaskbarItemInfo.ProgressValue = percentage / 100;
                textBlockProgress.Text = $"Processed: {progressData.CurrentTime.ToString(@"hh\:mm\:ss")} / {progressData.TotalTime.ToString(@"hh\:mm\:ss")}";
                if (!webStream) textBlockProgress.Text += $"  @ {progressData.EncodingSpeed}x speed";
                textBlockSize.Text = $"Output size: {GetBytesReadable(progressData.CurrentByteSize)}";
                if (progressData.AverageBitrate > 0) textBlockSize.Text += $" / {GetBytesReadable(approximateOutputByteSize)} (estimated)";
                Title = Math.Floor(percentage) + "%   " + TimeSpan.FromSeconds(remainingTime).ToString(@"hh\:mm\:ss");
                labelProgress.Content = $"Progress: {Math.Floor(percentage)}%   Remaining time: {TimeSpan.FromSeconds(remainingTime).ToString(@"hh\:mm\:ss")}";
            }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private async void ConversionCompleted(ProgressData progressData)
        {
            DoubleAnimation progressAnimation = new DoubleAnimation(100, TimeSpan.FromSeconds(0));
            progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
            textBlockProgress.Text = progressData.IsFastCut ? "Video cut!" : "Video converted!";
            string outputSize = GetBytesReadable(new FileInfo(currentOutputPath).Length);
            textBlockSize.Text = "Output size: " + outputSize;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            Storyboard storyboard = FindResource("ProgressAnimationOut") as Storyboard;
            storyboard.Begin();
            buttonConvert.IsEnabled = true;
            buttonPreview.IsEnabled = true;
            buttonOpenFile.IsEnabled = true;
            buttonOpenStream.IsEnabled = true;
            buttonPauseResume.IsEnabled = false;
            buttonCancel.IsEnabled = false;
            checkBoxCrop.IsEnabled = true;
            checkBoxCut.IsEnabled = true;
            gridSourceMedia.IsEnabled = true;
            buttonOpenOutput.Visibility = Visibility.Visible;
            Title = "AVC to HEVC Converter";

            MediaInfo outputFile = await MediaInfo.Open(currentOutputPath);
            textBlockDuration.Text += $"   ⟶   {outputFile.Duration.ToString(@"hh\:mm\:ss\.ff")}";
            textBlockCodec.Text += $"   ⟶   {outputFile.Codec} / {outputFile.AudioCodec}";
            textBlockFramerate.Text += $"   ⟶   {outputFile.Framerate} fps";
            textBlockBitrate.Text += $"   ⟶   {outputFile.Bitrate} Kbps";
            textBlockResolution.Text += $"   ⟶   {outputFile.Width + "x" + outputFile.Height}";
            textBlockInputSize.Text += $"   ⟶   {outputSize}";
            if (!String.IsNullOrEmpty(outputFile.AspectRatio) && outputFile.AspectRatio != "N/A") textBlockResolution.Text += $" ({outputFile.AspectRatio})";
        }

        private void ButtonPauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (buttonPauseResume.Content.ToString() == "❚❚")
            {
                ffmpegEngine.PauseConversion();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused;
                progressBarConvertProgress.Foreground = new BrushConverter().ConvertFromString("#FFB2B200") as SolidColorBrush;
                buttonPauseResume.Content = " ▶️";
                buttonPauseResume.ToolTip = "Resume";
            }
            else
            {
                ffmpegEngine.ResumeConversion();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                progressBarConvertProgress.ClearValue(ForegroundProperty);
                buttonPauseResume.Content = "❚❚";
                buttonPauseResume.ToolTip = "Pause";
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            ffmpegEngine.ProgressChanged -= UpdateProgress;
            ffmpegEngine.StopConversion();
            progressBarConvertProgress.ClearValue(ForegroundProperty); 
            DoubleAnimation progressAnimation = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
            progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
            textBlockProgress.Text = "Conversion canceled";
            textBlockSize.Text = "";
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
            Storyboard storyboard = FindResource("ProgressAnimationOut") as Storyboard;
            storyboard.Begin();
            buttonPauseResume.Content = "❚❚";
            buttonPauseResume.IsEnabled = false;
            buttonCancel.IsEnabled = false;
            buttonOpenFile.IsEnabled = true;
            buttonOpenStream.IsEnabled = true;
            buttonConvert.IsEnabled = true;
            buttonPreview.IsEnabled = true;
            checkBoxCrop.IsEnabled = true;
            checkBoxCut.IsEnabled = true;
            gridSourceMedia.IsEnabled = true;
            Title = "AVC to HEVC Converter";
        }

        private void ButtonOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(currentOutputPath);
        }

        #endregion

        #region Media player controls

        private void MediaElementInput_PositionChanged(object sender, Unosquare.FFME.Common.PositionChangedEventArgs e)
        {
            if (!isSeeking)
            {
                userInput = false;
                if (checkBoxCut.IsChecked == true && e.Position.TotalSeconds > rangeSliderCut.UpperValue)
                {
                    mediaElementInput.Seek(TimeSpan.FromSeconds(rangeSliderCut.LowerValue));
                }
                else rangeSliderCut.MiddleValue = e.Position.TotalSeconds;
                userInput = true;
            }
        }

        private void MediaElementInput_MouseEnter(object sender, MouseEventArgs e)
        {
            if (mediaElementInput.Source != null)
            {
                Storyboard storyboardIn = FindResource("mediaControlsAnimationIn") as Storyboard;
                Storyboard.SetTarget(storyboardIn, gridSourceControls);
                storyboardIn.Begin();
            }
        }

        private void MediaElementInput_MouseLeave(object sender, MouseEventArgs e)
        {
            if (mediaElementInput.Source != null)
            {
                Storyboard storyboardIn = FindResource("mediaControlsAnimationOut") as Storyboard;
                Storyboard.SetTarget(storyboardIn, gridSourceControls);
                storyboardIn.Begin();
            }
        }

        private void ButtonPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (buttonPlayPause.Content.ToString() == " ▶️")
            {
                mediaElementInput.Play();
                buttonPlayPause.Content = " ❚❚";
            }
            else
            {
                mediaElementInput.Pause();
                buttonPlayPause.Content = " ▶️";
            }
        }

        private void SliderSourcePosition_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isSeeking = false;
            if (wasPlaying) mediaElementInput.Play();
        }

        private void SliderSourcePosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userInput) mediaElementInput.Seek(TimeSpan.FromSeconds(rangeSliderCut.MiddleValue));
        }

        private void SliderSourcePosition_DragStarted(object sender, DragStartedEventArgs e)
        {
            isSeeking = true;
            wasPlaying = mediaElementInput.IsPlaying;
            mediaElementInput.Pause();
        }

        private void ButtonExpand_Click(object sender, RoutedEventArgs e)
        {
            if (isPlayerExpanded)
            {
                Storyboard storyboardIn = FindResource("ExpandMediaPlayerRev") as Storyboard;
                storyboardIn.Begin();
                isPlayerExpanded = false;
            }
            else
            {
                Storyboard storyboardIn = FindResource("ExpandMediaPlayer") as Storyboard;
                storyboardIn.Begin();
                isPlayerExpanded = true;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MouseHitLocation = SetHitType(rectangleCropVideo, Mouse.GetPosition(canvasCropVideo));
            SetMouseCursor();
            if (MouseHitLocation == HitLocation.None) return;

            LastPoint = Mouse.GetPosition(canvasCropVideo);
            isDragging = true;
            Storyboard storyboardIn = FindResource("mediaControlsAnimationOut") as Storyboard;
            Storyboard.SetTarget(storyboardIn, gridSourceControls);
            storyboardIn.Completed += (s, _e) => { gridSourceControls.IsHitTestVisible = false; };
            if (gridSourceControls.Opacity == 1) storyboardIn.Begin();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                // See how much the mouse has moved.
                Point point = Mouse.GetPosition(canvasCropVideo);
                double offset_x = point.X - LastPoint.X;
                double offset_y = point.Y - LastPoint.Y;

                // Get the rectangle's current position.
                double new_x = Canvas.GetLeft(rectangleCropVideo);
                double new_y = Canvas.GetTop(rectangleCropVideo);
                double new_width = rectangleCropVideo.Width;
                double new_height = rectangleCropVideo.Height;

                // Update the rectangle.
                switch (MouseHitLocation)
                {
                    case HitLocation.Body:
                        new_x += offset_x;
                        new_y += offset_y;
                        break;
                    case HitLocation.UpperLeft:
                        new_x += offset_x;
                        new_y += offset_y;
                        new_width -= offset_x;
                        new_height -= offset_y;
                        break;
                    case HitLocation.UpperRight:
                        new_y += offset_y;
                        new_width += offset_x;
                        new_height -= offset_y;
                        break;
                    case HitLocation.LowerRight:
                        new_width += offset_x;
                        new_height += offset_y;
                        break;
                    case HitLocation.LowerLeft:
                        new_x += offset_x;
                        new_width -= offset_x;
                        new_height += offset_y;
                        break;
                    case HitLocation.Left:
                        new_x += offset_x;
                        new_width -= offset_x;
                        break;
                    case HitLocation.Right:
                        new_width += offset_x;
                        break;
                    case HitLocation.Bottom:
                        new_height += offset_y;
                        break;
                    case HitLocation.Top:
                        new_y += offset_y;
                        new_height -= offset_y;
                        break;
                }

                // Keep a minimun size for the rectangle and keep the rectangle inside the canvas
                if (new_x < 0) new_x = 0;
                if (new_y < 0) new_y = 0;
                if (new_width + new_x > canvasCropVideo.ActualWidth)
                {
                    if (MouseHitLocation == HitLocation.Body)
                        new_x = canvasCropVideo.ActualWidth - new_width;
                    else new_width = canvasCropVideo.ActualWidth - new_x;
                }
                if (new_height + new_y > canvasCropVideo.ActualHeight)
                {
                    if (MouseHitLocation == HitLocation.Body)
                        new_y= canvasCropVideo.ActualHeight - new_height;
                    else new_height = canvasCropVideo.ActualHeight - new_y;
                }
                if (new_width < RECT_MIN_SIZE)
                {
                    if (MouseHitLocation == HitLocation.Left)
                    {
                        new_x -= offset_x;
                        new_width += offset_x;
                    }
                    else new_width = RECT_MIN_SIZE;
                }
                if (new_height < RECT_MIN_SIZE)
                {
                    if(MouseHitLocation == HitLocation.Top)
                    {
                        new_y -= offset_y;
                        new_height += offset_y;
                    }
                    else new_height = RECT_MIN_SIZE;
                }

                // Update the rectangle.
                Canvas.SetLeft(rectangleCropVideo, new_x);
                Canvas.SetTop(rectangleCropVideo, new_y);
                rectangleCropVideo.Width = new_width;
                rectangleCropVideo.Height = new_height;

                //Update the integer textboxes
                double cropTop = new_y * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropLeft = new_x * mediaInfo.Width / canvasCropVideo.ActualWidth;
                double cropBottom = (canvasCropVideo.ActualHeight - new_height - new_y) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropRight = (canvasCropVideo.ActualWidth - new_width - new_x) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                integerTextBoxCropTop.Value = (int)cropTop;
                integerTextBoxCropLeft.Value = (int)cropLeft;
                integerTextBoxCropBottom.Value = (int)cropBottom;
                integerTextBoxCropRight.Value = (int)cropRight;
                textBlockOutputResolution.Text = $"{(mediaInfo.Width - cropLeft - cropRight).ToString("0")}x{(mediaInfo.Height - cropTop - cropBottom).ToString("0")}";

                // Save the mouse's new location.
                LastPoint = point;
            }
            else
            {
                MouseHitLocation = SetHitType(rectangleCropVideo,
                    Mouse.GetPosition(canvasCropVideo));
                SetMouseCursor();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            gridSourceControls.IsHitTestVisible = true;
            Storyboard storyboardIn = FindResource("mediaControlsAnimationIn") as Storyboard;
            Storyboard.SetTarget(storyboardIn, gridSourceControls);
            storyboardIn.Completed += (s, _e) => { gridSourceControls.IsHitTestVisible = true; }; 
            if (gridSourceControls.Opacity == 0) storyboardIn.Begin();
            Cursor = Cursors.Arrow;
        }

        private void CanvasCropVideo_MouseLeave(object sender, MouseEventArgs e)
        {
            Canvas_MouseUp(null, null);
        }

        private void SetMouseCursor()
        {
            // See what cursor we should display.
            Cursor desired_cursor = Cursors.Arrow;
            switch (MouseHitLocation)
            {
                case HitLocation.None:
                    desired_cursor = Cursors.Arrow;
                    break;
                case HitLocation.Body:
                    desired_cursor = Cursors.SizeAll;
                    break;
                case HitLocation.UpperLeft:
                case HitLocation.LowerRight:
                    desired_cursor = Cursors.SizeNWSE;
                    break;
                case HitLocation.LowerLeft:
                case HitLocation.UpperRight:
                    desired_cursor = Cursors.SizeNESW;
                    break;
                case HitLocation.Top:
                case HitLocation.Bottom:
                    desired_cursor = Cursors.SizeNS;
                    break;
                case HitLocation.Left:
                case HitLocation.Right:
                    desired_cursor = Cursors.SizeWE;
                    break;
            }

            // Display the desired cursor.
            if (Cursor != desired_cursor) Cursor = desired_cursor;
        }

        private void CanvasCropVideo_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width != 0)
            {
                Canvas.SetLeft(rectangleCropVideo, e.NewSize.Width * Canvas.GetLeft(rectangleCropVideo) / e.PreviousSize.Width);
                Canvas.SetTop(rectangleCropVideo, e.NewSize.Height * Canvas.GetTop(rectangleCropVideo) / e.PreviousSize.Height);
                rectangleCropVideo.Width = e.NewSize.Width * rectangleCropVideo.Width / e.PreviousSize.Width;
                rectangleCropVideo.Height = e.NewSize.Height * rectangleCropVideo.Height / e.PreviousSize.Height;
            }
        }

        private void RangeSliderCut_UpperSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userInput) textBoxEnd.Text = TimeSpan.FromSeconds(rangeSliderCut.UpperValue).ToString(@"hh\:mm\:ss\.ff");
        }

        private void RangeSliderCut_LowerSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (userInput) textBoxStart.Text = TimeSpan.FromSeconds(rangeSliderCut.LowerValue).ToString(@"hh\:mm\:ss\.ff");
        }

        #endregion

        #region Helper methods

        public static string GetBytesReadable(long i)
        {
            string suffix;
            double readable;

            if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }

        public static byte GetCRFFromQuality(string quality, Encoder encoder)
        {
            int crf = 18 + Array.IndexOf(QUALITY, quality) * 4;
            return (byte)(encoder == Encoder.H264 ? crf : crf + 5);
        }

        public static Resolution GetResolutionFromString(string resolution)
        {
            string[] numbers = resolution.Split(' ')[0].Split('x');
            return new Resolution(Convert.ToInt16(numbers[0]), Convert.ToInt16(numbers[1]));
        }

        private HitLocation SetHitType(System.Windows.Shapes.Rectangle rect, Point point)
        {
            const double GAP = 10;
            double left = Canvas.GetLeft(rect);
            double top = Canvas.GetTop(rect);
            double right = left + rect.Width;
            double bottom = top + rect.Height;

            if (point.X < left || point.X > right || point.Y < top || point.Y > bottom) return HitLocation.None;
            if (point.X - left < GAP)
            {
                // Left edge.
                if (point.Y - top < GAP) return HitLocation.UpperLeft;
                if (bottom - point.Y < GAP) return HitLocation.LowerLeft;
                return HitLocation.Left;
            }
            else if (right - point.X < GAP)
            {
                // Right edge.
                if (point.Y - top < GAP) return HitLocation.UpperRight;
                if (bottom - point.Y < GAP) return HitLocation.LowerRight;
                return HitLocation.Right;
            }
            if (point.Y - top < GAP) return HitLocation.Top;
            if (bottom - point.Y < GAP) return HitLocation.Bottom;
            return HitLocation.Body;
        }

        #endregion

    }
}