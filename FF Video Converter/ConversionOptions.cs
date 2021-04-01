using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace FFVideoConverter
{
    [StructLayout(LayoutKind.Auto)]
    public struct ConversionOptions
    {
        public VideoEncoder Encoder;
        public bool NoAudio;
        public EncodingMode EncodingMode;
        public TimeIntervalCollection EncodeSections;
        /// <summary>
        /// The key is the streamIndex property of the audioTrack the AudioConversionOptions refers to
        /// </summary>
        public Dictionary<int, AudioConversionOptions> AudioConversionOptions;
        public List<IFilter> Filters;
        public bool FadeEffect;

        public ConversionOptions(VideoEncoder encoder)
        {
            Encoder = encoder;
            NoAudio = false;
            EncodingMode = 0;
            EncodeSections = new TimeIntervalCollection(TimeSpan.Zero);
            AudioConversionOptions = new Dictionary<int, AudioConversionOptions>();
            Filters = new List<IFilter>();
            FadeEffect = false;
        }
    }

    public class AudioConversionOptions
    {
        public AudioEncoder Encoder;
        public string Title;
        public byte Channels;

        public AudioConversionOptions()
        {
        }
    }
}