using System;
using System.Text.Json;


namespace FFVideoConverter
{
    public class AudioTrack
    {
        public string Codec { get; }
        public Bitrate Bitrate { get; set; }
        public byte Channels { get; }
        public string ChannelLayout { get; }
        public string Title { get; }
        public string Language { get; }
        public byte StreamIndex { get; }
        public long Size { get; set; }
        public bool Enabled { get; set; }
        public bool Default { get; set; }


        public AudioTrack(string codec, int bitrate, byte channels, string channelLayout, long size, byte streamIndex, string title, string language, bool defaultTrack = false)
        {
            Codec = codec;
            Bitrate = bitrate;
            Channels = channels;
            ChannelLayout = channelLayout;
            StreamIndex = streamIndex;
            Title = title;
            Language = language;
            Enabled = true;
            Default = defaultTrack;
            Size = size;
        }

        public static AudioTrack FromJson(JsonElement jsonElement)
        {
            double duration = 0;
            int bitrate = 0;
            byte channels = 0;
            string title = "", codec = "", language = "", channelLayout = "";
            bool defaultTrack = false;
            if (jsonElement.TryGetProperty("codec_name", out JsonElement e))
                codec = e.GetString();
            if (jsonElement.TryGetProperty("bit_rate", out e))
                bitrate = Convert.ToInt32(e.GetString());
            if (jsonElement.TryGetProperty("duration", out e))
                duration = Convert.ToDouble(e.GetString());
            if (jsonElement.TryGetProperty("channels", out e))
                channels = e.GetByte();
            if (jsonElement.TryGetProperty("channel_layout", out e))
                channelLayout = e.GetString();
            byte index = jsonElement.GetProperty("index").GetByte();

            if (jsonElement.TryGetProperty("tags", out JsonElement e2))
            {
                if (e2.TryGetProperty("title", out e))
                    title = e.GetString();
                else if (e2.TryGetProperty("handler_name", out e) && e.GetString() != "SoundHandler") // FFProbe will ignore the title tag on mp4 files, so in order to show the title changed by this program, the new title is also put in the handler tag, which is always reported by ffprobe
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

            long size = bitrate / 8 * Convert.ToInt32(duration);

            return new AudioTrack(codec, bitrate, channels, channelLayout, size, index, title, language, defaultTrack);
        }
    }
}