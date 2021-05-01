using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using FFVideoConverter.Encoders;
using Microsoft.Win32;
using PixelFormat = FFVideoConverter.Encoders.PixelFormat;


namespace FFVideoConverter
{
    public partial class MainWindow : Window
    {
        static readonly string[] SUPPORTED_EXTENSIONS = { ".mkv", ".mp4", ".m4v", ".avi", ".webm", ".gif"};
        FFmpegEngine ffmpegEngine;
        QueueWindow queueWindow;
        CompletedWindow completedWindow;
        ObservableCollection<Job> queuedJobs = new ();
        ObservableCollection<Job> completedJobs = new ();
        PerformanceCounter avaiableMemoryCounter = new ();
        PerformanceCounter memoryCounter = new ();
        long totalMemory;
        const int RECT_MIN_SIZE = 20;
        MediaInfo mediaInfo;
        bool isPlayerExpanded = false;
        bool isMediaOpen = false;
        bool isDragging = false;
        Job runningJob;
        TimeIntervalCollection timeIntervalCollection;


        public MainWindow()
        {
            InitializeComponent();

            TaskbarItemInfo = new TaskbarItemInfo();
            Height -= 30; // To compensate for hiding the window chrome
        }

        #region Load

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Initialize counters (do not pass these arguments in the constructor because for some reason it is much slower)
            avaiableMemoryCounter.CategoryName = "Memory";
            avaiableMemoryCounter.CounterName = "Available MBytes";
            memoryCounter.CategoryName = "Process";
            memoryCounter.CounterName = "Working Set - Private";
            memoryCounter.InstanceName = "FFVideoConverter";

            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            labelTitle.Text += $" v{version}";
            
            // Setup internal player
            mediaPlayer.Click += MediaPlayer_Click;
            mediaPlayer.CropChanged += MediaPlayer_CropChanged;
            mediaPlayer.ButtonExpandClick += ButtonExpand_Click;

            // Create queue and completed windows
            queueWindow = new QueueWindow(this, queuedJobs);
            queueWindow.QueueStarted += QueueWindow_QueueStarted;
            queueWindow.QueueStopped += () => buttonShowQueue.Content = "Queue";
            completedWindow = new CompletedWindow(this, completedJobs);

            // Setup ffmpeg
            ffmpegEngine = new FFmpegEngine();
            ffmpegEngine.ProgressChanged += UpdateProgress;
            ffmpegEngine.ConversionCompleted += ConversionCompleted;
            ffmpegEngine.ConversionAborted += ConversioAborted;

            // Setup comboboxes
            comboBoxFormat.Items.Add("MP4");
            comboBoxFormat.Items.Add("MKV");
            comboBoxFormat.Items.Add("GIF");
            SetComboBoxEncoders();
            comboBoxRotation.Items.Add("Don't rotate");
            comboBoxRotation.Items.Add("Horizontal flip");
            comboBoxRotation.Items.Add("90° clockwise");
            comboBoxRotation.Items.Add("90° clockwise and flip");
            comboBoxRotation.Items.Add("180°");
            comboBoxRotation.Items.Add("180° and flip");
            comboBoxRotation.Items.Add("270° clockwise");
            comboBoxRotation.Items.Add("270° clockwise and flip");
            comboBoxRotation.SelectedIndex = 0;
            comboBoxFramerate.Items.Add("Don't change");
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

            // Get total memory
            GetPhysicallyInstalledSystemMemory(out totalMemory);
            totalMemory *= 1024;

            // Clear old versions and checks for new updates
            ManageUpdates();

            // Open command line video if present
            CheckCommandLine();
        }

        private async void ManageUpdates()
        {
            // Remove old files, if they exists
            if (Directory.Exists("old version"))
            {
                Directory.Delete("old version", true);
            }

            if (await UpdaterWindow.UpdateAvaiable())
            {
                buttonUpdate.Visibility = Visibility.Visible;
            }
        }

        private void CheckCommandLine()
        {
            if (Environment.GetCommandLineArgs().Length > 1)
            {
                string sourcePath = Environment.GetCommandLineArgs()[1];
                string extension = Path.GetExtension(sourcePath);
                if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                {
                    OpenNewMedia(sourcePath);
                }
            }
        }

        private async Task OpenSource(string playerSource = null)
        {
            isMediaOpen = false;
            string sourcePath = mediaInfo.Source;

            if (mediaInfo.IsLocal)
            {
                string extension = Path.GetExtension(sourcePath);
                if (extension == ".mkv")
                {
                    comboBoxFormat.SelectedIndex = 1;
                }
                else
                {
                    extension = ".mp4";
                    comboBoxFormat.SelectedIndex = 0;
                }
                textBoxDestination.Text = sourcePath.Remove(sourcePath.LastIndexOf('.')) + " converted" + extension;
                labelTitle.Text = Path.GetFileName(sourcePath);
            }
            else
            {
                if (String.IsNullOrEmpty(mediaInfo.Title))
                {
                    textBoxDestination.Text = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\stream.mp4";
                    labelTitle.Text = "[Network stream]";
                }
                else
                {
                    string validTitle = String.Join("", mediaInfo.Title.Split(Path.GetInvalidFileNameChars()));
                    textBoxDestination.Text = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{validTitle}.mp4";
                    labelTitle.Text = mediaInfo.Title;
                }
                comboBoxFormat.SelectedIndex = 0;
            }

            await mediaPlayer.Open(mediaInfo, playerSource);
            borderSource.BorderThickness = new Thickness(0);

            labelledTextBlockFileSize.Text = mediaInfo.Size.ToBytesString();
            long videoSize = mediaInfo.Bitrate.Bps * Convert.ToInt64(mediaInfo.Duration.TotalSeconds) / 8;
            long videoSizePercentage = videoSize * 100 / mediaInfo.Size;
            labelledTextBlockFileSize.ToolTip = $"Video: {videoSizePercentage}%\nAudio: {100 - videoSizePercentage}%";
            labelledTextBlockDuration.Text = mediaInfo.Duration.ToFormattedString(true);
            labelledTextBlockCodec.Text = mediaInfo.Codec;
            double fps = Math.Round(mediaInfo.Framerate, 0);
            labelledTextBlockFramerate.Text = !Double.IsNaN(fps) ? fps + " fps" : "-";
            labelledTextBlockBitrate.Text = mediaInfo.Bitrate.Kbps.ToString("F0") + " Kbps";
            labelledTextBlockResolution.Text = mediaInfo.Resolution.HasValue() ? mediaInfo.Resolution.ToString() : "-";
            labelledTextBlockAspectRatio.Text = mediaInfo.Resolution.AspectRatio.ToString();
            labelledTextBlockColorDepth.Text = mediaInfo.ColorInfo.ColorDepth + " bit";
            labelledTextBlockDynamicRange.Text = mediaInfo.DynamicRange;
            labelledTextBlockDynamicRange.ToolTip = mediaInfo.IsHDR ? mediaInfo.ColorInfo.ToString() : $"Color space: {mediaInfo.ColorInfo.ColorSpace}";
            labelledTextBlockPixelFormat.Text = mediaInfo.ColorInfo.PixelFormat;
            labelledTextBlockBitsPerPixel.Text = mediaInfo.BitsPerPixel.ToString("0.00");

            checkBoxCrop.IsChecked = false;

            cutInsideControlsList.Items.Clear();
            OnDurationChanged();

            shadowEffect.Opacity = 1;

            SetComboBoxFramerate();
            SetComboBoxResolution();
            SetupAudioTracks();

            // Since applying a filter to the player just after it opened a new video makes it crash the entire program, the only thing to do is to set all these controls to the default value so no filter needs to be applied
            ResetColorSliders();
            comboBoxRotation.SelectedIndex = 0;

            long size = (long)(numericUpDownBitrate.Value * 1000 / 8 * timeIntervalCollection.TotalDuration.TotalSeconds);
            textBlockTargetSize.Text = $"Video size: {size.ToBytesString()}";

            buttonStart.IsEnabled = true;
            buttonPreview.IsEnabled = true;
            buttonAddToQueue.IsEnabled = true;
            buttonAddCutControl.IsEnabled = true;

            isMediaOpen = true;
        }

