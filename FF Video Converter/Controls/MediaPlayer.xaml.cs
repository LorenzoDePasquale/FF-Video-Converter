using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;


namespace FFVideoConverter.Controls
{
    public partial class MediaPlayer : UserControl
    {
        public struct CropData
        {
            public int Left { get; }
            public int Top { get; }
            public int Right { get; }
            public int Bottom { get; }

            public CropData(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        public TimeIntervalCollection TimeIntervalCollection { get; set; }
        public string VideoFilter 
        { 
            get => playerMediaOptions.VideoFilter;
            set
            {
                playerMediaOptions.VideoFilter = value;
                //Make changes visible if the player is paused
                if (mediaElement.IsPaused)
                {
                    mediaElement.ChangeMedia();
                }
            }
        }
        public bool IsPlaying => mediaElement.IsPlaying;
        public bool CropActive
        {
            get => canvasCropVideo.Visibility == Visibility.Visible;
            set
            {
                if (value)
                {
                    canvasCropVideo.Visibility = Visibility.Visible;
                }
                else
                {
                    canvasCropVideo.Visibility = Visibility.Hidden;
                }
            }
        }
        public CropData Crop
        {
            get
            {
                double cropTop = Canvas.GetTop(rectangleCropVideo) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropLeft = Canvas.GetLeft(rectangleCropVideo) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                double cropBottom = (canvasCropVideo.ActualHeight - rectangleCropVideo.Height - Canvas.GetTop(rectangleCropVideo)) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropRight = (canvasCropVideo.ActualWidth - rectangleCropVideo.Width - Canvas.GetLeft(rectangleCropVideo)) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                return new CropData((int)cropLeft, (int)cropTop, (int)cropRight, (int)cropBottom);
            }
            set
            {
                double left = value.Left * canvasCropVideo.ActualWidth / mediaInfo.Width;
                double right = value.Right * canvasCropVideo.ActualWidth / mediaInfo.Width;
                double top = value.Top * canvasCropVideo.ActualHeight / mediaInfo.Height;
                double bottom = value.Bottom * canvasCropVideo.ActualHeight / mediaInfo.Height;
                double width = canvasCropVideo.ActualWidth - left - right;
                double height = canvasCropVideo.ActualHeight - top - bottom;
                UpdateRectangleVisuals(left, top, width, height);
            }
        }
        public TimeSpan Position
        {
            get => mediaElement.ActualPosition.Value;
            set
            {
                playerSlider.Value = value.TotalSeconds;
            }
        }

        public event Action<CropData> CropChanged;
        public event RoutedEventHandler Click, ButtonExpandClick;

        private enum HitLocation
        {
            None, Body, UpperLeft, UpperRight, LowerRight, LowerLeft, Left, Right, Top, Bottom
        };

        const int RECT_MIN_SIZE = 20;
        Point LastPoint;
        HitLocation MouseHitLocation = HitLocation.None;
        bool isSeeking = false;
        bool isMediaOpen = false;
        bool isDragging = false;
        bool sliderUserInput = true;
        bool mouseDown = false;
        bool wasPlaying = false;
        MediaInfo mediaInfo;
        Unosquare.FFME.Common.MediaOptions playerMediaOptions;
        Unosquare.FFME.Common.MediaInfo playerMediaInfo;


        public MediaPlayer()
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return; //This constructor doesn't work with VS designer

            InitializeComponent();

            mediaElement.ScrubbingEnabled = false;
            mediaElement.PositionChanged += MediaElementInput_PositionChanged;
            mediaElement.MediaOpening += (sender, e) =>
            {
                e.Options.IsSubtitleDisabled = true;
                e.Options.DecoderParams.EnableFastDecoding = true;
                playerMediaOptions = e.Options;
                playerMediaInfo = e.Info;
            };
        }

        public async Task Open(MediaInfo mediaInfo, string playerSource = null)
        {
            isMediaOpen = false;
            this.mediaInfo = mediaInfo;
            await mediaElement.Open(new Uri(playerSource ?? mediaInfo.Source));
            mediaElement.Background = Brushes.Black;
            gridMediaControls.Visibility = Visibility.Visible;
            playerSlider.Maximum = mediaElement.PlaybackEndTime.Value.TotalSeconds;
            playerSlider.Value = 0;
            textBlockPlayerPosition.Text = $"00:00:00 / {mediaElement.PlaybackEndTime.Value.ToFormattedString()}";
            buttonPlayPause.Content = " ▶️";
            SetupAudioTracks();
            isMediaOpen = true;
        }

        public async Task Play()
        {
            buttonPlayPause.Content = " ❚❚";
            _ = await mediaElement.Play();
        }

