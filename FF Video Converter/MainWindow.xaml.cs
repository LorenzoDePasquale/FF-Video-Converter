using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
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
        private enum HitLocation
        {
            None, Body, UpperLeft, UpperRight, LowerRight, LowerLeft, Left, Right, Top, Bottom
        };

        private static readonly string[] SUPPORTED_EXTENSIONS = { ".mkv", ".mp4", ".m4v", ".avi", ".webm" };
        private readonly FFmpegEngine ffmpegEngine;
        private readonly QueueWindow queueWindow;
        private readonly CompletedWindow completedWindow;
        private readonly ObservableCollection<Job> queuedJobs = new ObservableCollection<Job>();
        private readonly ObservableCollection<Job> completedJobs = new ObservableCollection<Job>();
        private const int RECT_MIN_SIZE = 20;
        private MediaInfo mediaInfo;
        private Unosquare.FFME.Common.MediaOptions mediaOptions;
        private bool isSeeking = false;
        private bool sliderUserInput = true;
        private bool textBoxStartUserInput = true;
        private bool wasPlaying = false;
        private bool isPlayerExpanded = false;
        private bool isMediaOpen = false;
        private bool isDragging = false;
        private Point LastPoint;
        private HitLocation MouseHitLocation = HitLocation.None;
        private Job runningJob;


        public MainWindow()
        {
            InitializeComponent();

            TaskbarItemInfo = new TaskbarItemInfo();
            Height -= 30; //To compensate for hiding the window chrome

            //Setup internal player
            gridSourceControls.Visibility = Visibility.Collapsed;
            mediaElement.PositionChanged += MediaElementInput_PositionChanged;
            mediaElement.MediaOpening += (sender, e) =>
            {
                e.Options.IsSubtitleDisabled = true; 
                mediaOptions = e.Options;
            };

            //Setup comboboxes
            comboBoxEncoder.Items.Add(new NativeEncoder());
            comboBoxEncoder.Items.Add(new H264Encoder());
            comboBoxEncoder.Items.Add(new H265Encoder());
            if (H264Nvenc.IsAvaiable())
            {
                comboBoxEncoder.Items.Add(new H264Nvenc());
                comboBoxEncoder.Items.Add(new H265Nvenc());
            }
            if (H264QuickSync.IsAvaiable())
            {
                comboBoxEncoder.Items.Add(new H264QuickSync());
                comboBoxEncoder.Items.Add(new H265QuickSync());
            }
            comboBoxEncoder.SelectedIndex = 1;
            comboBoxRotation.Items.Add("Don't rotate");
            comboBoxRotation.Items.Add("Horizontal flip");
            comboBoxRotation.Items.Add("90° clockwise");
            comboBoxRotation.Items.Add("90° clockwise and flip");
            comboBoxRotation.Items.Add("180°");
            comboBoxRotation.Items.Add("180° and flip");
            comboBoxRotation.Items.Add("270° clockwise");
            comboBoxRotation.Items.Add("270° clockwise and flip");
            comboBoxRotation.SelectedIndex = 0;
            checkBoxCrop.IsEnabled = false;
            comboBoxFramerate.Items.Add("Same as source");
            comboBoxFramerate.SelectedIndex = 0;
            foreach (Preset preset in Enum.GetValues(typeof(Preset)))
            {
                comboBoxPreset.Items.Add(preset.GetName());
            }
            comboBoxPreset.SelectedIndex = 2;
            foreach (Quality quality in Enum.GetValues(typeof(Quality)))
            {
                comboBoxQuality.Items.Add(quality.GetName());
            }
            comboBoxQuality.SelectedIndex = 2;
            comboBoxResolution.Items.Add("Same as source");
            comboBoxResolution.SelectedIndex = 0;

            //Create queue and completed windows
            queueWindow = new QueueWindow(this, queuedJobs);
            queueWindow.QueueStarted += () =>
            {
                //If there are no jobs running, start the next one
                if (runningJob == null && queuedJobs.Count > 0)
                {
                    RunJob(queuedJobs[0]);
                    queuedJobs.RemoveAt(0);
                }
            };
            completedWindow = new CompletedWindow(this, completedJobs);
            queuedJobs.CollectionChanged += (s, e) =>
            {
                buttonShowQueue.Content = $"Show queue ({queuedJobs.Count})";
            };
            completedJobs.CollectionChanged += (s, e) =>
            {
                buttonOpenOutput.Content = $"Show completed ({completedJobs.Count})";
            };

            //Setup ffmpeg
            Unosquare.FFME.Library.FFmpegDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ffmpegEngine = new FFmpegEngine();
            ffmpegEngine.ProgressChanged += UpdateProgress;
            ffmpegEngine.ConversionCompleted += ConversionCompleted;

            //Remove old version, if it exists
            if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "update"))
            {
                Directory.Delete(AppDomain.CurrentDomain.BaseDirectory + "update", true);
            }
            if (File.Exists("FFVideoConverterOld.exe"))
            {
                File.Delete("FFVideoConverterOld.exe");
            }
            if (File.Exists("Update.zip"))
            {
                File.Delete("Update.zip");
            }
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
                    try
                    {
                        mediaInfo = await MediaInfo.Open(sourcePath);
                        OpenSource();
                    }
                    catch (Exception ex)
                    {
                        new MessageBoxWindow(ex.Message, "Error opening file").ShowDialog();
                    }
                }
            }

            if (await UpdaterWindow.UpdateAvaiable())
            {
                buttonUpdate.Visibility = Visibility.Visible;
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
            }
            else
            {
                textBoxDestination.Text = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{(String.IsNullOrEmpty(mediaInfo.Title) ? "stream" : mediaInfo.Title)}.mp4";
                labelTitle.Content = !String.IsNullOrEmpty(mediaInfo.Title) ? mediaInfo.Title : "[Network stream]";
            }

            mediaElement.Open(new Uri(sourcePath));
            mediaElement.Background = Brushes.Black;
            borderSource.BorderThickness = new Thickness(0);

            textBlockDuration.Text = mediaInfo.Duration.ToFormattedString(true);
            textBlockCodec.Text = $"{mediaInfo.Codec} / {mediaInfo.AudioCodec}";
            double fps = Math.Round(mediaInfo.Framerate, 2);
            textBlockFramerate.Text = !Double.IsNaN(fps) ? fps + " fps" : "-";
            textBlockBitrate.Text = mediaInfo.Bitrate + " Kbps";
            textBlockResolution.Text = mediaInfo.Resolution.HasValue() ? mediaInfo.Resolution.ToString() : "-";
            textBlockAspectRatio.Text = mediaInfo.Resolution.AspectRatio.ToString();
            textBlockInputSize.Text = mediaInfo.Size.ToBytesString();
            if (!String.IsNullOrEmpty(mediaInfo.AudioSource))
            {
                textBlockInputSize.Text += " (video only)";
            }

            checkBoxCrop.IsEnabled = true;
            checkBoxCrop.IsChecked = false;
            checkBoxCut.IsEnabled = true;
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
            SetComboBoxResolution(mediaInfo.Resolution);      

            buttonConvert.IsEnabled = true;
            buttonPreview.IsEnabled = true;
            buttonAddToQueue.IsEnabled = true;
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

        private void SetComboBoxResolution(Resolution r)
        {
            comboBoxResolution.Items.Clear();
            comboBoxResolution.Items.Add("Same as source");
            if (r.AspectRatio.IntegerName == "16:9")
            {
                if (r.Height > 2160)
                    comboBoxResolution.Items.Add("3840×2160");
                if (r.Height > 1440)
                    comboBoxResolution.Items.Add("2560x1440");
                if (r.Height > 1080)
                    comboBoxResolution.Items.Add("1920x1080");
                if (r.Height > 900)
                    comboBoxResolution.Items.Add("1600x900");
                if (r.Height > 720)
                    comboBoxResolution.Items.Add("1280x720");
                if (r.Height > 540)
                    comboBoxResolution.Items.Add("960x540");
                if (r.Height > 480)
                    comboBoxResolution.Items.Add("854x480");
                if (r.Height > 360)
                    comboBoxResolution.Items.Add("640x360");
            }
            else if (r.AspectRatio.IntegerName == "4:3")
            {
                if (r.Height > 2100)
                    comboBoxResolution.Items.Add("2800×2100");
                if (r.Height > 1536)
                    comboBoxResolution.Items.Add("2048x1536");
                if (r.Height > 1200)
                    comboBoxResolution.Items.Add("1600x1200");
                if (r.Height > 960)
                    comboBoxResolution.Items.Add("1280x960");
                if (r.Height > 768)
                    comboBoxResolution.Items.Add("1024x768");
                if (r.Height > 600)
                    comboBoxResolution.Items.Add("800x600");
                if (r.Height > 480)
                    comboBoxResolution.Items.Add("640x480");
            }
            else if (r.AspectRatio.IntegerName == "2:1")
            {
                if (r.Height > 2048)
                    comboBoxResolution.Items.Add("4096x2048");
                if (r.Height > 1440)
                    comboBoxResolution.Items.Add("2880x1440");
                if (r.Height > 1080)
                    comboBoxResolution.Items.Add("2160x1080");
                if (r.Height > 960)
                    comboBoxResolution.Items.Add("1920x960");
                if (r.Height > 720)
                    comboBoxResolution.Items.Add("1440x720"); 
                if (r.Height > 640)
                    comboBoxResolution.Items.Add("1280x640");
                if (r.Height > 320)
                    comboBoxResolution.Items.Add("640x320");
            }
            else
            {
                foreach (int width in new int[] { 3840, 2560, 1920, 1600, 1280, 960, 640})
                {
                    int height = width * r.AspectRatio.Heigth / r.AspectRatio.Width;
                    if (r.Height > height)
                        comboBoxResolution.Items.Add($"{width}x{height}");
                }
            }
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
                try
                {
                    mediaInfo = await MediaInfo.Open(ofd.FileName);
                    OpenSource();
                }
                catch (Exception ex)
                {
                    new MessageBoxWindow(ex.Message, "Error opening file").ShowDialog();
                }
            }
        }

        private void Rectangle_DragEnter(object sender, DragEventArgs e)
        {
            PlayStoryboard("DragOverAnimation");
        }

        private void Rectangle_DragLeave(object sender, DragEventArgs e)
        {
            StopStoryboard("DragOverAnimation");
        }

        private async void Rectangle_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                string extension = Path.GetExtension(paths[0]);
                if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                {
                    try
                    {
                        mediaInfo = await MediaInfo.Open(paths[0]);
                        OpenSource();
                    }
                    catch (Exception ex)
                    {
                        new MessageBoxWindow(ex.Message, "Error opening file").ShowDialog();
                    }
                }
                else
                {
                    new MessageBoxWindow($"This file type ({extension}) is not supported", "Error opening file").ShowDialog();
                }
            }
            StopStoryboard("DragOverAnimation");
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
            Close();
        }

        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            new UpdaterWindow().ShowDialog();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Make sure the ffmpeg process is not running
            ffmpegEngine.StopConversion();
            //Terminates the application (this call is necessary because other opened windows would keep the application running)
            Application.Current.Shutdown();
        }

        #endregion

        #region Conversion settings

        private void ComboBoxEncoder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Encoder selectedEncoder = (Encoder)comboBoxEncoder.SelectedItem;
            if (selectedEncoder is NativeEncoder)
            {
                comboBoxPreset.IsEnabled = false;
                comboBoxQuality.IsEnabled = false;
                comboBoxFramerate.IsEnabled = false;
                checkBoxCrop.IsChecked = false;
                checkBoxCrop.IsEnabled = false;
                comboBoxResolution.IsEnabled = false;
                comboBoxRotation.IsEnabled = false;
                checkBoxCut.Content = "Fast cut";
                checkBoxCut.ToolTip = "Cut the video without re-encoding it. Because no encoding is performed, every other option is ignored.\nAlthough the video can be cut anywhere, cutting at the suggested start positions will provide the most accurate result";
                if (checkBoxCut.IsChecked == true)
                {
                    textBlockStartBefore.Visibility = Visibility.Visible;
                    textBlockStartAfter.Visibility = Visibility.Visible;
                    buttonConvert.Content = "Fast cut";
                }
                else
                {
                    if (mediaInfo != null && !mediaInfo.IsLocal)
                    {
                        buttonConvert.Content = "Download";
                    }
                    else
                    {
                        buttonConvert.Content = "Do nothing"; // ¯\_(ツ)_/¯
                    }
                }
                TextBoxStart_TextChanged(null, null);
                PlayStoryboard("PreviewButtonAnimationOut");
            }
            else
            {
                comboBoxPreset.IsEnabled = !(selectedEncoder is H264Nvenc || selectedEncoder is H265Nvenc);
                comboBoxQuality.IsEnabled = true;
                comboBoxFramerate.IsEnabled = true;
                comboBoxResolution.IsEnabled = true;
                comboBoxRotation.IsEnabled = true;
                checkBoxCrop.IsEnabled = true;
                checkBoxCut.Content = "Cut";
                checkBoxCut.ToolTip = "Cut the video and re-encode it with the selected encoder";
                textBlockStartBefore.Visibility = Visibility.Hidden;
                textBlockStartAfter.Visibility = Visibility.Hidden;
                buttonConvert.Content = "Convert";
                textBoxStart.ClearValue(TextBox.ForegroundProperty);
                PlayStoryboard("PreviewButtonAnimationIn");
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (mediaInfo != null)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Select the destination file";
                sfd.Filter = "MKV|*.mkv|MP4|*.mp4";
                sfd.FileName = Path.GetFileNameWithoutExtension(mediaInfo.Source) + "_converted";
                string extension = mediaInfo.Source.Substring(mediaInfo.Source.LastIndexOf('.'));
                if (extension == ".mp4") sfd.FilterIndex = 2;
                bool? result = sfd.ShowDialog();
                if (result == true)
                {
                    textBoxDestination.Text = sfd.FileName;
                }
            }
        }

        private void ButtonPreview_Click(object sender, RoutedEventArgs e)
        {
            if (isMediaOpen && mediaInfo.IsLocal)
            {
                if (comboBoxEncoder.SelectedIndex != 0)
                {
                    new ComparisonWindow(mediaInfo, (Encoder)comboBoxEncoder.SelectedItem).ShowDialog();
                }
                else
                {
                    new MessageBoxWindow("Selected encoder should be different than Native.", "FF Video Converter").ShowDialog();
                }
            }
        }

        private void TextBoxStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isMediaOpen && TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start) && TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
            {
                if ((end - start).TotalSeconds > 2)
                {
                    textBlockOutputDuration.Text = $"Duration: {(end - start).ToFormattedString()}";
                    sliderUserInput = false;
                    rangeSliderCut.LowerValue = start.TotalSeconds;
                    sliderUserInput = true;
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() => textBoxStart.Undo()));
                }

                if (comboBoxEncoder.SelectedIndex == 0 && checkBoxCut.IsChecked == true)
                {
                    textBoxStart.ClearValue(TextBox.ForegroundProperty);
                    if (textBoxStartUserInput) UpdateKeyFrameSuggestions(start);
                }
            }            
        }

        private void TextBoxEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isMediaOpen && TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start) && TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
            {
                if ((end - start).TotalSeconds > 2 && end <= mediaInfo.Duration)
                {
                    textBlockOutputDuration.Text = $"Duration: {(end - start).ToFormattedString()}";
                    sliderUserInput = false;
                    rangeSliderCut.UpperValue = end.TotalSeconds;
                    sliderUserInput = true;
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

        private async void UpdateKeyFrameSuggestions(TimeSpan t)
        {
            textBlockStartBefore.Text = "...";
            textBlockStartAfter.Text = "...";
            var (before, after, isKeyFrame) = await mediaInfo.GetNearestBeforeAndAfterKeyFrames(t.TotalSeconds);
            t = TimeSpan.FromSeconds(before);
            textBlockStartBefore.Text = t.ToFormattedString(true);
            t = TimeSpan.FromSeconds(after);
            textBlockStartAfter.Text = t.ToFormattedString(true);
            textBoxStart.Foreground = new BrushConverter().ConvertFromString(isKeyFrame ? "#FF109320" : "#FFC12222") as SolidColorBrush;
        }

        private void CheckBoxCut_Click(object sender, RoutedEventArgs e)
        {
            if (checkBoxCut.IsChecked == true)
            {
                rangeSliderCut.RangeSelectorVisibility = Visibility.Visible;
                TimeSpan start = TimeSpan.Parse(textBoxStart.Text);
                TimeSpan end = TimeSpan.Parse(textBoxEnd.Text);
                textBlockOutputDuration.Text = $"Duration: {(end - start).ToFormattedString()}";

                if (comboBoxEncoder.SelectedIndex == 0)
                {
                    buttonConvert.Content = "Fast cut";
                    textBlockStartBefore.Visibility = Visibility.Visible;
                    textBlockStartAfter.Visibility = Visibility.Visible;
                    TextBoxStart_TextChanged(null, null);
                }
            }
            else
            {
                rangeSliderCut.RangeSelectorVisibility = Visibility.Hidden;
                textBlockStartBefore.Visibility = Visibility.Hidden;
                textBlockStartAfter.Visibility = Visibility.Hidden;
                textBoxStart.ClearValue(TextBox.ForegroundProperty);
                if (comboBoxEncoder.SelectedIndex == 0)
                {
                    if (mediaInfo.IsLocal)
                    {
                        buttonConvert.Content = "Do nothing"; // ¯\_(ツ)_ /¯
                    }
                    else
                    {
                        buttonConvert.Content = "Download";
                    }
                }
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

        private void ComboBoxRotation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mediaOptions != null)
            {
                mediaOptions.VideoFilter = ((Rotation)comboBoxRotation.SelectedIndex).FilterString;
            }
        }

        #endregion

        #region Conversion process

        private void ButtonConvert_Click(object sender, RoutedEventArgs e)
        {
            if (CheckForErrors()) return;

            string senderName = ((Button)sender).Name.ToString();
            Encoder encoder = (Encoder)comboBoxEncoder.SelectedItem;
            encoder.Preset = (Preset)comboBoxPreset.SelectedIndex;
            encoder.Quality = (Quality)comboBoxQuality.SelectedIndex;
            ConversionOptions conversionOptions = new ConversionOptions(encoder);

            if (!(encoder is NativeEncoder))
            {
                if (checkBoxCrop.IsChecked == true)
                {
                    conversionOptions.CropData = new CropData((short)integerTextBoxCropLeft.Value, (short)integerTextBoxCropTop.Value, (short)integerTextBoxCropRight.Value, (short)integerTextBoxCropBottom.Value);
                }
                else if (comboBoxResolution.SelectedIndex != 0)
                {
                    conversionOptions.Resolution = Resolution.FromString(comboBoxResolution.Text);
                }
                if (comboBoxFramerate.SelectedIndex != 0)
                {
                    conversionOptions.Framerate = Convert.ToByte(comboBoxFramerate.SelectedItem);
                }
                conversionOptions.Rotation = new Rotation(comboBoxRotation.SelectedIndex);

                textBlockProgress.Text = "Starting conversion process...";
            }
            else
            {
                textBlockProgress.Text = checkBoxCut.IsChecked == true ? "Starting cutting process..." : "Starting download...";
            }

            if (checkBoxCut.IsChecked == true)
            {
                if (!TimeSpan.TryParse(textBoxStart.Text, out TimeSpan start))
                {
                    new MessageBoxWindow("Enter a valid start time", "FF Video Converter").ShowDialog();
                    return;
                }
                if (!TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan end))
                {
                    new MessageBoxWindow("Enter a valid end time", "FF Video Converter").ShowDialog();
                    return;
                }

                conversionOptions.Start = start;
                conversionOptions.End = end;
            }

            if (senderName == "buttonConvert" || (queueWindow.QueueActive && runningJob == null)) //If the queue is started but there are no conversion running, run this one directly instead of adding it to the queue
            {
                RunJob(new Job(mediaInfo, textBoxDestination.Text, conversionOptions));
            }
            else
            {
                queuedJobs.Add(new Job(mediaInfo, textBoxDestination.Text, conversionOptions));
                textBlockProgress.Text = "Added to queue";
            }
        }

        private bool CheckForErrors()
        {
            if (buttonConvert.Content.ToString() == "Do nothing")
            {
                new MessageBoxWindow(@"¯\_(ツ)_/¯", "FF Video Converter").ShowDialog();
                return true;
            }
            if (textBoxDestination.Text.EndsWith("mp4") && mediaInfo.AudioCodec?.ToLower() == "opus")
            {
                new MessageBoxWindow("Opus audio in mp4 container is currently unsupported.\nEither use aac audio or mkv container.", "FF Video Converter").ShowDialog();
                return true;
            }
            if (mediaInfo.Source.Equals(textBoxDestination.Text))
            {
                new MessageBoxWindow("Source and destination file are the same.\nSelect a different file name.", "FF Video Converter").ShowDialog();
                return true;
            }
            if (!textBoxDestination.Text.EndsWith(".mp4") && !textBoxDestination.Text.EndsWith(".mkv"))
            {
                new MessageBoxWindow("Wrong output file format.\nSelect either mp4 or mkv as output extension.", "FF Video Converter").ShowDialog();
                return true;
            }
            try //Check if the destination path is a valid path
            {
                File.Create(textBoxDestination.Text).Dispose();
                File.Delete(textBoxDestination.Text); //Delete in case file is never converted
            }
            catch (Exception)
            {
                new MessageBoxWindow("Error creating output file at the selected destination path.\nMake sure the destination path is valid and that the file is not beign written to by another process", "FF Video Converter").ShowDialog();
                return true;
            }

            return false;
        }

        private async void RunJob(Job job)
        {
            runningJob = job;
            ffmpegEngine.Convert(job.SourceInfo, job.Destination, job.ConversionOptions);

            buttonPauseResume.IsEnabled = true;
            buttonCancel.IsEnabled = true;
            buttonConvert.IsEnabled = false;
            buttonPreview.IsEnabled = false;
            await mediaElement.Pause();
            buttonPlayPause.Content = " ▶️";
            PlayStoryboard("ProgressAnimationIn");
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            BlockSleepMode();
        }

        private void UpdateProgress(ProgressData progressData)
        {
            double secondsToEncode = progressData.TotalTime.TotalSeconds - progressData.CurrentTime.TotalSeconds;
            double remainingTime = progressData.EncodingSpeed == 0 ? 0 : (secondsToEncode) / progressData.EncodingSpeed;
            double percentage = Math.Min(progressData.CurrentFrame * 100 / (runningJob.OutputFramerate * progressData.TotalTime.TotalSeconds), 99.4);
            long approximateOutputByteSize = progressData.CurrentByteSize + (long)(progressData.AverageBitrate * 1000 * secondsToEncode / 8);

            Dispatcher.Invoke(() =>
            {
                DoubleAnimation progressAnimation = new DoubleAnimation(percentage, TimeSpan.FromSeconds(0.5));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                TaskbarItemInfo.ProgressValue = percentage / 100;
                if (runningJob.JobType != JobType.Download)
                {
                    textBlockProgress.Text = $"Processed: {progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
                    if (mediaInfo.IsLocal) textBlockProgress.Text += $"  @ {progressData.EncodingSpeed}x speed";
                    textBlockSize.Text = $"Output size: {progressData.CurrentByteSize.ToBytesString()}";
                    if (progressData.AverageBitrate > 0) textBlockSize.Text += $" / {approximateOutputByteSize.ToBytesString()} (estimated)";
                }
                else
                {
                    textBlockProgress.Text = $"Progress: {progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
                    textBlockSize.Text = $"Downloaded: {progressData.CurrentByteSize.ToBytesString()}";
                }
                Title = Math.Floor(percentage) + "%   " + TimeSpan.FromSeconds(remainingTime).ToFormattedString();
                labelProgress.Content = $"Progress: {Math.Round(percentage)}%   Remaining time: {TimeSpan.FromSeconds(remainingTime).ToFormattedString()}";
            }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private async void ConversionCompleted(ProgressData progressData)
        {
            if (!String.IsNullOrEmpty(progressData.ErrorMessage))
            {
                if (progressData.CurrentFrame == 0) //Error while starting encoding process
                {
                    new MessageBoxWindow($"Error while starting the conversion process:\n\n\"{progressData.ErrorMessage}\"\n\nMake sure the selected encoder is compatible with your hardware configuration", "FF Video Converter").ShowDialog();
                }
                else //Error during encoding process
                {
                    new MessageBoxWindow($"The encoder reported the following error during the encoding process:\n\n\"{progressData.ErrorMessage}\"", "FF Video Converter").ShowDialog();
                }
                progressBarConvertProgress.Value = 0;
                textBlockProgress.Text = "Conversion failed!";
                runningJob.State = JobState.Failed;
                runningJob.ConversionResults.Add(new ConversioResult("Error", progressData.ErrorMessage));
            }
            else
            {
                DoubleAnimation progressAnimation = new DoubleAnimation(100, TimeSpan.FromSeconds(0));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                long outputSize = new FileInfo(runningJob.Destination).Length;
                if (runningJob.JobType == JobType.Conversion)
                {
                    textBlockProgress.Text = "Video converted!";
                    textBlockSize.Text = "Output size: " + outputSize.ToBytesString();
                }
                else if (runningJob.JobType == JobType.FastCut)
                {
                    textBlockProgress.Text = "Video cut!";
                    textBlockSize.Text = "Output size: " + outputSize.ToBytesString();
                }
                else
                {
                    textBlockProgress.Text = "Video downloaded!";
                    textBlockSize.Text = "Video size: " + outputSize.ToBytesString();
                }

                MediaInfo outputFile = await MediaInfo.Open(runningJob.Destination);
                int percentageDifference = 100 - (int)(outputSize / (float)runningJob.SourceInfo.Size * 100);
                string videoOnly = String.IsNullOrEmpty(runningJob.SourceInfo.AudioSource) ? "" : " (video only)";
                string biggerSmaller = percentageDifference >= 0 ? "smaller" : "bigger";
                if (runningJob.JobType == JobType.Download)
                {
                    runningJob.ConversionResults.Add(new ConversioResult("Duration", outputFile.Duration.ToFormattedString(true)));
                    runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{outputFile.Codec} / {outputFile.AudioCodec}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{outputFile.Framerate} fps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{outputFile.Bitrate} Kbps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Resolution", outputFile.Resolution.ToString()));
                    runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", outputFile.Resolution.AspectRatio.ToString()));
                    runningJob.ConversionResults.Add(new ConversioResult("Size", outputSize.ToBytesString()));
                }
                else
                {
                    runningJob.ConversionResults.Add(new ConversioResult("Duration", $"{runningJob.SourceInfo.Duration.ToFormattedString(true)}   ⟶   {outputFile.Duration.ToFormattedString(true)}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{runningJob.SourceInfo.Codec} / {runningJob.SourceInfo.AudioCodec}   ⟶   {outputFile.Codec} / {outputFile.AudioCodec}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{runningJob.SourceInfo.Framerate} fps   ⟶   {outputFile.Framerate} fps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{runningJob.SourceInfo.Bitrate} Kbps   ⟶   {outputFile.Bitrate} Kbps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Resolution", $"{runningJob.SourceInfo.Resolution}   ⟶   {outputFile.Resolution}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", $"{runningJob.SourceInfo.Resolution.AspectRatio}   ⟶   {outputFile.Resolution.AspectRatio}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Size", $"{runningJob.SourceInfo.Size.ToBytesString()}{videoOnly}   ⟶   {outputSize.ToBytesString()}  ({Math.Abs(percentageDifference)}% {biggerSmaller})"));
                }

                //Show conversion results compared to original values, only if the conversion was not a download and the loaded media is the same as the converted one
                if (runningJob.JobType != JobType.Download && mediaInfo.Source == runningJob.SourceInfo.Source)
                {
                    textBlockDuration.Text = $"{runningJob.SourceInfo.Duration.ToFormattedString(true)}   ⟶   {outputFile.Duration.ToFormattedString(true)}";
                    textBlockCodec.Text = $"{runningJob.SourceInfo.Codec} / {runningJob.SourceInfo.AudioCodec}   ⟶   {outputFile.Codec} / {outputFile.AudioCodec}";
                    textBlockFramerate.Text = $"{runningJob.SourceInfo.Framerate} fps   ⟶   {outputFile.Framerate} fps";
                    textBlockBitrate.Text = $"{runningJob.SourceInfo.Bitrate} Kbps   ⟶   {outputFile.Bitrate} Kbps";
                    textBlockResolution.Text = $"{runningJob.SourceInfo.Resolution}   ⟶   {outputFile.Resolution}";
                    textBlockAspectRatio.Text = $"{runningJob.SourceInfo.Resolution.AspectRatio}   ⟶   {outputFile.Resolution.AspectRatio}";
                    textBlockInputSize.Text = $"{runningJob.SourceInfo.Size.ToBytesString()}{videoOnly}   ⟶   {outputSize.ToBytesString()}  ({Math.Abs(percentageDifference)}% {biggerSmaller})";
                }

                runningJob.State = JobState.Completed;
            }

            //Complete this job and run the next one, if present
            completedJobs.Add(runningJob);
            OnConversionEnded();
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
                runningJob.State = JobState.Paused;
            }
            else
            {
                ffmpegEngine.ResumeConversion();
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
                progressBarConvertProgress.ClearValue(ForegroundProperty);
                buttonPauseResume.Content = "❚❚";
                buttonPauseResume.ToolTip = "Pause";
                runningJob.State = JobState.Running;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (new QuestionBoxWindow("Are you sure you want to cancel current process?", "Confirm cancellation").ShowDialog() == true)
            {
                ffmpegEngine.StopConversion();
                progressBarConvertProgress.ClearValue(ForegroundProperty);
                DoubleAnimation progressAnimation = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                textBlockProgress.Text = "Conversion canceled";
                textBlockSize.Text = "";
                buttonPauseResume.Content = "❚❚";
                
                runningJob.State = JobState.Canceled;
                runningJob.ConversionResults.Add(new ConversioResult("Duration", runningJob.SourceInfo.Duration.ToFormattedString(true)));
                runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{runningJob.SourceInfo.Codec} / {runningJob.SourceInfo.AudioCodec}"));
                runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{runningJob.SourceInfo.Framerate} fps"));
                runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{runningJob.SourceInfo.Bitrate} Kbps"));
                runningJob.ConversionResults.Add(new ConversioResult("Resolution", runningJob.SourceInfo.Resolution.ToString()));
                runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", runningJob.SourceInfo.Resolution.AspectRatio.ToString()));
                runningJob.ConversionResults.Add(new ConversioResult("Size", runningJob.SourceInfo.Size.ToBytesString()));
                completedJobs.Add(runningJob);

                OnConversionEnded();
            }
        }

        private void OnConversionEnded()
        {
            if (queueWindow.QueueActive)
            {
                //Run next job, if present
                if (queuedJobs.Count > 0)
                {
                    RunJob(queuedJobs[0]);
                    queuedJobs.RemoveAt(0);
                }
                else 
                {
                    runningJob = null;
                    AllowSleepMode();
                    Title = "AVC to HEVC Converter";
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    PlayStoryboard("ProgressAnimationOut");
                    buttonConvert.IsEnabled = true;
                    buttonPreview.IsEnabled = true;
                    buttonPauseResume.IsEnabled = false;
                    buttonCancel.IsEnabled = false;

                    if (queueWindow.QueueCompletedAction == QueueCompletedAction.Sleep)
                    {
                        SetSuspendState(false, true, true);
                    }
                    else if (queueWindow.QueueCompletedAction == QueueCompletedAction.Shutdown)
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("shutdown", "/s /t 0");
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;
                        System.Diagnostics.Process.Start(psi);
                    }
                }
            }
            else
            {
                runningJob = null;
                AllowSleepMode();
                Title = "AVC to HEVC Converter";
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                PlayStoryboard("ProgressAnimationOut");
                buttonConvert.IsEnabled = true;
                buttonPreview.IsEnabled = true;
                buttonPauseResume.IsEnabled = false;
                buttonCancel.IsEnabled = false;
            }

            buttonOpenOutput.Visibility = Visibility.Visible;
        }

        #endregion

        #region Media player controls

        private async void MediaElementInput_PositionChanged(object sender, Unosquare.FFME.Common.PositionChangedEventArgs e)
        {
            if (!isSeeking)
            {
                sliderUserInput = false;
                if (checkBoxCut.IsChecked == true && e.Position.TotalSeconds > rangeSliderCut.UpperValue && mediaElement.IsPlaying)
                {
                    await mediaElement.Pause();
                    await mediaElement.Seek(TimeSpan.FromSeconds(rangeSliderCut.LowerValue));
                    await mediaElement.Play();
                }
                else
                {
                    rangeSliderCut.MiddleValue = e.Position.TotalSeconds;
                    textBlockPlayerPosition.Text = $"{e.Position.ToFormattedString()} / {mediaElement.PlaybackEndTime.Value.ToFormattedString()}";
                }
                sliderUserInput = true;
            }
        }

        private void MediaElementInput_MouseEnter(object sender, MouseEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                PlayStoryboard("mediaControlsAnimationIn");
            }
        }

        private void MediaElementInput_MouseLeave(object sender, MouseEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                PlayStoryboard("mediaControlsAnimationOut");
            }
        }

        private async void ButtonPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.IsPaused)
            {
                buttonPlayPause.Content = " ❚❚";
                await mediaElement.Play();
            }
            else
            {
                buttonPlayPause.Content = " ▶️";
                await mediaElement.Pause();
            }
        }

        private async void SliderSourcePosition_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isSeeking = false;
            if (wasPlaying)
            {
                await mediaElement.Play();
            }
        }

        private void SliderSourcePosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderUserInput)
            {
                mediaElement.Seek(TimeSpan.FromSeconds(rangeSliderCut.MiddleValue));
            }
        }

        private void SliderSourcePosition_DragStarted(object sender, DragStartedEventArgs e)
        {
            isSeeking = true;
            wasPlaying = mediaElement.IsPlaying;
            mediaElement.Pause();
        }

        private void ButtonExpand_Click(object sender, RoutedEventArgs e)
        {
            if (isPlayerExpanded)
            {
                PlayStoryboard("ExpandMediaPlayerRev");
                isPlayerExpanded = false;
                if (comboBoxEncoder.SelectedIndex > 0) //If encoder is not native, animate preview button too
                {
                    PlayStoryboard("PreviewButtonAnimationIn");
                }
            }
            else
            {
                PlayStoryboard("ExpandMediaPlayer");
                isPlayerExpanded = true;
                if (comboBoxEncoder.SelectedIndex > 0) //If encoder is not native, animate preview button too
                {
                    PlayStoryboard("PreviewButtonAnimationOut");
                }
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
                MouseHitLocation = SetHitType(rectangleCropVideo, Mouse.GetPosition(canvasCropVideo));
                SetMouseCursor();
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            gridSourceControls.IsHitTestVisible = true;
            Storyboard storyboardIn = FindResource("mediaControlsAnimationIn") as Storyboard;
            storyboardIn.Completed += (s, _e) => { gridSourceControls.IsHitTestVisible = true; }; 
            if (gridSourceControls.Opacity == 0) storyboardIn.Begin();
            Cursor = Cursors.Arrow;
        }

        private void CanvasCropVideo_MouseLeave(object sender, MouseEventArgs e)
        {
            Canvas_MouseUp(null, null);
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
            if (sliderUserInput) textBoxEnd.Text = TimeSpan.FromSeconds(rangeSliderCut.UpperValue).ToFormattedString(true);
        }

        private void RangeSliderCut_LowerSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sliderUserInput)
            {
                textBoxStartUserInput = false;
                textBoxStart.Text = TimeSpan.FromSeconds(rangeSliderCut.LowerValue).ToFormattedString(true);
                textBoxStartUserInput = true;
            }
        }

        private void RangeSliderCut_LowerSliderDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (comboBoxEncoder.SelectedIndex == 0)
                UpdateKeyFrameSuggestions(TimeSpan.Parse(textBoxStart.Text));
        }

        private void ButtonMute_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.IsMuted)
            {
                mediaElement.IsMuted = false;
                buttonMute.Content = "🔊";
            }
            else
            {
                mediaElement.IsMuted = true;
                buttonMute.Content = "🔇";
            }
        }

        #endregion

        #region Queue

        private void ButtonShowQueue_Click(object sender, RoutedEventArgs e)
        {
            queueWindow.Show();
            queueWindow.Activate();
        }

        private void ButtonOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            completedWindow.Show();
            completedWindow.Activate();
        }

        public void OpenJob(Job job)
        {
            mediaInfo = job.SourceInfo;
            OpenSource();
            ConversionOptions options = job.ConversionOptions;
            comboBoxEncoder.SelectedValue = options.Encoder.ToString();
            comboBoxPreset.SelectedValue = options.Encoder.Preset.GetName();
            comboBoxQuality.SelectedValue = options.Encoder.Quality.GetName();
            if (options.Framerate > 0)
                comboBoxFramerate.SelectedValue = options.Framerate.ToString();
            else comboBoxFramerate.SelectedIndex = 0;
            if (options.Resolution.HasValue())
                comboBoxResolution.SelectedValue = options.Resolution.ToString();
            else comboBoxResolution.SelectedIndex = 0;
            comboBoxRotation.SelectedIndex = options.Rotation.RotationType;
            if (options.Start != TimeSpan.Zero || (options.End != TimeSpan.Zero && options.End != mediaInfo.Duration))
            {
                checkBoxCut.IsChecked = true;
                textBoxStart.Text = options.Start.ToFormattedString(true);
                textBoxEnd.Text = options.End.ToFormattedString(true);
            }
            if (options.CropData.HasValue()) //Removing the ValueChanged event is necessary to avoid the controls checking for errors on the single values and refusing them
            {
                integerTextBoxCropBottom.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropLeft.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropRight.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropTop.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropBottom.Value = options.CropData.Bottom;
                integerTextBoxCropLeft.Value = options.CropData.Left;
                integerTextBoxCropRight.Value = options.CropData.Right;
                integerTextBoxCropTop.Value = options.CropData.Top;
                integerTextBoxCropBottom.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropLeft.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropRight.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                integerTextBoxCropTop.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                checkBoxCrop.IsChecked = true;
            }
        }

        #endregion

        #region Helper methods

        private void PlayStoryboard(string storyboardName)
        {
            Storyboard storyboard = FindResource(storyboardName) as Storyboard;
            storyboard.Begin();
        }

        private void StopStoryboard(string storyboardName)
        {
            Storyboard storyboard = FindResource(storyboardName) as Storyboard;
            storyboard.Stop();
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

        private void BlockSleepMode()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }

        private void AllowSleepMode()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }

        #endregion

        #region Native methods

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040, //Should only be used by applications that must perform critical background processing while the computer appears to be sleeping. This value must be specified with ES_CONTINUOUS
            ES_CONTINUOUS = 0x80000000,        //Informs the system that the state being set should remain in effect until the next call that uses ES_CONTINUOUS and one of the other state flags is cleared
            ES_DISPLAY_REQUIRED = 0x00000002,  //Forces the display to be on by resetting the display idle timer
            ES_SYSTEM_REQUIRED = 0x00000001    //Forces the system to be in the working state by resetting the system idle timer.
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        #endregion
    }
}