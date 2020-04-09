using System;
using System.Runtime.InteropServices;


namespace FFVideoConverter
{
    [StructLayout(LayoutKind.Auto)]
    public struct ProgressData
    {
        public short EncodingSpeedFps { get; set; }
        public long CurrentByteSize { get; set; }
        public float AverageBitrate { get; set; }
        public float EncodingSpeed { get; set; }
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan TotalTime { get; set; }
        public uint CurrentFrame { get; set; }
    }

    public struct CropData
    {
        public static readonly CropData NoCrop = new CropData(-1, -1, -1, -1);

        public short Left { get; }
        public short Top { get; }
        public short Right { get; }
        public short Bottom { get; }

        public CropData(short left, short top, short right, short bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool HasValue()
        {
            return Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0;
        }

        public override string ToString()
        {
            return HasValue() ? $"Left: {Left}, Top: {Top}\nRight: {Right}, Bottom: {Bottom}" : "no crop";
        }
    }

    public struct Resolution
    {
        public static readonly Resolution SameAsSource = new Resolution(-1, -1);

        public short Width { get; }
        public short Height { get; }

        public Resolution(short width, short height)
        {
            Width = width;
            Height = height;
        }

        public Resolution(string resolution) //resolution must be a string in this form: 1920x1080
        {
            string[] numbers = resolution.Split('x');
            Width = Convert.ToInt16(numbers[0]);
            Height = Convert.ToInt16(numbers[1]);
        }

        public bool HasValue()
        {
            return Width >= 0 && Height >= 0;
        }

        public override string ToString()
        {
            return HasValue() ? $"{Width}x{Height}" : "same as source";
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ConversionOptions
    {
        public Encoder Encoder { get; set; }
        public byte Framerate { get; set; }
        public Resolution Resolution { get; set; }
        public CropData CropData { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public bool SkipAudio { get; set; }

        public ConversionOptions(Encoder encoder)
        {
            Encoder = encoder;
            CropData = CropData.NoCrop;
            Resolution = Resolution.SameAsSource;
            Framerate = 0;
            Start = TimeSpan.Zero;
            End = TimeSpan.Zero;
            SkipAudio = false;
        }
    }
}