using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;


namespace FFVideoConverter
{
    public class MediaInfo
    {
        public string Title { get; set; }
        public TimeSpan Duration { get; private set; }
        public long Size { get; private set; }
        public string Codec { get; private set; }
        public string ExternalAudioCodec { get; private set; }
        public double Framerate { get; private set; }
        public Bitrate Bitrate { get; private set; }
        public string Source { get; private set; }
        public string ExternalAudioSource { get; private set; }
        public Resolution Resolution { get; private set; }
        public ColorInfo ColorInfo { get; private set; }
        public IReadOnlyList<AudioTrack> AudioTracks => audioTracks;
        public int Width => Resolution.Width;
        public int Height => Resolution.Height;
        public bool IsLocal => !Source.StartsWith("http");
        public bool HasExternalAudio => !String.IsNullOrEmpty(ExternalAudioSource);
        public bool IsHDR => ColorInfo.DisplayMetadata != null;
        public string DynamicRange => IsHDR ? "HDR" : "SDR";
        public double BitsPerPixel => Bitrate.Bps / Framerate / (Resolution.Width * Resolution.Height);

        private AudioTrack[] audioTracks;

        private MediaInfo()
        {
        }

        public static async Task<MediaInfo> Open(string source, string externalAudioSource = "")
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source).ConfigureAwait(false);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);
            if (externalAudioSource != "")
            {
                mediaInfo.ExternalAudioSource = externalAudioSource;
                await mediaInfo.FF_ExternalAudioOpen(externalAudioSource).ConfigureAwait(false);
            }
            return mediaInfo;
        }

        private async Task FF_Open(string source)
        {
            try
            {
                string jsonOutput = await FFProbe.GetMediaInfo(source);
                await Task.Run(() => ParseJson(jsonOutput)).ConfigureAwait(false);
                ColorInfo = await GetColorInfo(source);
            }
            catch (Exception)
            {
                //Since it's not possible to create a Window from this thread, every exception is rethrown; whoever called this method will have to show the user the error
                throw;
            }       
        }

        private async Task FF_ExternalAudioOpen(string audioSource)
        {
            try
            {
                string jsonOutput = await FFProbe.GetMediaInfo(audioSource);
                using JsonDocument jsonDocument = JsonDocument.Parse(jsonOutput);
                JsonElement streamsElement = jsonDocument.RootElement.GetProperty("streams");
                JsonElement audioStreamElement = new JsonElement();

                //Find first audio stream
                for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                {
                    if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                    {
                        audioStreamElement = streamsElement[i];
                        break;
                    }
                }

                AudioTrack audioTrack = JsonToAudioTrack(audioStreamElement, Duration.TotalSeconds);
                Array.Resize(ref audioTracks, audioTracks.Length + 1);
                audioTracks[audioTracks.Length - 1] = audioTrack;
                ExternalAudioCodec = audioTrack.Codec;
                Size += audioTrack.Size;
            }
            catch (Exception)
            {
                //Since it's not possible to create a Window from this thread, every exception is rethrown; whoever called this method will have to show the user the error
                throw;
            }
        }

        private void ParseJson(string jsonContent)
        {
            using (JsonDocument jsonOutput = JsonDocument.Parse(jsonContent))
            {
                JsonElement streamsElement = jsonOutput.RootElement.GetProperty("streams");
                JsonElement videoStreamElement = new JsonElement();
                List<JsonElement> audioStreamElements = new List<JsonElement>();

                //Find first video stream and all audio streams
                for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                {
                    if (streamsElement[i].GetProperty("codec_type").GetString() == "video")
                    {
                        if (videoStreamElement.ValueKind == JsonValueKind.Undefined) videoStreamElement = streamsElement[i];
                    }
                    else if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                    {
                        audioStreamElements.Add(streamsElement[i]);
                    }
                }

                //Get resolution
                short width = 0, height = 0;
                if (videoStreamElement.TryGetProperty("codec_name", out JsonElement e))
                    Codec = e.GetString();
                if (videoStreamElement.TryGetProperty("width", out e))
                    width = e.GetInt16();
                if (videoStreamElement.TryGetProperty("height", out e))
                    height = e.GetInt16();
                if (width > 0 && height > 0)
                    Resolution = new Resolution(width, height);

                //Get framerate
                string fps = videoStreamElement.GetProperty("avg_frame_rate").GetString();
                if (fps != "N/A")
                {
                    int a = Convert.ToInt32(fps.Remove(fps.IndexOf('/')));
                    int b = Convert.ToInt32(fps.Remove(0, fps.IndexOf('/') + 1));
                    Framerate = Math.Round(a / (double)b, 2);
                }

                //Get remaining properties
                JsonElement formatElement = jsonOutput.RootElement.GetProperty("format");
                double totalSeconds = Double.Parse(formatElement.GetProperty("duration").GetString());
                Duration = TimeSpan.FromSeconds(totalSeconds);
                Size = Int64.Parse(formatElement.GetProperty("size").GetString());
                //Since sometimes the bitrate is missing from the video stream, it's necessary to get the total bitrate from here, and subtract the bitrate of all audio streams
                Bitrate = new Bitrate(Int32.Parse(formatElement.GetProperty("bit_rate").GetString()));

                //Get audio properties
                audioTracks = new AudioTrack[audioStreamElements.Count];
                for (int i = 0; i < audioStreamElements.Count; i++)
                {
                    audioTracks[i] = JsonToAudioTrack(audioStreamElements[i], totalSeconds);
                    Bitrate -= audioTracks[i].Bitrate; //Remove audio bitrate from the total bitrate to get the video bitrate
                }
            }
        }

        private AudioTrack JsonToAudioTrack(JsonElement jsonElement, double duration)
        {
            string codec = "";
            int bitrate = 0;
            byte channels = 0;
            string title = "", language = "", channelLayout = "";
            bool defaultTrack = false;
            if (jsonElement.TryGetProperty("codec_name", out JsonElement e))
                codec = e.GetString();
            if (jsonElement.TryGetProperty("bit_rate", out e))
                bitrate = Convert.ToInt32(e.GetString());
            if (jsonElement.TryGetProperty("channels", out e))
                channels = e.GetByte();
            if (jsonElement.TryGetProperty("channel_layout", out e))
                channelLayout = e.GetString();
            byte index = jsonElement.GetProperty("index").GetByte();

            if (jsonElement.TryGetProperty("tags", out JsonElement e2))
            {
                if (e2.TryGetProperty("title", out e))
                    title = e.GetString();
                else if (e2.TryGetProperty("handler_name", out e) && e.GetString() != "SoundHandle") //FFProbe will ignore the title tag on mp4 files, so in order to show the title changed by this program, the new title is also put in the handler tag, which is always reported by ffprobe
                    title = e.GetString();
                if (e2.TryGetProperty("language", out e) && e.GetString() != "und")
                    language = e.GetString();
                if (bitrate == 0)
                {
                    if (e2.TryGetProperty("BPS-eng", out e))
                        bitrate = Convert.ToInt32(e.GetString()); ;
                }
                e2 = jsonElement.GetProperty("disposition");
                if (e2.TryGetProperty("default", out e))
                    defaultTrack = Convert.ToBoolean(e.GetByte());
            }

            return new AudioTrack(codec, bitrate, channels, channelLayout, duration, index, title, language, defaultTrack);
        }

        private async Task<ColorInfo> GetColorInfo(string source)
        {
            string jsonOutput = await FFProbe.GetColorInfo(source).ConfigureAwait(false);
            JsonElement element = JsonDocument.Parse(jsonOutput).RootElement;
            element = element.GetProperty("frames")[0];
            return ColorInfo.FromJson(element);
        }
    }
}