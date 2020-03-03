using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;


namespace FFVideoConverter
{
    struct StreamInfo
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


    abstract class VideoUrlParser
    {
        public abstract Task<List<StreamInfo>> GetVideoList(string url);
    }

   
    class YouTubeParser : VideoUrlParser
    {
        public override async Task<List<StreamInfo>> GetVideoList(string url)
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


    class RedditParser : VideoUrlParser
    {
        public override async Task<List<StreamInfo>> GetVideoList(string url)
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
        }

        private List<(string dash, string label, long size)> GetDashInfos(string dashContent)
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


    class TwitterParser : VideoUrlParser
    {
        public override async Task<List<StreamInfo>> GetVideoList(string url)
        {
            List<StreamInfo> videoList = new List<StreamInfo>();
            string videoId = url.Split('/')[5];
            string title = $"@{url.Split('/')[3]} Twitter video";

            using (HttpClient client = new HttpClient())
            {
                //Get javascript player url
                string pageSource = await client.GetStringAsync("https://twitter.com/i/videos/tweet/" + videoId).ConfigureAwait(false);
                string jsPlayerUrl = "";
                foreach (var line in pageSource.Split('\n'))
                {
                    if (line.Contains("script"))
                    {
                        jsPlayerUrl = line.Substring(line.IndexOf('"') + 1);
                        jsPlayerUrl = jsPlayerUrl.Remove(jsPlayerUrl.IndexOf('"'));
                        break;
                    }
                }
                //Get bearer token
                pageSource = await client.GetStringAsync(jsPlayerUrl).ConfigureAwait(false);
                string token = pageSource.Substring(pageSource.IndexOf("Bearer") + 7);
                token = token.Remove(token.IndexOf('"'));
                //Get guest_token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage response = await client.PostAsync("https://api.twitter.com/1.1/guest/activate.json", null).ConfigureAwait(false);
                JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                string guestToken = document.RootElement.GetProperty("guest_token").GetString();
                //Get m3u8 file
                client.DefaultRequestHeaders.Add("x-guest-token", guestToken);
                string jsResponse = await client.GetStringAsync($"https://api.twitter.com/1.1/videos/tweet/config/{videoId}.json").ConfigureAwait(false);
                document = JsonDocument.Parse(jsResponse);
                string playbackUrl = document.RootElement.GetProperty("track").GetProperty("playbackUrl").GetString();
                string m3u8Content = await client.GetStringAsync(playbackUrl).ConfigureAwait(false);
                //Get m3u8 playlists
                foreach (var (relativeUrl, label) in GetPlaylists(m3u8Content))
                {
                    videoList.Add(new StreamInfo($"https://video.twimg.com{relativeUrl}", false, title, label, 0));
                }

                document.Dispose();
            }

            return videoList;
        }

        private List<(string relativeUrl, string label)> GetPlaylists(string m3u8Content)
        {
            List<(string, string)> videoInfos = new List<(string, string)>();
            string[] m3u8Lines = m3u8Content.Split('\n');

            for (int i = 0; i < m3u8Lines.Length - 1; i++)
            {
                if (m3u8Lines[i].StartsWith("#EXT-X-STREAM-INF"))
                {
                    string[] data = m3u8Lines[i].Substring(17).Split(',');
                    string resolution = data[1].Substring(11);
                    videoInfos.Add((m3u8Lines[i + 1], resolution));
                }
            }

            return videoInfos;
        }
    }
}