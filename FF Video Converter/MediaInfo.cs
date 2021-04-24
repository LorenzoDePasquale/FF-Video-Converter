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

        public static async Task<MediaInfo> Open(string source)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source).ConfigureAwait(false);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);

            return mediaInfo;
        }

        public static async Task<MediaInfo> Open(string source, string externalAudioSource)
        {
            MediaInfo mediaInfo = new MediaInfo();
            mediaInfo.Source = source;
            await mediaInfo.FF_Open(source).ConfigureAwait(false);
            if (!source.StartsWith("http")) mediaInfo.Title = Path.GetFileName(source);
            mediaInfo.ExternalAudioSource = externalAudioSource;
            await mediaInfo.FF_ExternalAudioOpen(externalAudioSource).ConfigureAwait(false);

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
                // Since it's not possible to create a Window from this thread, every exception is rethrown; whoever called this method will have to show the user the error
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

                // Find first audio stream
                for (int i = 0; i < streamsElement.GetArrayLength(); i++)
                {
                    if (streamsElement[i].GetProperty("codec_type").GetString() == "audio")
                    {
                        audioStreamElement = streamsElement[i];
                        break;
                    }
                }

                AudioTrack audioTrack = AudioTrack.FromJson(audioStreamElement);
                audioTracks = new AudioTrack[] { audioTrack };
                ExternalAudioCodec = audioTrack.Codec;
                Size += audioTrack.Size;
            }
            catch (Exception)
            {
                // Since it's not possible to create a Window from this thread, every exception is rethrown; whoever called this method will have to show the user the error
                throw;
            }
        }

        private void ParseJson(string jsonContent)
        {
            using JsonDocument jsonOutput = JsonDocument.Parse(jsonContent);
            JsonElement streamsElement = jsonOutput.RootElement.GetProperty("streams");
            JsonElement videoStreamElement = new JsonElement();
            List<JsonElement> audioStreamElements = new List<JsonElement>();

            // Find first video stream and all audio streams
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

            // Get resolution
            short width = 0, height = 0;
            if (videoStreamElement.TryGetProperty("codec_name", out JsonElement e))
                Codec = e.GetString();
            if (videoStreamElement.TryGetProperty("width", out e))
                width = e.GetInt16();
            if (videoStreamElement.TryGetProperty("height", out e))
                height = e.GetInt16();
            if (width > 0 && height > 0)
                Resolution = new Resolution(width, height);

            // Get framerate
            string fps = videoStreamElement.GetProperty("avg_frame_rate").GetString();
            if (fps != "N/A")
            {
                int a = Convert.ToInt32(fps.Remove(fps.IndexOf('/')));
                int b = Convert.ToInt32(fps.Remove(0, fps.IndexOf('/') + 1));
                Framerate = Math.Round(a / (double)b, 2);
            }

            // Get remaining properties
            JsonElement formatElement = jsonOutput.RootElement.GetProperty("format");
            double totalSeconds = Double.Parse(formatElement.GetProperty("duration").GetString());
            Duration = TimeSpan.FromSeconds(totalSeconds);
            Size = Int64.Parse(formatElement.GetProperty("size").GetString());
            // Since sometimes the bitrate is missing from the video stream, it's necessary to get the total bitrate from here, and subtract the bitrate of all audio streams
            Bitrate = Int32.Parse(formatElement.GetProperty("bit_rate").GetString());

            // Get audio properties
            audioTracks = new AudioTrack[audioStreamElements.Count];
            for (int i = 0; i < audioStreamElements.Count; i++)
            {
                audioTracks[i] = AudioTrack.FromJson(audioStreamElements[i]);
                Bitrate -= audioTracks[i].Bitrate; // Remove audio bitrate from the total bitrate to get the video bitrate
            }
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