        public async void OpenJob(Job job)
        {
            // Open media (if it's different from the one currently open)
            if (mediaInfo != job.SourceInfo)
            {
                mediaInfo = job.SourceInfo;
                await OpenSource();
            }
            ConversionOptions conversionOptions = job.ConversionOptions;

            // Load encoding settings
            LoadEncoder(conversionOptions);

            // Load filters
            LoadFilters(conversionOptions.Filters);

            // Load encoding segments
            cutInsideControlsList.Items.Clear();
            if (conversionOptions.EncodeSections?.Count > 0)
            {
                foreach (var item in conversionOptions.EncodeSections)
                {
                    AddEncodeSegment(item.Start, item.End);
                }
            }
            OnDurationChanged();
            checkBoxFade.IsChecked = conversionOptions.FadeEffect;

            // Load audio tracks
            foreach (var aco in conversionOptions.AudioConversionOptions)
            {
                foreach (Controls.AudioTrackControl atc in listViewAudioTracks.Items)
                {
                    if (atc.AudioTrack.StreamIndex == aco.Key)
                    {
                        atc.ConversionOptions = aco.Value;
                        break;
                    }
                }
            }
        }

        private void LoadEncoder(ConversionOptions conversionOptions)
        {
            if (conversionOptions.Encoder != null)
            {
                comboBoxEncoder.SelectedValue = conversionOptions.Encoder;
                comboBoxPreset.SelectedValue = conversionOptions.Encoder.Preset.GetName();
                comboBoxQuality.SelectedValue = conversionOptions.Encoder.Quality.GetName();
                comboBoxPixelFormat.SelectedValue = conversionOptions.Encoder.PixelFormat.GetName();
            }
            switch (conversionOptions.EncodingMode)
            {
                case EncodingMode.ConstantQuality:
                    radioButtonQuality.IsChecked = true;
                    checkBoxTwoPass.IsChecked = false;
                    break;
                case EncodingMode.AverageBitrate_SinglePass:
                    radioButtonBitrate.IsChecked = true;
                    checkBoxTwoPass.IsChecked = false;
                    numericUpDownBitrate.Value = conversionOptions.Encoder.Bitrate.Bps / 1000;
                    break;
                case EncodingMode.AverageBitrate_FirstPass:
                case EncodingMode.AverageBitrate_SecondPass:
                    radioButtonBitrate.IsChecked = true;
                    checkBoxTwoPass.IsChecked = true;
                    numericUpDownBitrate.Value = conversionOptions.Encoder.Bitrate.Bps / 1000;
                    break;
                case EncodingMode.NoEncoding:
                    comboBoxFormat.SelectedIndex = 2;
                    comboBoxQuality.SelectedIndex = 0;
                    foreach (var filter in conversionOptions.Filters)
                    {
                        if (filter is Filters.GifFilter gifFilter)
                        {
                            comboBoxPreset.SelectedIndex = gifFilter.UseMultiplePalette ? 2 : 1;
                            comboBoxQuality.SelectedIndex = Array.IndexOf(new byte[] { 255, 200, 150, 100, 60, 30 }, gifFilter.MaxColors);
                            break;
                        }
                    }
                    break;
            }
        }

        private void LoadFilters(IEnumerable<IFilter> filters)
        {
            foreach (var filter in filters)
            {
                switch (filter)
                {
                    case Filters.CropFilter f:
                        // Setting the flag isDragging stops the controls from checking for errors on the single values and refusing them
                        isDragging = true;
                        integerTextBoxCropBottom.Value = f.Bottom;
                        integerTextBoxCropLeft.Value = f.Left;
                        integerTextBoxCropRight.Value = f.Right;
                        isDragging = false;
                        // The flag is removed before editing the last control to allow the ValueChanged event to set the rectangle size
                        integerTextBoxCropTop.Value = f.Top;
                        checkBoxCrop.IsChecked = true;
                        break;
                    case Filters.FpsFilter f:
                        comboBoxFramerate.SelectedValue = (int)f.Framerate;
                        break;
                    case Filters.RotationFilter f:
                        comboBoxRotation.SelectedIndex = f.RotationType;
                        break;
                    case Filters.ScaleFilter f:
                        comboBoxResolution.SelectedValue = f.OutputResolution.ToString();
                        break;
                    case Filters.EqFilter f:
                        sliderBrightness.Value = f.Brightness;
                        sliderContrast.Value = f.Contrast;
                        sliderSaturation.Value = f.Saturation;
                        sliderGamma.Value = f.Gamma;
                        sliderRed.Value = f.GammaRed;
                        sliderGreen.Value = f.GammaGreen;
                        sliderBlue.Value = f.GammaBlue;
                        break;
                }
            }
        }

        private void SetComboBoxEncoders()
        {
            comboBoxEncoder.Items.Add(new CopyEncoder());
            comboBoxEncoder.Items.Add(new H264Encoder());
            comboBoxEncoder.Items.Add(new H265Encoder());
            if (VideoAdapters.Contains("nvidia geforce rtx"))
            {
                comboBoxEncoder.Items.Add(new H264Nvenc());
                comboBoxEncoder.Items.Add(new H265Nvenc());
            }
            if (VideoAdapters.Contains("intel"))
            {
                comboBoxEncoder.Items.Add(new H264QuickSync());
                comboBoxEncoder.Items.Add(new H265QuickSync());
            }
            comboBoxEncoder.Items.Add(new AV1Encoder());
            comboBoxEncoder.SelectedIndex = 1;
        }

        private void SetComboBoxFramerate(int maxFramerate = 1000)
        {
            int framerate = Convert.ToInt32(mediaInfo.Framerate);

            comboBoxFramerate.Items.Clear();
            if (framerate <= maxFramerate)
                comboBoxFramerate.Items.Add("Don't change");
            foreach (var item in new int[]{ 165, 144, 120, 60, 50, 40, 30, 24, 20, 15 })
            {
                if (item < maxFramerate && item < framerate)
                    comboBoxFramerate.Items.Add(item);
            }
            comboBoxFramerate.SelectedIndex = 0;
        }

        private void SetComboBoxResolution()
        {
            Resolution r = mediaInfo.Resolution;
            comboBoxResolution.Items.Clear();
            comboBoxResolution.Items.Add("Don't change");
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

        private void SetComboBoxPixelFormats(VideoEncoder encoder)
        {
            comboBoxPixelFormat.Items.Clear();
            foreach (PixelFormat pixelFormat in encoder.SupportedPixelFormats)
            {
                comboBoxPixelFormat.Items.Add(pixelFormat.GetName());
            }
            comboBoxPixelFormat.SelectedIndex = 0;
        }

        private void SetupAudioTracks()
        {
            listViewAudioTracks.Items.Clear();

            foreach (AudioTrack audioTrack in mediaInfo.AudioTracks)
            {
                Controls.AudioTrackControl audioTrackControl = new Controls.AudioTrackControl(audioTrack);
                audioTrackControl.ExportButtonClicked += AudioTrackControl_ExportButtonClicked;
                listViewAudioTracks.Items.Add(audioTrackControl);
            }
        }

        private void ResetColorSliders()
        {
            sliderContrast.Value = 0;
            sliderBrightness.Value = 0;
            sliderSaturation.Value = 0;
            sliderGamma.Value = 0;
            sliderRed.Value = 0;
            sliderGreen.Value = 0;
            sliderBlue.Value = 0;
        }

        private void ButtonOpenStream_Click(object sender, RoutedEventArgs e)
        {
            OpenStreamWindow osw = new OpenStreamWindow();
            osw.ShowDialog();
            if (osw.MediaStream != null)
            {
                mediaInfo = osw.MediaStream;
                _ = OpenSource(osw.PlayerSource);
            }
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
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
                OpenNewMedia(ofd.FileName);
            }
        }

