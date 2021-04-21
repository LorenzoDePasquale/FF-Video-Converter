using System;
using System.Windows;
using System.Windows.Media.Animation;


namespace FFVideoConverter
{
    public partial class OpenStreamWindow : Window
    {
        public MediaInfo MediaStream { get; private set; }
        public string PlayerSource { get; private set; }
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
                        titleBar.Text = "Opening network stream...";
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
                            titleBar.Text = "Fetching Reddit post info...";
                            videoUrlParser = new RedditParser();
                        }
                        else if (url.Contains("youtu"))
                        {
                            host = "Youtube";
                            titleBar.Text = "Fetching Youtube video info...";
                            videoUrlParser = new YouTubeParser();
                        }
                        else if (url.Contains("twitter"))
                        {
                            host = "Twitter";
                            titleBar.Text = "Fetching Twitter status info...";
                            videoUrlParser = new TwitterParser();
                            comboBoxAudioQuality.Visibility = Visibility.Hidden;
                            textBlockAudioQuality.Visibility = Visibility.Hidden;
                        }
                        else if (url.Contains("facebook"))
                        {
                            host = "Facebook";
                            titleBar.Text = "Fetching Facebook post info...";
                            videoUrlParser = new FacebookParser();
                            comboBoxAudioQuality.Visibility = Visibility.Hidden;
                            textBlockAudioQuality.Visibility = Visibility.Hidden;
                        }
                        else if (url.Contains("instagram"))
                        {
                            host = "Instagram";
                            titleBar.Text = "Fetching Instagram post info...";
                            videoUrlParser = new InstagramParser();
                        }

                        GetVideoList(videoUrlParser, url);
                    }
                }
                else
                {
                    new MessageBoxWindow("Enter a valid url", "FF Video Converter").ShowDialog();
                }
            }
            else
            {
                OpenVideo((StreamInfo)comboBoxQuality.SelectedItem);
            }
        }

        private async void GetVideoList(VideoUrlParser videoUrlParser, string url)
        {
            try
            {
                var videoList = await videoUrlParser.GetVideoList(url);
                if (videoUrlParser is YouTubeParser youTubeParser)
                {
                    PlayerSource = await youTubeParser.GetMuxedSource(url);
                }
                if (videoList.Count == 1)
                {
                    OpenVideo(videoList[0]);
                    return;
                }
                else
                {
                    for (int i = 0; i < videoList.Count; i++)
                    {
                        if (!videoList[i].IsAudio)
                        {
                            comboBoxQuality.Items.Add(videoList[i]);
                        }
                        else
                        {
                            comboBoxAudioQuality.Items.Add(videoList[i]);
                        }
                        titleBar.Text = videoList[i].Title;
                    }
                }
            }
            catch (Exception)
            {
                new MessageBoxWindow("Failed to retreive media info from the url\n" + url, "Error parsing url").ShowDialog();
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
            buttonOpen.Content = "Open";
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

        private async void OpenVideo(StreamInfo video)
        {
            buttonOpen.IsEnabled = false;
            titleBar.Text = $"Loading {host} video...";
            try
            {
                if (comboBoxAudioQuality.Items.Count > 0)
                {
                    StreamInfo selectedAudio = (StreamInfo)comboBoxAudioQuality.SelectedItem;
                    MediaStream = await MediaInfo.Open(video.Url, selectedAudio.Url);
                }
                else
                {
                    PlayerSource = null;
                    MediaStream = await MediaInfo.Open(video.Url);
                }
            }
            catch (Exception ex)
            {
                new MessageBoxWindow(ex.Message, "Error opening selected url").ShowDialog();
                Close();
                return;
            }

            MediaStream.Title = video.Title;
            Close();
        }
    }
}