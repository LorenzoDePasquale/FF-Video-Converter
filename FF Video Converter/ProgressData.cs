using System;
using System.Runtime.InteropServices;


namespace FFVideoConverter
{
    [StructLayout(LayoutKind.Auto)]
    public struct ProgressData
    {
        public short EncodingSpeedFps;
        public long CurrentByteSize;
        public long TotalByteSize;
        public Bitrate AverageBitrate;
        public float EncodingSpeed;
        public TimeSpan CurrentTime;
        public TimeSpan TotalTime;
        public int CurrentFrames;
        public int TotalFrames;
        public EncodingMode EncodingMode;
        public float Percentage;
    }
}