        private void MediaElement_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                string extension = Path.GetExtension(paths[0]);
                if (Array.IndexOf(SUPPORTED_EXTENSIONS, extension) > -1)
                {
                    OpenNewMedia(paths[0]);
                }
                else
                {
                    new MessageBoxWindow($"This file type ({extension}) is not supported", "Error opening file").ShowDialog();
                }
            }
            this.StopStoryboard("DragOverAnimation");
        }

        private async void OpenNewMedia(string source)
        {
            try
            {
                labelTitle.Text = "Opening media...";
                mediaInfo = await MediaInfo.Open(source);
                _ = OpenSource();
            }
            catch (Exception ex)
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                labelTitle.Text = $"FF Video Converter v{version}";
                new MessageBoxWindow(ex.Message, "Error opening file").ShowDialog();
            }
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
            // Make sure the ffmpeg process is not running
            ffmpegEngine.StopConversion();

            // Terminates the application (this call is necessary because other opened windows would keep the application running)
            Application.Current.Shutdown();
        }

        #endregion

        #region Conversion settings

        private void ComboBoxFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isMediaOpen) return;

            if (textBoxDestination.Text.Length > 0)
            {
                string extension = comboBoxFormat.SelectedItem.ToString().ToLower();
                textBoxDestination.Text = Path.ChangeExtension(textBoxDestination.Text, extension);
            }

            if (comboBoxFormat.SelectedIndex == 2) // switched to gif
            {
                comboBoxEncoder.Items.Clear();
                comboBoxEncoder.Items.Add("GIF encoder");
                comboBoxEncoder.SelectedIndex = 0;
                comboBoxEncoder.ToolTip = "The gif encoder will produce massive files with very low quality (since it's limited to 256 colors per frame), and framerate is capped at 50fps\nUnless necessary, don't use this encoder";

                comboBoxPreset.Items.Clear();
                comboBoxPreset.Items.Add("Default palette");
                comboBoxPreset.Items.Add("Global palette");
                comboBoxPreset.Items.Add("Per-frame palette");
                comboBoxPreset.ToolTip = "Default means no custom palette is generated; output will be low in size and quality\nGlobal means a single global palette is generated from the color data of every frame; output will be high in quality but in size too\nPer-frame means a custom palette is generated for every single frame, based on that frame colors; output will have the maximum quality and size possible (on average)";
                comboBoxPreset.SelectedIndex = 0;

                SetComboBoxFramerate(51);
                radioButtonQuality.IsChecked = true;
                radioButtonBitrate.IsEnabled = false;
            }
            else if (radioButtonBitrate.IsEnabled == false) // switched to mp4 or mkv, from gif
            {
                SetComboBoxEncoders();
                comboBoxEncoder.ToolTip = "Copy means the video is not re-encoded, thus conversion options are not used\nH264 provides maximum compatibility and is faster to encode\nH265 is from 25% to 50% more efficient than H264, but requires more time to encode\nAV1 is a modern codec that's more efficient than H265 but much slower to enccode, ideal for very high resolution content\nHardware encoders like QuickSync or Nvenc can encode much faster than software encoders, but at a lower quality per bitrate";
                
                SetComboBoxFramerate();
                comboBoxQuality.IsEnabled = true;

                comboBoxPreset.Items.Clear();
                foreach (Preset preset in Enum.GetValues(typeof(Preset)))
                {
                    comboBoxPreset.Items.Add(preset.GetName());
                }
                comboBoxPreset.SelectedIndex = 2;
                comboBoxPreset.ToolTip = "Select the encoding speed to quality ratio\nA slower profile will require more time to encode, but it will result in better quality than a faster profile at the same bitrate";

                radioButtonBitrate.IsEnabled = true;
            }
        }

        private void ComboBoxEncoder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBoxEncoder.SelectedItem is VideoEncoder selectedEncoder)
            {
                if (selectedEncoder is CopyEncoder)
                {
                    gridEncoding.IsEnabled = false;
                    gridColor.IsEnabled = false;
                    gridResize.IsEnabled = false;

                    foreach (Controls.EncodeSegmentControl encodeSegmentControl in cutInsideControlsList.Items)
                    {
                        encodeSegmentControl.ShowKeyframesSuggestions = true;
                    }

                    this.PlayStoryboard("CheckBoxFadeAnimationOut");
                    this.PlayStoryboard("PreviewButtonAnimationOut");
                }
                else
                {
                    gridEncoding.IsEnabled = true;
                    gridColor.IsEnabled = true;
                    gridResize.IsEnabled = true;
                    comboBoxPreset.IsEnabled = !(selectedEncoder is H264Nvenc || selectedEncoder is H265Nvenc);

                    if (cutInsideControlsList.Items.Count > 1)
                    {
                        this.PlayStoryboard("CheckBoxFadeAnimationIn");
                    }
                    foreach (Controls.EncodeSegmentControl item in cutInsideControlsList.Items)
                    {
                        item.ShowKeyframesSuggestions = false;
                    }

                    // Setting the IsEnabled property to false removes the binding, so when IsEnabled can be true, the binding is recreated
                    if (selectedEncoder.IsDoublePassSupported)
                    {
                        Binding binding = new Binding();
                        binding.ElementName = "radioButtonBitrate";
                        binding.Path = new PropertyPath("IsEnabled");
                        BindingOperations.SetBinding(checkBoxTwoPass, IsEnabledProperty, binding);
                    }
                    else
                    {
                        checkBoxTwoPass.IsEnabled = false;
                    }

                    this.PlayStoryboard("PreviewButtonAnimationIn");
                }

                SetComboBoxPixelFormats(selectedEncoder);
            }
        }

        private void ComboBoxProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBoxFormat.SelectedIndex == 2) // gif
            {
                comboBoxQuality.IsEnabled = comboBoxPreset.SelectedIndex != 0;
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (isMediaOpen)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Select the destination file";
                sfd.Filter = "MKV|*.mkv|MP4|*.mp4";
                sfd.FileName = Path.GetFileNameWithoutExtension(textBoxDestination.Text) + "_converted";
                string extension = textBoxDestination.Text.Substring(textBoxDestination.Text.LastIndexOf('.'));
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
                    new ComparisonWindow(mediaInfo, (VideoEncoder)comboBoxEncoder.SelectedItem).ShowDialog();
                }
                else
                {
                    new MessageBoxWindow("Selected encoder should be different than Native.", "FF Video Converter").ShowDialog();
                }
            }
        }

        private void CheckBoxCrop_Click(object sender, RoutedEventArgs e)
        {
            if (isMediaOpen)
            {
                if (checkBoxCrop.IsChecked == true)
                {
                    mediaPlayer.CropActive = true;
                    if (comboBoxRotation.SelectedIndex != 0)
                        textBlockRotationCropWarning.Visibility = Visibility.Visible;

                    // If it's the first time crop is enabled, sets initial values corresponding to initial rectangle size
                    if (integerTextBoxCropBottom.Value == 0 && integerTextBoxCropLeft.Value == 0 && integerTextBoxCropRight.Value == 0 && integerTextBoxCropTop.Value == 0)
                    {
                        isDragging = true;
                        Controls.MediaPlayer.CropData cropData = mediaPlayer.Crop;
                        integerTextBoxCropTop.Value = cropData.Top;
                        integerTextBoxCropLeft.Value = cropData.Left;
                        integerTextBoxCropBottom.Value = cropData.Bottom;
                        integerTextBoxCropRight.Value = cropData.Right;
                        isDragging = false;
                    }
                }
                else
                {
                    mediaPlayer.CropActive = false;
                    textBlockRotationCropWarning.Visibility = Visibility.Hidden;
                }
            }
        }

        private void IntegerTextBoxCrop_ValueChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!isDragging && isMediaOpen)
            {
                double newLeft = integerTextBoxCropLeft.Value * mediaPlayer.Width / mediaInfo.Width;
                double newTop = integerTextBoxCropTop.Value * mediaPlayer.Height / mediaInfo.Height;
                double newRight = integerTextBoxCropRight.Value * mediaPlayer.Width / mediaInfo.Width;
                double newBottom = integerTextBoxCropBottom.Value * mediaPlayer.Height / mediaInfo.Height;
                double newWidth = mediaPlayer.Width - newLeft - newRight;
                double newHeight = mediaPlayer.Height - newTop - newBottom;

                if (newWidth < RECT_MIN_SIZE || newHeight < RECT_MIN_SIZE)
                {
                    Controls.NumericUpDown itb = (Controls.NumericUpDown)sender;
                    itb.ValueChanged -= IntegerTextBoxCrop_ValueChanged;
                    itb.Value = (int)e.OldValue;
                    itb.ValueChanged += IntegerTextBoxCrop_ValueChanged;
                }
                else
                {
                    mediaPlayer.Crop = new Controls.MediaPlayer.CropData(integerTextBoxCropLeft.Value, integerTextBoxCropTop.Value, integerTextBoxCropRight.Value, integerTextBoxCropBottom.Value);
                    textBlockOutputResolution.Text = $"{(mediaInfo.Width - integerTextBoxCropLeft.Value - integerTextBoxCropRight.Value).ToString("0")}x{(mediaInfo.Height - integerTextBoxCropTop.Value - integerTextBoxCropBottom.Value).ToString("0")}";
                }
            }
        }
        
        private void MediaPlayer_CropChanged(Controls.MediaPlayer.CropData cropData)
        {
            isDragging = true;
            integerTextBoxCropTop.Value = cropData.Top;
            integerTextBoxCropLeft.Value = cropData.Left;
            integerTextBoxCropBottom.Value = cropData.Bottom;
            integerTextBoxCropRight.Value = cropData.Right;
            int horizontal = mediaInfo.Width - cropData.Left - cropData.Right;
            int vertical = mediaInfo.Height - cropData.Top - cropData.Bottom;
            textBlockOutputResolution.Text = $"{horizontal:0}x{vertical:0}";
            isDragging = false;
        }

        private void ComboBoxRotation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVideoFilter();

            if (comboBoxRotation.SelectedIndex != 0 && checkBoxCrop.IsChecked == true)
            {
                textBlockRotationCropWarning.Visibility = Visibility.Visible;
            }
            else
            {
                textBlockRotationCropWarning.Visibility = Visibility.Hidden;
            }
        }

        private void NumericUpDownBitrate_ValueChanged(object _, DependencyPropertyChangedEventArgs _1)
        {
            if (isMediaOpen)
            {
                long size = (long)(numericUpDownBitrate.Value * 1000 / 8 * timeIntervalCollection.TotalDuration.TotalSeconds);
                textBlockTargetSize.Text = $"Video size: {size.ToBytesString()}";
            }
        }

        private void ButtonAddCutControl_Click(object sender, RoutedEventArgs e)
        {
            AddEncodeSegment(null, null);
            OnDurationChanged();
        }

        private void SliderColor_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateVideoFilter();
        }

        #endregion

        #region Conversion process

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            if (CheckForErrors()) return;

            ConversionOptions conversionOptions;
            if (comboBoxFormat.SelectedIndex == 2) // gif
            {
                conversionOptions = GenerateConversionOptionsForGif();
            }
            else // mp4,mkv
            {
                VideoEncoder encoder = GenerateVideoEncoder();
                if (encoder == null) return; // it's null when user declined to encode
                conversionOptions = GenerateConversionOptions(encoder);
            }

            Job job = new Job(mediaInfo, textBoxDestination.Text, conversionOptions);
            switch (job.JobType)
            {
                case JobType.Conversion:
                    labelledtextBlockProgress.Text = "Starting conversion...";
                    break;
                case JobType.FastCut:
                    labelledtextBlockProgress.Text = "Preparing to cut...";
                    break;
                case JobType.Download:
                    labelledtextBlockProgress.Text = "Starting download...";
                    break;
                case JobType.Remux:
                    labelledtextBlockProgress.Text = "Starting remux...";
                    break;
                case JobType.AudioExport:
                    labelledtextBlockProgress.Text = "Starting audio export...";
                    break;
            }

            if (((Button)sender).Name == "buttonStart" || (queueWindow.QueueActive && runningJob == null)) // If the queue is started but there are no conversion running, run this one directly instead of adding it to the queue
            {
                RunJob(job);
            }
            else
            {
                queuedJobs.Add(job);
                labelledtextBlockProgress.Text = "Added to queue";
            }
        }

        private void AudioTrackControl_ExportButtonClicked(AudioTrack audioTrack)
        {
            if (buttonStart.IsEnabled)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Select the destination file";
                sfd.Filter = audioTrack.Codec.ToLower() switch
                {
                    "aac" => "AAC audio file|*.aac|MP4 audio container|*.m4a|Matroska audio container|*.mka",
                    "opus" => "Opus audio file|*.opus|MP4 audio container|*.m4a|Matroska audio container|*.mka",
                    "ac3" or "eac3" => "Dolby Digital audio file|*.ac3|MP4 audio container|*.m4a|Matroska audio container|*.mka",
                    "dts" => "DTS audio file|*.dts|Matroska audio container|*.mka",
                    "truehd" => "Dolby TrueHD audio file|*.thd|Matroska audio container|*.mka",
                    _ => "Matroska audio container|*.mka",
                };
                sfd.FileName = Path.GetFileNameWithoutExtension(mediaInfo.Title);
                sfd.FilterIndex = 0;
                bool? result = sfd.ShowDialog();
                if (result == true)
                {
                    RunJob(new Job(mediaInfo, sfd.FileName, audioTrack));
                }
            }
            else
            {
                new MessageBoxWindow("Can't perform this operation while there is a conversion running", "FF Video Converter").ShowDialog();
            }
        }

        private bool CheckForErrors()
        {
            if (mediaInfo.Source.Equals(textBoxDestination.Text))
            {
                new MessageBoxWindow("Source and destination file are the same.\nSelect a different file name.", "FF Video Converter").ShowDialog();
                return true;
            }
            if (!textBoxDestination.Text.EndsWith(".mp4") && !textBoxDestination.Text.EndsWith(".mkv") && !textBoxDestination.Text.EndsWith(".gif"))
            {
                new MessageBoxWindow("Wrong output file format.\nSelect either mp4 or mkv as output extension.", "FF Video Converter").ShowDialog();
                return true;
            }

            return false;
        }

        private VideoEncoder GenerateVideoEncoder()
        {
            VideoEncoder encoder = (VideoEncoder)comboBoxEncoder.SelectedItem;
            encoder.Preset = (Preset)comboBoxPreset.SelectedIndex;
            if (mediaInfo.IsHDR)
            {
                if (encoder is H265Encoder)
                {
                    encoder.ColorInfo = mediaInfo.ColorInfo;
                }
                else if (encoder is not CopyEncoder)
                {
                    if (new QuestionBoxWindow("Encoding with this encoder will lose HDR metadata, use x265 to preserve HDR it.\n\nProcede anyway?", "FF Video Converter").ShowDialog() == false)
                    {
                        return null;
                    }
                }
            }
            if (radioButtonBitrate.IsChecked == true)
                encoder.Bitrate = numericUpDownBitrate.Value * 1000;
            else
                encoder.Quality = (Quality)comboBoxQuality.SelectedIndex;
            foreach (PixelFormat pixelFormat in Enum.GetValues<PixelFormat>())
            {
                if (pixelFormat.GetName() == comboBoxPixelFormat.SelectedItem.ToString())
                {
                    encoder.PixelFormat = pixelFormat;
                    break;
                }
            }

            return encoder;
        }

        private ConversionOptions GenerateConversionOptions(VideoEncoder encoder)
        {
            ConversionOptions conversionOptions = new ConversionOptions(encoder);

            if (encoder is CopyEncoder)
            {
                conversionOptions.EncodingMode = EncodingMode.Copy;
            }
            else
            {
                if (checkBoxCrop.IsChecked == true)
                {
                    conversionOptions.Filters.Add(new Filters.CropFilter((short)integerTextBoxCropLeft.Value, (short)integerTextBoxCropTop.Value, (short)integerTextBoxCropRight.Value, (short)integerTextBoxCropBottom.Value));
                }
                if (comboBoxResolution.SelectedIndex != 0)
                {
                    conversionOptions.Filters.Add(new Filters.ScaleFilter(Resolution.FromString(comboBoxResolution.Text)));
                }
                if (comboBoxFramerate.SelectedItem.ToString() != "Don't change")
                {
                    conversionOptions.Filters.Add(new Filters.FpsFilter((int)comboBoxFramerate.SelectedItem));
                }
                if (comboBoxRotation.SelectedIndex != 0)
                {
                    conversionOptions.Filters.Add(new Filters.RotationFilter(comboBoxRotation.SelectedIndex));
                }
                if (sliderBrightness.Value != 0 || sliderContrast.Value != 0 || sliderSaturation.Value != 0 || sliderGamma.Value != 0 || sliderRed.Value != 0 || sliderGreen.Value != 0 || sliderBlue.Value != 0)
                {
                    conversionOptions.Filters.Add(new Filters.EqFilter
                    {
                        Brightness = sliderBrightness.Value,
                        Contrast = sliderContrast.Value,
                        Saturation = sliderSaturation.Value,
                        Gamma = sliderGamma.Value,
                        GammaRed = sliderRed.Value,
                        GammaGreen = sliderGreen.Value,
                        GammaBlue = sliderBlue.Value
                    });
                }
                conversionOptions.FadeEffect = checkBoxFade.IsChecked.Value;

                if (encoder == null)
                {
                    conversionOptions.EncodingMode = EncodingMode.NoEncoding;
                }
                else if (radioButtonQuality.IsChecked == true)
                {
                    conversionOptions.EncodingMode = EncodingMode.ConstantQuality;
                }
                else
                {
                    conversionOptions.EncodingMode = checkBoxTwoPass.IsChecked == true ? EncodingMode.AverageBitrate_FirstPass : EncodingMode.AverageBitrate_SinglePass;
                }
            }

            conversionOptions.EncodeSections = timeIntervalCollection;

            foreach (Controls.AudioTrackControl audioTrackControl in listViewAudioTracks.Items)
            {
                if (audioTrackControl.ConversionOptions != null)
                {
                    conversionOptions.AudioConversionOptions.Add(audioTrackControl.AudioTrack.StreamIndex, audioTrackControl.ConversionOptions);
                }
            }

            return conversionOptions;
        }

        private ConversionOptions GenerateConversionOptionsForGif()
        {
            ConversionOptions conversionOptions = GenerateConversionOptions(null);
            conversionOptions.NoAudio = true;

            // Fps filter must be present, or it defaults to a very high frame delay; thus is necessary to manually add it when the "Don't change" option is selected
            if (comboBoxFramerate.SelectedItem.ToString() == "Don't change")
                conversionOptions.Filters.Add(new Filters.FpsFilter(mediaInfo.Framerate));

            if (comboBoxPreset.SelectedIndex != 0)
            {
                Filters.GifFilter gifFilter = new Filters.GifFilter();
                gifFilter.UseMultiplePalette = comboBoxPreset.SelectedIndex == 2;
                byte[] colorCounts = new byte[] { 255, 200, 150, 100, 60, 30 }; // Best, VeryGood, Good, Medium, Low, VeryLow 
                gifFilter.MaxColors = colorCounts[comboBoxQuality.SelectedIndex];
                conversionOptions.Filters.Add(gifFilter);
            }

            return conversionOptions;
        }

        private async void RunJob(Job job)
        {
            job.State = JobState.Running;
            runningJob = job;
            if (job.JobType == JobType.AudioExport)
                ffmpegEngine.ExtractAudioTrack(job.SourceInfo, job.AudioTrack.StreamIndex, job.Destination, TimeSpan.Zero, TimeSpan.Zero);
            else
                ffmpegEngine.Convert(job.SourceInfo, job.Destination, job.ConversionOptions);

            buttonPauseResume.IsEnabled = true;
            buttonStop.IsEnabled = true;
            buttonKill.IsEnabled = true;
            buttonStart.IsEnabled = false;
            buttonPreview.IsEnabled = false;
            await mediaPlayer.Pause();
            this.PlayStoryboard("ProgressAnimationIn");
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
            BlockSleepMode();
            queueWindow.RunningJob = job;
        }

        private void UpdateProgress(ProgressData progressData)
        {
            double percentage = Math.Min(progressData.Percentage, 99.4);
            double secondsToEncode = progressData.TotalTime.TotalSeconds - progressData.CurrentTime.TotalSeconds;
            double remainingTime = progressData.EncodingSpeed == 0 ? 0 : secondsToEncode / progressData.EncodingSpeed;

            labelledtextBlockProgress.Label = "Processed";
            labelledtextBlockOutputSize.Label = "Output size";
            labelledtextBlockProgress.Text = $"{progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
            labelledtextBlockOutputSize.Text = progressData.CurrentByteSize.ToBytesString();

            switch (progressData.EncodingMode)
            {
                case EncodingMode.Copy:
                    if (runningJob.JobType == JobType.Conversion)
                    {
                        if (mediaInfo.IsLocal) labelledtextBlockProgress.Text += $"       {progressData.EncodingSpeed:0.00}s per second ({progressData.EncodingSpeedFps} fps)";
                        labelledtextBlockOutputSize.Text += $" / {progressData.TotalByteSize.ToBytesString()}";
                    }
                    else if (runningJob.JobType == JobType.Download)
                    {
                        labelledtextBlockProgress.Label = "Progress";
                        labelledtextBlockProgress.Text = $"{progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
                        labelledtextBlockOutputSize.Label = "Downloaded";
                        labelledtextBlockOutputSize.Text = progressData.CurrentByteSize.ToBytesString();
                    }
                    else
                    {
                        labelledtextBlockProgress.Text = $"{progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
                    }
                    break;
                case EncodingMode.ConstantQuality:
                    if (mediaInfo.IsLocal) labelledtextBlockProgress.Text += $"       {progressData.EncodingSpeed:0.00}s per second ({progressData.EncodingSpeedFps} fps)";
                    if (progressData.AverageBitrate.Bps > 0)
                    {
                        long approximateOutputByteSize = progressData.CurrentByteSize + (long)(progressData.AverageBitrate.Bps * secondsToEncode / 8);
                        labelledtextBlockOutputSize.Text += $" / {approximateOutputByteSize.ToBytesString()} (estimated)";
                    }
                    break;
                case EncodingMode.AverageBitrate_SinglePass or EncodingMode.AverageBitrate_SecondPass:
                    if (mediaInfo.IsLocal) labelledtextBlockProgress.Text += $"       {progressData.EncodingSpeed:0.00}s per second ({progressData.EncodingSpeedFps} fps)";
                    labelledtextBlockOutputSize.Text += $" / {progressData.TotalByteSize.ToBytesString()}";
                    break;
                case EncodingMode.AverageBitrate_FirstPass:
                    secondsToEncode = progressData.TotalTime.TotalSeconds * 2 - progressData.CurrentTime.TotalSeconds;
                    remainingTime = progressData.EncodingSpeed == 0 ? 0 : secondsToEncode / progressData.EncodingSpeed;
                    labelledtextBlockProgress.Label = "Analyzed";
                    labelledtextBlockProgress.Text = $"{progressData.CurrentTime.ToFormattedString()} / {progressData.TotalTime.ToFormattedString()}";
                    if (mediaInfo.IsLocal) labelledtextBlockProgress.Text += $"       {progressData.EncodingSpeed:0.00}s per second ( {progressData.EncodingSpeedFps}  fps)";
                    labelledtextBlockOutputSize.Label = null;
                    labelledtextBlockOutputSize.Text = "";
                    break;
                case EncodingMode.NoEncoding: // Gif encoding
                    if (progressData.CurrentFrames == 0)
                    {
                        labelledtextBlockProgress.Label = null;
                        labelledtextBlockProgress.Text = "Generating palette, this might take some time...";
                        labelledtextBlockOutputSize.Label = null;
                        labelledtextBlockOutputSize.Text = "";
                    }
                    else goto case EncodingMode.ConstantQuality;
                    break;
            }

            DoubleAnimation progressAnimation = new DoubleAnimation(percentage, TimeSpan.FromSeconds(0.5));
            progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
            TaskbarItemInfo.ProgressValue = percentage / 100;
            Title = $"{Math.Floor(percentage)}%   {TimeSpan.FromSeconds(remainingTime).ToFormattedString()}";
            labelProgress.Text = $"Progress: {Math.Round(percentage)}%   Remaining time: {TimeSpan.FromSeconds(remainingTime).ToFormattedString()}";

            if (percentage > 99)
            {
                labelledtextBlockProgress.Label = null;
                labelledtextBlockProgress.Text = "Finishing conversion, this might take some time...";
            }

            long usedMemory = (long)memoryCounter.NextValue() + ffmpegEngine.PrivateWorkingSet;
            long avaiableMemory = (long)avaiableMemoryCounter.NextValue() * 1024 * 1024;
            textBlockMemory.Text = $"Memory used: {usedMemory.ToBytesString()} \nAvaiable: {avaiableMemory.ToBytesString()} / {totalMemory.ToBytesString()}";
            if (avaiableMemory < 300_000_000) // 300MB
            {
                ButtonPauseResume_Click(null, null);
                new MessageBoxWindow($"Current avaiable memory is very low: {avaiableMemory.ToBytesString()}\nEither reduce memory used by other applications or stop this conversion process", "WARNING: low memory").ShowDialog();
            }
        }

        private async void ConversionCompleted()
        {
            DoubleAnimation progressAnimation = new DoubleAnimation(100, TimeSpan.FromSeconds(0));
            progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);

            long outputSize = new FileInfo(runningJob.Destination).Length;
            switch (runningJob.JobType)
            {
                case JobType.Conversion:
                    labelledtextBlockProgress.Text = "Video converted!";
                    labelledtextBlockOutputSize.Text = "Output size: " + outputSize.ToBytesString();
                    break;
                case JobType.FastCut:
                    labelledtextBlockProgress.Text = "Video cut!";
                    labelledtextBlockOutputSize.Text = "Output size: " + outputSize.ToBytesString();
                    break;
                case JobType.Download:
                    labelledtextBlockProgress.Text = "Video downloaded!";
                    labelledtextBlockOutputSize.Text = "Video size: " + outputSize.ToBytesString();
                    break;
                case JobType.AudioExport:
                    labelledtextBlockProgress.Text = "Audio exported!";
                    labelledtextBlockOutputSize.Text = "Audio size: " + outputSize.ToBytesString();
                    break;
            }

            if (runningJob.JobType == JobType.AudioExport)
            {
                runningJob.ConversionResults.Add(new ConversioResult("Size", outputSize.ToBytesString()));
            }
            else
            {
                MediaInfo outputFile = await MediaInfo.Open(runningJob.Destination);
                int percentageDifference = 100 - (int)(outputSize / (float)runningJob.SourceInfo.Size * 100);
                string biggerSmaller = percentageDifference >= 0 ? "smaller" : "bigger";
                if (runningJob.JobType == JobType.Download)
                {
                    runningJob.ConversionResults.Add(new ConversioResult("Size", outputSize.ToBytesString()));
                    runningJob.ConversionResults.Add(new ConversioResult("Duration", outputFile.Duration.ToFormattedString(true)));
                    runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{outputFile.Codec}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{outputFile.Framerate} fps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{outputFile.Bitrate.Kbps:F0} Kbps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Resolution", outputFile.Resolution.ToString()));
                    runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", outputFile.Resolution.AspectRatio.ToString()));
                    runningJob.ConversionResults.Add(new ConversioResult("Color depth", $"{outputFile.ColorInfo.ColorDepth} bit"));
                    runningJob.ConversionResults.Add(new ConversioResult("Dynamic range", outputFile.DynamicRange));
                    runningJob.ConversionResults.Add(new ConversioResult("Pixel format", outputFile.ColorInfo.PixelFormat));
                    runningJob.ConversionResults.Add(new ConversioResult("Bits per pixel", outputFile.BitsPerPixel.ToString("0.00")));
                }
                else
                {
                    runningJob.ConversionResults.Add(new ConversioResult("Size", $"{runningJob.SourceInfo.Size.ToBytesString()}   ⟶   {outputSize.ToBytesString()}  ({Math.Abs(percentageDifference)}% {biggerSmaller})"));
                    runningJob.ConversionResults.Add(new ConversioResult("Duration", $"{runningJob.SourceInfo.Duration.ToFormattedString(true)}   ⟶   {outputFile.Duration.ToFormattedString(true)}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{runningJob.SourceInfo.Codec}   ⟶   {outputFile.Codec}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{runningJob.SourceInfo.Framerate} fps   ⟶   {outputFile.Framerate} fps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{runningJob.SourceInfo.Bitrate.Kbps:F0} Kbps   ⟶   {outputFile.Bitrate.Kbps:F0} Kbps"));
                    runningJob.ConversionResults.Add(new ConversioResult("Resolution", $"{runningJob.SourceInfo.Resolution}   ⟶   {outputFile.Resolution}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", $"{runningJob.SourceInfo.Resolution.AspectRatio}   ⟶   {outputFile.Resolution.AspectRatio}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Color depth", $"{runningJob.SourceInfo.ColorInfo.ColorDepth} bit   ⟶   {outputFile.ColorInfo.ColorDepth} bit")); 
                    runningJob.ConversionResults.Add(new ConversioResult("Dynamic range", $"{runningJob.SourceInfo.DynamicRange}   ⟶   {outputFile.DynamicRange}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Pixel format", $"{runningJob.SourceInfo.ColorInfo.PixelFormat}   ⟶   {outputFile.ColorInfo.PixelFormat}"));
                    runningJob.ConversionResults.Add(new ConversioResult("Bits per pixel", $"{runningJob.SourceInfo.BitsPerPixel:0.00}   ⟶   {outputFile.BitsPerPixel:0.00}"));

                    // Show conversion results compared to original values, only if the conversion was not a download and the loaded media is the same as the converted one
                    if (mediaInfo.Source == runningJob.SourceInfo.Source)
                    {
                        labelledTextBlockFileSize.Text = $"{runningJob.SourceInfo.Size.ToBytesString()}   ⟶   {outputSize.ToBytesString()}  ({Math.Abs(percentageDifference)}% {biggerSmaller})";
                        labelledTextBlockDuration.Text = $"{runningJob.SourceInfo.Duration.ToFormattedString(true)}   ⟶   {outputFile.Duration.ToFormattedString(true)}";
                        labelledTextBlockCodec.Text = $"{runningJob.SourceInfo.Codec}   ⟶   {outputFile.Codec}";
                        labelledTextBlockFramerate.Text = $"{runningJob.SourceInfo.Framerate} fps   ⟶   {outputFile.Framerate} fps";
                        labelledTextBlockBitrate.Text = $"{runningJob.SourceInfo.Bitrate.Kbps:F0} Kbps   ⟶   {outputFile.Bitrate.Kbps:F0} Kbps";
                        labelledTextBlockResolution.Text = $"{runningJob.SourceInfo.Resolution}   ⟶   {outputFile.Resolution}";
                        labelledTextBlockAspectRatio.Text = $"{runningJob.SourceInfo.Resolution.AspectRatio}   ⟶   {outputFile.Resolution.AspectRatio}";
                        labelledTextBlockColorDepth.Text = $"{runningJob.SourceInfo.ColorInfo.ColorDepth} bit   ⟶   {outputFile.ColorInfo.ColorDepth} bit";
                        labelledTextBlockDynamicRange.Text = $"{runningJob.SourceInfo.DynamicRange}   ⟶   {outputFile.DynamicRange}";
                        labelledTextBlockPixelFormat.Text = $"{runningJob.SourceInfo.ColorInfo.PixelFormat}   ⟶   {outputFile.ColorInfo.PixelFormat}";
                        labelledTextBlockBitsPerPixel.Text = $"{runningJob.SourceInfo.BitsPerPixel:0.00}   ⟶   {outputFile.BitsPerPixel:0.00}";

                        long previousVideoSize = runningJob.SourceInfo.Bitrate.Bps * Convert.ToInt64(runningJob.SourceInfo.Duration.TotalSeconds) / 8;
                        long previousVideoSizePercentage = previousVideoSize * 100 / runningJob.SourceInfo.Size;
                        long newVideoSize = outputFile.Bitrate.Bps * Convert.ToInt64(outputFile.Duration.TotalSeconds) / 8;
                        long newVideoSizePercentage = newVideoSize * 100 / outputFile.Size;
                        labelledTextBlockFileSize.ToolTip = $"Video: {previousVideoSizePercentage}%   ⟶   {newVideoSizePercentage}%\nAudio: {100 - previousVideoSizePercentage}%   ⟶   {100 - newVideoSizePercentage}%";
                    }
                }
            }

            runningJob.State = JobState.Completed;

            // Complete this job and run the next one, if present
            completedJobs.Add(runningJob);
            OnConversionEnded();
        }

        private void ConversioAborted(string errorMessage)
        {
            if (progressBarConvertProgress.Value == 0) // Error while starting encoding process
            {
                new MessageBoxWindow($"Error while starting the conversion process:\n\n\"{errorMessage}\"", "FF Video Converter").ShowDialog();
            }
            else // Error during encoding process
            {
                new MessageBoxWindow($"The encoder reported the following error during the encoding process:\n\n\"{errorMessage}\"", "FF Video Converter").ShowDialog();
            }

            progressBarConvertProgress.Value = 0;
            labelledtextBlockProgress.Text = "Conversion failed!";
            runningJob.State = JobState.Failed;
            runningJob.ConversionResults.Add(new ConversioResult("Error", errorMessage));

            // Complete this job and run the next one, if present
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

        private async void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            if (new QuestionBoxWindow("Are you sure you want to cancel current process?\nThis might take some seconds, but will generate a playable video up to the current frame", "Confirm cancellation").ShowDialog() == true)
            {
                labelledtextBlockProgress.Label = null;
                labelledtextBlockProgress.Text = "Stopping...";
                await ffmpegEngine.CancelConversion();
                progressBarConvertProgress.ClearValue(ForegroundProperty);
                DoubleAnimation progressAnimation = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                labelledtextBlockProgress.Text = "Conversion canceled";
                labelledtextBlockOutputSize.Text = "";
                buttonPauseResume.Content = "❚❚";

                runningJob.State = JobState.Canceled;
                runningJob.ConversionResults.Add(new ConversioResult("Duration", runningJob.SourceInfo.Duration.ToFormattedString(true)));
                runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{runningJob.SourceInfo.Codec}"));
                runningJob.ConversionResults.Add(new ConversioResult("Framerate", $"{runningJob.SourceInfo.Framerate} fps"));
                runningJob.ConversionResults.Add(new ConversioResult("Bitrate", $"{runningJob.SourceInfo.Bitrate} Kbps"));
                runningJob.ConversionResults.Add(new ConversioResult("Resolution", runningJob.SourceInfo.Resolution.ToString()));
                runningJob.ConversionResults.Add(new ConversioResult("Aspect ratio", runningJob.SourceInfo.Resolution.AspectRatio.ToString()));
                runningJob.ConversionResults.Add(new ConversioResult("Size", runningJob.SourceInfo.Size.ToBytesString()));

                completedJobs.Add(runningJob);
                OnConversionEnded();
            }
        }

        private void ButtonKill_Click(object sender, RoutedEventArgs e)
        {
            if (new QuestionBoxWindow("Are you sure you want to cancel current process?", "Confirm cancellation").ShowDialog() == true)
            {
                ffmpegEngine.StopConversion();
                progressBarConvertProgress.ClearValue(ForegroundProperty);
                DoubleAnimation progressAnimation = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
                progressBarConvertProgress.BeginAnimation(ProgressBar.ValueProperty, progressAnimation);
                labelledtextBlockProgress.Text = "Conversion canceled";
                labelledtextBlockOutputSize.Text = "";
                buttonPauseResume.Content = "❚❚";

                runningJob.State = JobState.Canceled;
                runningJob.ConversionResults.Add(new ConversioResult("Duration", runningJob.SourceInfo.Duration.ToFormattedString(true)));
                runningJob.ConversionResults.Add(new ConversioResult("Codec", $"{runningJob.SourceInfo.Codec}"));
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
            runningJob = null;
            queueWindow.RunningJob = null;

            if (queueWindow.QueueActive)
            {
                // Run next job, if present
                if (queuedJobs.Count > 0)
                {
                    RunJob(queuedJobs[0]);
                    queueWindow.RunningJob = queuedJobs[0];
                    queuedJobs.RemoveAt(0);
                }
                else 
                {
                    AllowSleepMode();
                    Title = "AVC to HEVC Converter";
                    TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    this.PlayStoryboard("ProgressAnimationOut");
                    buttonStart.IsEnabled = true;
                    buttonPreview.IsEnabled = true;
                    buttonPauseResume.IsEnabled = false;
                    buttonStop.IsEnabled = false;
                    buttonKill.IsEnabled = false;

                    if (queueWindow.QueueCompletedAction == QueueCompletedAction.Sleep)
                    {
                        SetSuspendState(false, true, true);
                    }
                    else if (queueWindow.QueueCompletedAction == QueueCompletedAction.Shutdown)
                    {
                        var psi = new ProcessStartInfo("shutdown", "/s /t 0");
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;
                        Process.Start(psi);
                    }
                }
            }
            else
            {
                AllowSleepMode();
                Title = "AVC to HEVC Converter";
                TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                this.PlayStoryboard("ProgressAnimationOut");
                buttonStart.IsEnabled = true;
                buttonPreview.IsEnabled = true;
                buttonPauseResume.IsEnabled = false;
                buttonStop.IsEnabled = false;
                buttonKill.IsEnabled = false;
            }

            textBlockMemory.Text = "";
            labelledtextBlockProgress.Label = null;
            labelledtextBlockOutputSize.Label = null;
            this.PlayStoryboard("OpenOutputButtonAnimationIn");
        }

        #endregion

        #region Media Player

        private void MediaElement_DragEnter(object sender, DragEventArgs e)
        {
            this.PlayStoryboard("DragOverAnimation");
        }

        private void MediaElement_DragLeave(object sender, DragEventArgs e)
        {
            this.StopStoryboard("DragOverAnimation");
        }

        private void MediaPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (isMediaOpen)
            {
                if (mediaPlayer.IsPlaying) mediaPlayer.Pause();
                else mediaPlayer.Play();
            }
            else
            {
                ButtonOpen_Click(null, null);
            }
        }

        private void ButtonExpand_Click(object sender, RoutedEventArgs e)
        {
            if (isPlayerExpanded)
            {
                this.PlayStoryboard("ExpandMediaPlayerRev");
                if (tabItemCut.IsSelected || tabItemResize.IsSelected)
                {
                    this.PlayStoryboard("ShowBottomUI");
                    if (comboBoxEncoder.SelectedIndex > 0) // If encoder is not native, animate preview button too
                    {
                        this.PlayStoryboard("PreviewButtonAnimationIn");
                    }
                }
                isPlayerExpanded = false;
            }
            else
            {
                this.PlayStoryboard("ExpandMediaPlayer");
                if (tabItemCut.IsSelected || tabItemResize.IsSelected)
                {
                    this.PlayStoryboard("HideBottomUI");
                    if (comboBoxEncoder.SelectedIndex > 0) // If encoder is not native, animate preview button too
                    {
                        this.PlayStoryboard("PreviewButtonAnimationOut");
                    }
                }
                isPlayerExpanded = true;
            }
        }

        #endregion

        private void ButtonShowQueue_Click(object sender, RoutedEventArgs e)
        {
            queueWindow.Show();
            queueWindow.Activate();
        }

        private void QueueWindow_QueueStarted()
        {
            buttonShowQueue.Content = "Queue (running)";

            // If there are no jobs running, start the next one
            if ((runningJob == null || (runningJob.State != JobState.Running && runningJob.State != JobState.Paused)) && queuedJobs.Count > 0)
            {
                RunJob(queuedJobs[0]);
                queueWindow.RunningJob = queuedJobs[0];
                queuedJobs.RemoveAt(0);
            }
        }

        private void ButtonOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            completedWindow.Show();
            completedWindow.Activate();
        }

        #region Utility

        private bool IsCutEnabled()
        {
            if (mediaInfo == null) return false;
            TimeSpan duration = timeIntervalCollection.TotalDuration;
            return duration > TimeSpan.Zero && Math.Abs(duration.TotalSeconds - mediaInfo.Duration.TotalSeconds) >= 0.01; // duration will be accurate up to 0.01 seconds, so the two timespan can't be compared directly
        }

        private void BlockSleepMode()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }

        private void AllowSleepMode()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }

        private void AddEncodeSegment(TimeSpan? start, TimeSpan? end)
        {
            Controls.EncodeSegmentControl encodeSegmentControl = new Controls.EncodeSegmentControl(mediaPlayer, mediaInfo);
            encodeSegmentControl.ShowKeyframesSuggestions = comboBoxEncoder.SelectedItem is CopyEncoder;
            if (start != null) encodeSegmentControl.Start = start.Value;
            if (end != null) encodeSegmentControl.End = end.Value;
            encodeSegmentControl.StartChanged += s =>
            {
                mediaPlayer.Position = s.Start;
                OnDurationChanged();
            };
            encodeSegmentControl.EndChanged += s =>
            {
                mediaPlayer.Position = s.End;
                OnDurationChanged();
            };
            encodeSegmentControl.Removed += s =>
            {
                if (cutInsideControlsList.Items.Count <= 1 && checkBoxFade.Width == 95)
                {
                    this.PlayStoryboard("CheckBoxFadeAnimationOut");
                }
                OnDurationChanged();
            };

            cutInsideControlsList.Items.Add(encodeSegmentControl);

            if (cutInsideControlsList.Items.Count > 1 && comboBoxEncoder.SelectedItem is not CopyEncoder && checkBoxFade.Width == 0)
            {
                this.PlayStoryboard("CheckBoxFadeAnimationIn");
            }
        }

        private void OnDurationChanged()
        {
            timeIntervalCollection = new TimeIntervalCollection(mediaInfo.Duration);

            foreach (Controls.EncodeSegmentControl encodeSegmentControl in cutInsideControlsList.Items)
            {
                timeIntervalCollection.Add(encodeSegmentControl.Start, encodeSegmentControl.End);
            }

            cutPreviewControl.UpdateIntervalCollection(timeIntervalCollection, mediaInfo.Duration);
            mediaPlayer.TimeIntervalCollection = timeIntervalCollection;

            if (IsCutEnabled())
            {
                textBlockOutputDuration.Text = timeIntervalCollection.TotalDuration.ToFormattedString(true);
            }
            else
            {
                textBlockOutputDuration.Text = mediaInfo.Duration.ToFormattedString(true);
            }

            long size = (long)(numericUpDownBitrate.Value * 1000 / 8 * timeIntervalCollection.TotalDuration.TotalSeconds);
            textBlockTargetSize.Text = $"Video encoded size: {size.ToBytesString()}";
        }

        private void UpdateVideoFilter()
        {
            if (isMediaOpen)
            {
                string filter = "";

                if (sliderBrightness.Value != 0 || sliderContrast.Value != 0 || sliderSaturation.Value != 0 || sliderGamma.Value != 0 || sliderRed.Value != 0 || sliderGreen.Value != 0 || sliderBlue.Value != 0)
                {
                    Filters.EqFilter eq = new Filters.EqFilter()
                    {
                        Brightness = sliderBrightness.Value,
                        Contrast = sliderContrast.Value,
                        Saturation = sliderSaturation.Value,
                        Gamma = sliderGamma.Value,
                        GammaRed = sliderRed.Value,
                        GammaGreen = sliderGreen.Value,
                        GammaBlue = sliderBlue.Value,
                    };
                    filter = eq.GetFilter();
                }

                if (comboBoxRotation.SelectedIndex != 0)
                {
                    Filters.RotationFilter rotation = new Filters.RotationFilter(comboBoxRotation.SelectedIndex);
                    if (filter == "")
                    {
                        filter = rotation.GetFilter();
                    }
                    else
                    {
                        filter += $",{rotation.GetFilter()}";
                    }
                }

                mediaPlayer.VideoFilter = filter;
            }
        }

        #endregion

        #region Native methods

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040, // Should only be used by applications that must perform critical background processing while the computer appears to be sleeping. This value must be specified with ES_CONTINUOUS
            ES_CONTINUOUS = 0x80000000,        // Informs the system that the state being set should remain in effect until the next call that uses ES_CONTINUOUS and one of the other state flags is cleared
            ES_DISPLAY_REQUIRED = 0x00000002,  // Forces the display to be on by resetting the display idle timer
            ES_SYSTEM_REQUIRED = 0x00000001    // Forces the system to be in the working state by resetting the system idle timer.
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);

        [DllImport("kernel32.dll")]
        static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

        #endregion

        // CURRENTLY NOT USED
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*if (tabItemAdvanced.IsSelected && mediaInfo != null)
            {
                Encoder encoder = (Encoder)comboBoxEncoder.SelectedItem;
                encoder.Preset = (Preset)comboBoxPreset.SelectedIndex;
                if (radioButtonBitrate.IsChecked == true)
                {
                    int audioBitrate = 0;
                    foreach (var audioTrack in mediaInfo.AudioTracks)
                    {
                        if (audioTrack.Enabled) audioBitrate += audioTrack.Bitrate;
                    }
                    long desiredSize = (long)(mediaInfo.Size * sliderTargetSize.Value / 100);
                    int totalBitrate = (int)(desiredSize * 8 / mediaInfo.Duration.TotalSeconds);
                    encoder.Bitrate = (totalBitrate - audioBitrate) / 1000;
                }
                else
                {
                    encoder.Quality = (Quality)comboBoxQuality.SelectedIndex;
                }

                ConversionOptions conversionOptions = GenerateConversionOptions(encoder);
                string arguments = ffmpegEngine.BuildArgumentsString(mediaInfo, textBoxDestination.Text, conversionOptions);
                arguments = System.Text.RegularExpressions.Regex.Replace(arguments, " -(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", "\n-");
                textBoxCommandLine.Text = arguments;
            }*/
        }
    }
}