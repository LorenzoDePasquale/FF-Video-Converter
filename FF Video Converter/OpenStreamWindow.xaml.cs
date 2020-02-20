using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Xml;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace FFVideoConverter
{
    public partial class OpenStreamWindow : Window
    {
        public MediaInfo MediaStream { get; private set; }

        private struct StreamInfo
        {
            public bool IsAudio { get; }
            public string Url { get; }
            public string Title { get; }
            public string DisplayValue { get; }
            public long Size { get; }

            public StreamInfo(string url, bool isAudio, string title, string displayValue, long size)
            {
                Url = url;
                IsAudio = isAudio;
                Title = title;
                DisplayValue = displayValue;
                Size = size;
            }

            public override string ToString()
            {
                return DisplayValue;
            }
        }

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
                        labelTitle.Content = "Fetching Reddit video info...";
                        host = "Reddit";
                    }
                    else if (url.Contains("youtu"))
                    {
                        labelTitle.Content = "Fetching Youtube video info...";
                        host = "Youtube";
                    }
                    else
                    {
                        labelTitle.Content = "Opening network stream...";
                        MediaStream = await MediaInfo.Open(url);
                        Close();
                    }

                    foreach (var item in await (host == "Youtube" ? GetYoutubeVideos(url) : GetRedditVideos(url)))
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

        private async Task<List<StreamInfo>> GetRedditVideos(string url)
        {
            using (HttpClient client = new HttpClient())
            using (Stream stream = await client.GetStreamAsync(url + ".json").ConfigureAwait(false))
            using (JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false))
            {
                string title = document.RootElement[0].GetProperty("data").GetProperty("children")[0].GetProperty("data").GetProperty("title").GetString();
                string dashDocumentUrl = document.RootElement[0].GetProperty("data").GetProperty("children")[0].GetProperty("data").GetProperty("media").GetProperty("reddit_video").GetProperty("dash_url").GetString();
                string dashContent = await client.GetStringAsync(dashDocumentUrl).ConfigureAwait(false);
                string baseVideoUrl = dashDocumentUrl.Substring(0, dashDocumentUrl.LastIndexOf('/') + 1);
                List<StreamInfo> videoList = new List<StreamInfo>();
                string displayValue;
                foreach (var dashInfo in GetDashInfos(dashContent))
                {
                    displayValue = $"{dashInfo.label} - {MainWindow.GetBytesReadable(dashInfo.size)}";
                    videoList.Add(new StreamInfo(baseVideoUrl + dashInfo.dash, dashInfo.dash == "audio", title, displayValue, dashInfo.size));

                }
                videoList.Sort((x, y) => { return y.Size.CompareTo(x.Size); });
                return videoList;
            }

            List<(string dash, string label, long size)> GetDashInfos(string dashContent)
            {
                List<(string, string, long)> dashUrls = new List<(string, string, long)>();
                XmlDocument document = new XmlDocument();
                document.LoadXml(dashContent);
                XmlNode periodNode = document.DocumentElement.FirstChild;
                string durationValue = periodNode.Attributes["duration"].Value.Replace("PT", "").Replace("S", "");
                double duration = Double.Parse(durationValue, System.Globalization.CultureInfo.InvariantCulture);
                foreach (XmlNode adaptationSet in periodNode.ChildNodes)
                {
                    foreach (XmlNode representation in adaptationSet.ChildNodes)
                    {
                        int bitrate = Convert.ToInt32(representation.Attributes["bandwidth"].Value);
                        long size = (long)(bitrate * duration / 8);
                        string label = representation.Attributes["height"]?.Value + "p" ?? (bitrate / 1000) + " Kbps";
                        string dash = representation.SelectSingleNode("*[local-name()='BaseURL']").InnerText;
                        dashUrls.Add((dash, label, size));
                    }
                }
                return dashUrls;
            }
        }

        private async Task<List<StreamInfo>> GetYoutubeVideos(string url)
        {
            YoutubeClient youtubeClient = new YoutubeClient();
            string videoId = YoutubeClient.ParseVideoId(url);
            var video = await youtubeClient.GetVideoAsync(videoId).ConfigureAwait(false);
            var mediaStreamsInfoSet = await youtubeClient.GetVideoMediaStreamInfosAsync(videoId).ConfigureAwait(false);
            List<StreamInfo> videoList = new List<StreamInfo>();
            string displayValue;
            foreach (VideoStreamInfo videoStreamInfo in mediaStreamsInfoSet.Video)
            {
                displayValue = $"{videoStreamInfo.VideoQualityLabel} ({videoStreamInfo.VideoEncoding.ToString()}) - {MainWindow.GetBytesReadable(videoStreamInfo.Size)}";
                videoList.Add(new StreamInfo(videoStreamInfo.Url, false, video.Title, displayValue, videoStreamInfo.Size));
            }
            foreach (AudioStreamInfo audioStreamInfo in mediaStreamsInfoSet.Audio)
            {
                displayValue = $"{audioStreamInfo.Bitrate / 1000} Kbps ({audioStreamInfo.AudioEncoding.ToString()}) - {MainWindow.GetBytesReadable(audioStreamInfo.Size)}";
                videoList.Add(new StreamInfo(audioStreamInfo.Url, true, video.Title, displayValue, audioStreamInfo.Size));
            }
            videoList.Sort((x, y) => { return y.Size.CompareTo(x.Size); });
            return videoList;
        }
    }
}