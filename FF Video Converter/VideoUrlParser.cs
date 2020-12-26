using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using YoutubeExplode;


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
        YoutubeClient youtubeClient;
        YoutubeExplode.Videos.Video video;
        YoutubeExplode.Videos.Streams.StreamManifest streamManifest;
        string currentUrl = "";

        public YouTubeParser()
        {
            youtubeClient = new YoutubeClient();
        }

        public override async Task<List<StreamInfo>> GetVideoList(string url)
        {
            if (url != currentUrl)
            {
                video = await youtubeClient.Videos.GetAsync(url).ConfigureAwait(false);
                streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(video.Id).ConfigureAwait(false);
                currentUrl = url;
            }

            List<StreamInfo> videoList = new List<StreamInfo>();
            string displayValue;
            foreach (var videoStreamInfo in streamManifest.GetVideoOnly())
            {
                string videoCodec;
                if (videoStreamInfo.VideoCodec.Contains("avc"))
                {
                    videoCodec = "h264";
                }
                else if (videoStreamInfo.VideoCodec.Contains("av01"))
                {
                    videoCodec = "av1";
                }
                else
                {
                    videoCodec = videoStreamInfo.VideoCodec;
                }
                displayValue = $"{videoStreamInfo.VideoQualityLabel} ({videoCodec}) - {videoStreamInfo.Size.TotalBytes.ToBytesString()}";
                videoList.Add(new StreamInfo(videoStreamInfo.Url, false, video.Title, displayValue, videoStreamInfo.Size.TotalBytes));
            }
            foreach (var audioStreamInfo in streamManifest.GetAudioOnly())
            {
                string audioCodec = audioStreamInfo.AudioCodec.Replace("mp4a.40.2", "aac");
                displayValue = $"{audioStreamInfo.Bitrate.BitsPerSecond / 1000} Kbps ({audioCodec}) - {audioStreamInfo.Size.TotalBytes.ToBytesString()}";
                videoList.Add(new StreamInfo(audioStreamInfo.Url, true, video.Title, displayValue, audioStreamInfo.Size.TotalBytes));
            }
            videoList.Sort((x, y) => { return y.Size.CompareTo(x.Size); });
            return videoList;
        }

        public async Task<string> GetMuxedSource(string url)
        {
            if (url != currentUrl)
            {
                video = await youtubeClient.Videos.GetAsync(url).ConfigureAwait(false);
                streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(video.Id).ConfigureAwait(false);
                currentUrl = url;
            }

            foreach (var videoStreamInfo in streamManifest.GetMuxed())
            {
                if (videoStreamInfo.Resolution.Height == 720) return videoStreamInfo.Url;
            }
            return null;
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
                foreach (var (dash, label, size) in GetDashInfos(dashContent))
                {
                    displayValue = $"{label} - {size.ToBytesString()}";
                    videoList.Add(new StreamInfo(baseVideoUrl + dash, dash == "audio", title, displayValue, size));

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
            double duration = XmlConvert.ToTimeSpan(periodNode.Attributes["duration"].Value).TotalSeconds;

            foreach (XmlNode adaptationSet in periodNode.ChildNodes)
            {
                foreach (XmlNode representation in adaptationSet.ChildNodes)
                {
                    int bitrate = Convert.ToInt32(representation.Attributes["bandwidth"].Value);
                    long size = (long)(bitrate * duration / 8);
                    string dash = representation.SelectSingleNode("*[local-name()='BaseURL']").InnerText;
                    string label = dash == "audio" ? "Aac" : representation.Attributes["height"]?.Value + "p" ?? (bitrate / 1000) + " Kbps";
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


    class FacebookParser : VideoUrlParser
    {
        public override async Task<List<StreamInfo>> GetVideoList(string url)
        {
            List<StreamInfo> videoList = new List<StreamInfo>();

            using (HttpClient client = new HttpClient())
            {
                //Get page source
                client.DefaultRequestHeaders.Add("User-Agent", "facebook_bad"); //Facebook won't return page without a user agent, but will return with a random one...
                byte[] response = await client.GetByteArrayAsync(url).ConfigureAwait(false);
                string pageSource = Encoding.UTF8.GetString(response, 0, response.Length);
                //Get video title
                int startIndex = pageSource.IndexOf("og:title\"") + 19;
                int endIndex = pageSource.IndexOf('"', startIndex);
                string title = pageSource.Substring(startIndex, endIndex - startIndex);
                //Get HD source
                string videoUrl = "";
                startIndex = pageSource.IndexOf("hd_src") + 8;
                if (startIndex > -1) // HD could be missing
                {
                    endIndex = pageSource.IndexOf('"', startIndex);
                    videoUrl = pageSource.Substring(startIndex, endIndex - startIndex).Replace("amp;", "").Replace("\\", "");
                    videoList.Add(new StreamInfo(videoUrl, false, title, "HD Source", 0));
                }
                //Get SD source
                startIndex = pageSource.IndexOf("sd_src", endIndex) + 8;
                endIndex = pageSource.IndexOf('"', startIndex);
                videoUrl = pageSource.Substring(startIndex, endIndex - startIndex).Replace("amp;", "").Replace("\\", "");
                videoList.Add(new StreamInfo(videoUrl, false, title, "SD Source", 0));
            }

            return videoList;
        }

    }


    class InstagramParser : VideoUrlParser
    {
        public override async Task<List<StreamInfo>> GetVideoList(string url)
        {
            List<StreamInfo> videoList = new List<StreamInfo>();

            using (HttpClient client = new HttpClient())
            {
                //Get page source
                string pageSource = await client.GetStringAsync(url);
                //Get video url
                int startIndex = pageSource.IndexOf("og:video") + 19;
                int endIndex = pageSource.IndexOf('"', startIndex);
                string videoUrl = pageSource.Substring(startIndex, endIndex - startIndex);
                //Get owner id
                startIndex = pageSource.IndexOf("instapp:owner_user_id") + 32;
                endIndex = pageSource.IndexOf('"', startIndex);
                string ownerId = pageSource.Substring(startIndex, endIndex - startIndex);
                //Get owner name
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 10_3_3 like Mac OS X) AppleWebKit/603.3.8 (KHTML, like Gecko) Mobile/14G60 Instagram 12.0.0.16.90 (iPhone9,4; iOS 10_3_3; en_US; en-US; scale=2.61; gamut=wide; 1080x1920)"); //User agent of Instagram app
                string response = await client.GetStringAsync($"https://i.instagram.com/api/v1/users/{ownerId}/info/").ConfigureAwait(false);
                startIndex = response.IndexOf("name") + 8;
                endIndex = response.IndexOf('"', startIndex);
                string ownerName = response.Substring(startIndex, endIndex - startIndex);
                videoList.Add(new StreamInfo(videoUrl, false, $"Instagram post by @{ownerName}", "", 0));
            }

            return videoList;
        }
    }
}