        public async Task Pause()
        {
            buttonPlayPause.Content = " ▶️";
            _ = await mediaElement.Pause();
        }

        private void SetupAudioTracks()
        {
            listViewPlayerTrackPicker.SelectionChanged -= ListViewPlayerTrackPicker_SelectionChanged;
            listViewPlayerTrackPicker.Items.Clear();

            if (mediaInfo.AudioTracks.Count > 1)
            {
                int[] streamIndices = new int[mediaInfo.AudioTracks.Count];
                for (int i = 0; i < mediaInfo.AudioTracks.Count; i++)
                {
                    AudioTrack audioTrack = mediaInfo.AudioTracks[i];
                    string trackLabel = audioTrack.Title != "" ? audioTrack.Title : audioTrack.Codec;
                    if (audioTrack.Channels > 2)
                        trackLabel += $" {audioTrack.ChannelLayout}";
                    if (audioTrack.Language != "")
                        trackLabel += $" [{audioTrack.Language}]";
                    listViewPlayerTrackPicker.Items.Add(trackLabel);
                    streamIndices[i] = audioTrack.StreamIndex;

                    if (audioTrack.StreamIndex == mediaElement.AudioStreamIndex)
                        listViewPlayerTrackPicker.SelectedIndex = i;
                }
                listViewPlayerTrackPicker.Tag = streamIndices;
                buttonAudioTrack.Visibility = Visibility.Visible;
            }
            else
            {
                buttonAudioTrack.Visibility = Visibility.Hidden;
            }

            listViewPlayerTrackPicker.SelectionChanged += ListViewPlayerTrackPicker_SelectionChanged;
        }

        private async void MediaElementInput_PositionChanged(object sender, Unosquare.FFME.Common.PositionChangedEventArgs e)
        {
            if (!isSeeking)
            {
                sliderUserInput = false;
                if (TimeIntervalCollection != null && TimeIntervalCollection.Count > 0 && !TimeIntervalCollection.Contains(e.Position) && mediaElement.IsPlaying)
                {

                    await mediaElement.Pause();
                    if (e.Position > TimeIntervalCollection.ActualEnd)
                    {
                        await mediaElement.Seek(TimeIntervalCollection.ActualStart);
                    }
                    else if (e.Position < TimeIntervalCollection.ActualStart)
                    {
                        await mediaElement.Seek(TimeIntervalCollection.ActualStart);
                    }
                    else
                    {
                        await mediaElement.Seek(TimeIntervalCollection.GetClosestTimeSpanAfter(e.Position));
                    }
                    await mediaElement.Play();
                }
                else
                {
                    playerSlider.Value = e.Position.TotalSeconds;
                    textBlockPlayerPosition.Text = $"{e.Position.ToFormattedString()} / {mediaElement.PlaybackEndTime.Value.ToFormattedString()}";
                }
                sliderUserInput = true;
            }
        }

        private void MediaElementInput_MouseEnter(object sender, MouseEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                this.PlayStoryboard("mediaControlsAnimationIn");
            }
        }

        private void MediaElementInput_MouseLeave(object sender, MouseEventArgs e)
        {
            if (mediaElement.Source != null)
            {
                this.PlayStoryboard("mediaControlsAnimationOut");
                this.PlayStoryboard("AudioTrackPickerAnimationOut");
            }
        }

        private void MediaElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseDown = true;
        }

