namespace FFVideoConverter
{
    public class AudioTrack
    {
        public string Codec { get; }
        public Bitrate Bitrate { get; }
        public byte Channels { get; }
        public string ChannelLayout { get; }
        public string Title { get; }
        public string Language { get; }
        public byte StreamIndex { get; }
        public long Size { get; }
        public bool Enabled { get; set; }
        public bool Default { get; set; }


        public AudioTrack(string codec, int bitrate, byte channels, string channelLayout, double totalSeconds, byte streamIndex, string title, string language, bool defaultTrack = false)
        {
            Codec = codec;
            Bitrate = new Bitrate(bitrate);
            Channels = channels;
            ChannelLayout = channelLayout;
            StreamIndex = streamIndex;
            Title = title;
            Language = language;
            Enabled = true;
            Default = defaultTrack;
            Size = (long)(bitrate / 8 * totalSeconds);
        }
    }
}