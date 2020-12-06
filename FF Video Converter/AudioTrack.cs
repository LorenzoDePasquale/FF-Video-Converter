namespace FFVideoConverter
{
    public class AudioTrack
    {
        public string Codec { get; }
        /// <summary>
        /// Audio bitrate (in bit)
        /// </summary>
        public int Bitrate { get; }
        public int SampleRate { get; }
        public string Language { get; }
        public int StreamIndex { get; }
        public long Size { get; }
        public bool Enabled { get; set; }
        public bool Default { get; set; }

        public AudioTrack(string codec, int bitrate, int sampleRate, double totalSeconds, int streamIndex, string language = "undefined", bool defaultTrack = false)
        {
            Codec = codec;
            Bitrate = bitrate;
            SampleRate = sampleRate;
            StreamIndex = streamIndex;
            Language = language;
            Enabled = true;
            Default = defaultTrack;
            Size = (long)(bitrate / 8 * totalSeconds);
        }
    }
}