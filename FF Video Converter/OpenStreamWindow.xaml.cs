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
                    if (url.Contains("reddit"))
                    {
                        labelTitle.Content = "Fetching Reddit post info...";
                        host = "Reddit";
                    }
                    else if (url.Contains("youtu"))
                    {
                        labelTitle.Content = "Fetching Youtube video info...";
                        host = "Youtube";
                    }
                    else if (url.Contains("twitter"))
                    {
                        labelTitle.Content = "Fetching Twitter status info...";
                        host = "Twitter";
                        comboBoxAudioQuality.Visibility = Visibility.Hidden;
                        textBlockAudioQuality.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        labelTitle.Content = "Opening network stream...";
                        MediaStream = await MediaInfo.Open(url);
                        Close();
                    }

                    VideoUrlParser videoUrlParser = null;
                    switch (host)
                    {
                        case "Reddit":
                            videoUrlParser = new RedditParser();
                            break;
                        case "Youtube":
                            videoUrlParser = new YouTubeParser();
                            break;
                        case "Twitter":
                            videoUrlParser = new TwitterParser();
                            break;
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
                }
                MediaStream.Title = selectedVideo.Title;
                Close();
            }
        }
    }
}