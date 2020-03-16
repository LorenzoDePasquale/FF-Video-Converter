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
                    if (!url.Contains("reddit") && !url.Contains("youtu") && !url.Contains("twitter"))
                    {
                        labelTitle.Content = "Opening network stream...";
                        MediaStream = await MediaInfo.Open(url);
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
                        comboBoxAudioQuality.Items.Add("[No Audio]");
                        comboBoxQuality.SelectedIndex = 0;
                        comboBoxAudioQuality.SelectedIndex = 0;
                        buttonOpen.Content = "Open selected quality";
                        buttonOpen.IsEnabled = true;
                        Storyboard storyboard = FindResource("ChoseQualityAnimation") as Storyboard;
                        storyboard.Begin();
                    }
                }
            }
            else
            {
                buttonOpen.IsEnabled = false;
                labelTitle.Content = $"Loading {host} video...";
                StreamInfo selectedVideo = (StreamInfo)comboBoxQuality.SelectedItem;
                MediaStream = await MediaInfo.Open(selectedVideo.Url);
                if (comboBoxAudioQuality.SelectedItem.ToString() != "[No Audio]")
                {
                    StreamInfo selectedAudio = (StreamInfo)comboBoxAudioQuality.SelectedItem;
                    MediaStream.AudioSource = selectedAudio.Url;
                    MediaStream.AudioCodec = selectedAudio.Codec;
                }
                MediaStream.Title = selectedVideo.Title;
                Close();
            }
        }
    }
}