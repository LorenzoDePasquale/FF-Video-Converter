using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;


namespace FFVideoConverter
{
    public partial class OpenStreamWindow : Window
    {
        public MediaInfo MediaStream { get; private set; }
        private string host = "";

        public OpenStreamWindow()
        {
            InitializeComponent();

            string clipboardText = Clipboard.GetText();
            if (!String.IsNullOrEmpty(clipboardText) && clipboardText.StartsWith("http"))
            {
                textBoxURL.Text = clipboardText;
            }
        }

        #region "Title Bar controls"

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        private async void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(textBoxURL.Text)) return;

            buttonOpen.IsEnabled = false;
            if (buttonOpen.Content.ToString() == "Open URL")
            {
                string url = textBoxURL.Text;
                if (url.StartsWith("http"))
                {
                    if (!url.Contains("reddit") && !url.Contains("youtu") && !url.Contains("twitter") && !url.Contains("facebook") && !url.Contains("instagram"))
                    {
                        labelTitle.Content = "Opening network stream...";
                        try
                        {
                            MediaStream = await MediaInfo.Open(url);
                        }
                        catch (Exception)
                        {
                            new MessageBoxWindow("Failed to open media from the url\n" + url, "Error opening url").ShowDialog();
                            MediaStream = null;
                        }
                        Close();
                    }
                    else
                    {
                        VideoUrlParser videoUrlParser = null;

                        if (url.Contains("reddit"))
                        {
                            host = "Reddit";
                            labelTitle.Content = "Fetching Reddit post info...";
                            videoUrlParser = new RedditParser();
                        }
                        else if (url.Contains("youtu"))
                        {
                            host = "Youtube";
                            labelTitle.Content = "Fetching Youtube video info...";
                            videoUrlParser = new YouTubeParser();
                        }
                        else if (url.Contains("twitter"))
                        {
                            host = "Twitter";
                            labelTitle.Content = "Fetching Twitter status info...";
                            videoUrlParser = new TwitterParser();
                            comboBoxAudioQuality.Visibility = Visibility.Hidden;
                            textBlockAudioQuality.Visibility = Visibility.Hidden;
                        }
                        else if (url.Contains("facebook"))
                        {
                            host = "Facebook";
                            labelTitle.Content = "Fetching Facebook post info...";
                            videoUrlParser = new FacebookParser();
                            comboBoxAudioQuality.Visibility = Visibility.Hidden;
                            textBlockAudioQuality.Visibility = Visibility.Hidden;
                        }
                        else if (url.Contains("instagram"))
                        {
                            host = "Instagram";
                            labelTitle.Content = "Fetching Instagram post info...";
                            videoUrlParser = new InstagramParser();
                        }

                        try
                        {
                            foreach (var item in await videoUrlParser.GetVideoList(url))
                            {
                                if (!item.IsAudio)
                                {
                                    comboBoxQuality.Items.Add(item);
                                }
                                else
                                {
                                    comboBoxAudioQuality.Items.Add(item);
                                }
                                labelTitle.Content = item.Title;
                            }
                        }
                        catch (Exception)
                        {
                            new MessageBoxWindow("Failed to retreive media info from the url\n" +url, "Error parsing url").ShowDialog();
                            buttonOpen.IsEnabled = true;
                            return;
                        }

                        comboBoxQuality.SelectedIndex = 0;
                        if (comboBoxAudioQuality.Items.Count > 0)
                        {
                            comboBoxAudioQuality.SelectedIndex = 0;
                            comboBoxAudioQuality.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            comboBoxAudioQuality.Visibility = Visibility.Hidden;
                        }
                        buttonOpen.Content = "Open selected quality";
                        if (comboBoxQuality.Items.Count == 1 && comboBoxAudioQuality.Items.Count == 1) //Only one option -> automatically select that option
                        {
                            ButtonOpen_Click(null, null);
                        }
                        else
                        {
                            buttonOpen.IsEnabled = true;
                            Storyboard storyboard = FindResource("ChoseQualityAnimation") as Storyboard;
                            storyboard.Begin();
                        }
                    }
                }
            }
            else
            {
                buttonOpen.IsEnabled = false;
                labelTitle.Content = $"Loading {host} video...";
                StreamInfo selectedVideo = (StreamInfo)comboBoxQuality.SelectedItem;
                try
                {
                    MediaStream = await MediaInfo.Open(selectedVideo.Url);
                }
                catch (Exception ex)
                {
                    new MessageBoxWindow(ex.Message, "Error opening selected url").ShowDialog();
                    Close();
                    return;
                }
                if (comboBoxAudioQuality.Items.Count > 0)
                {
                    StreamInfo selectedAudio = (StreamInfo)comboBoxAudioQuality.SelectedItem;
                    MediaStream.AudioSource = selectedAudio.Url;
                    MediaStream.AudioCodec = selectedAudio.Codec;
                    new MessageBoxWindow("The audio stream relative to this video is separate, therefore the integrated player will play the video without audio.\nWhen converting or downloading this video however, audio and video will be muxed toghether", "Info").ShowDialog();
                }
                MediaStream.Title = selectedVideo.Title;
                Close();
            }
        }
    }
}