        private void MediaElement_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseDown)
            {
                mouseDown = false;
                Click?.Invoke(this, e);
            }
        }

        private void ButtonPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.IsPaused)
            {
                Play();
            }
            else
            {
                Pause();
            }
        }

        private async void ButtonNextFrame_Click(object sender, RoutedEventArgs e)
        {
            await Pause();
            mediaElement.StepForward();
        }

        private async void ButtonPreviousFrame_Click(object sender, RoutedEventArgs e)
        {
            await Pause();
            mediaElement.StepBackward();
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
                mediaElement.Seek(TimeSpan.FromSeconds(playerSlider.Value));
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
            ButtonExpandClick?.Invoke(this, e);
        }

        private void ButtonMute_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.IsMuted)
            {
                SliderVolume_ValueChanged(null, null);
            }
            else
            {
                mediaElement.IsMuted = true;
                buttonMute.Content = "🔇";
            }
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaElement.IsMuted = false;
            mediaElement.Volume = sliderVolume.Value;
            if (sliderVolume.Value < 0.5)
                buttonMute.Content = sliderVolume.Value == 0 ? "🔇" : "🔉";
            else
                buttonMute.Content = "🔊";
        }

        private void ButtonMute_MouseEnter(object sender, MouseEventArgs e)
        {
            this.PlayStoryboard("VolumeSliderAnimationIn");
        }

        private void StackPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            this.PlayStoryboard("VolumeSliderAnimationOut");
        }

        private void ButtonAudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (listViewPlayerTrackPicker.Opacity == 1)
            {
                this.PlayStoryboard("AudioTrackPickerAnimationOut");
            }
            else if (listViewPlayerTrackPicker.Opacity == 0)
            {
                this.PlayStoryboard("AudioTrackPickerAnimationIn");
            }
        }

        private void ListViewPlayerTrackPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isMediaOpen)
            {
                int[] streamIndices = (int[])listViewPlayerTrackPicker.Tag;
                playerMediaOptions.AudioStream = playerMediaInfo.Streams[streamIndices[listViewPlayerTrackPicker.SelectedIndex]];
                mediaElement.ChangeMedia();
            }
        }

        private void CanvasCropVideo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MouseHitLocation = SetHitType(rectangleCropVideo, Mouse.GetPosition(canvasCropVideo));
            SetMouseCursor();
            if (MouseHitLocation == HitLocation.None) return;

            LastPoint = Mouse.GetPosition(canvasCropVideo);
            isDragging = true;
            gridMediaControls.IsHitTestVisible = false;
            if (gridMediaControls.Opacity == 1)
            {
                this.PlayStoryboard("mediaControlsAnimationOut");
            }
        }

        private void CanvasCropVideo_MouseMove(object sender, MouseEventArgs e)
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
                        new_y = canvasCropVideo.ActualHeight - new_height;
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
                    if (MouseHitLocation == HitLocation.Top)
                    {
                        new_y -= offset_y;
                        new_height += offset_y;
                    }
                    else new_height = RECT_MIN_SIZE;
                }

                //Update the rectangle and the black border that hides the cropped part
                UpdateRectangleVisuals(new_x, new_y, new_width, new_height);

                //Update crop data
                double cropTop = new_y * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropLeft = new_x * mediaInfo.Width / canvasCropVideo.ActualWidth;
                double cropBottom = (canvasCropVideo.ActualHeight - new_height - new_y) * mediaInfo.Height / canvasCropVideo.ActualHeight;
                double cropRight = (canvasCropVideo.ActualWidth - new_width - new_x) * mediaInfo.Width / canvasCropVideo.ActualWidth;
                CropChanged?.Invoke(new CropData((int)cropLeft, (int)cropTop, (int)cropRight, (int)cropBottom));

                //Save the mouse new location.
                LastPoint = point;
            }
            else
            {
                MouseHitLocation = SetHitType(rectangleCropVideo, Mouse.GetPosition(canvasCropVideo));
                SetMouseCursor();
            }
        }

        private void CanvasCropVideo_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            gridMediaControls.IsHitTestVisible = true;
            if (gridMediaControls.Opacity == 0)
            {
                this.PlayStoryboard("mediaControlsAnimationIn");
            }
            Cursor = Cursors.Arrow;
        }

        private void CanvasCropVideo_MouseLeave(object sender, MouseEventArgs e)
        {
            CanvasCropVideo_MouseUp(null, null);
        }

        private void CanvasCropVideo_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width != 0)
            {
                double new_x = e.NewSize.Width * Canvas.GetLeft(rectangleCropVideo) / e.PreviousSize.Width;
                double new_y = e.NewSize.Height * Canvas.GetTop(rectangleCropVideo) / e.PreviousSize.Height;
                double new_width = e.NewSize.Width * rectangleCropVideo.Width / e.PreviousSize.Width;
                double new_height = e.NewSize.Height * rectangleCropVideo.Height / e.PreviousSize.Height;

                UpdateRectangleVisuals(new_x, new_y, new_width, new_height);
            }
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

        private void UpdateRectangleVisuals(double left, double top, double width, double height)
        {
            // Update the rectangle.
            Canvas.SetLeft(rectangleCropVideo, left);
            Canvas.SetTop(rectangleCropVideo, top);
            rectangleCropVideo.Width = width;
            rectangleCropVideo.Height = height;

            //Update the black border that hides the cropped part
            borderCropVideo.BorderThickness = new Thickness(left, top, canvasCropVideo.ActualWidth - left - width, canvasCropVideo.ActualHeight - top - height);
        }

        private void SetMouseCursor()
        {
            Cursor = MouseHitLocation switch
            {
                HitLocation.None => Cursors.Arrow,
                HitLocation.Body => Cursors.SizeAll,
                HitLocation.UpperLeft or HitLocation.LowerRight => Cursors.SizeNWSE,
                HitLocation.LowerLeft or HitLocation.UpperRight => Cursors.SizeNESW,
                HitLocation.Top or HitLocation.Bottom => Cursors.SizeNS,
                HitLocation.Left or HitLocation.Right => Cursors.SizeWE
            };
        }
    }
}