using System;


namespace FFVideoConverter
{
    public struct Bitrate : IEquatable<Bitrate>
    {
        public int Bps { get; }
        public float Kbps => ((float)Bps) / 1000;

        public Bitrate(int bitsPerSecond)
        {
            Bps = bitsPerSecond;
        }

        public Bitrate(float kilobitsPerSecond)
        {
            Bps = (int)(kilobitsPerSecond * 1000);
        }

        public bool Equals(Bitrate other)
        {
            return Bps == other.Bps;
        }

        public static bool operator ==(Bitrate b1, Bitrate b2)
        {
            return b1.Bps == b2.Bps;
        }

        public static bool operator !=(Bitrate b1, Bitrate b2)
        {
            return b1.Bps != b2.Bps;
        }

        public static Bitrate operator +(Bitrate b1, Bitrate b2)
        {
            return new Bitrate(b1.Bps + b2.Bps);
        }

        public static Bitrate operator -(Bitrate b1, Bitrate b2)
        {
            return new Bitrate(b1.Bps - b2.Bps);
        }
    }
}