namespace FFVideoConverter.Encoders
{
    public enum Quality 
    { 
        Best, VeryGood, Good, Medium, Low, VeryLow 
    }

    public enum Preset 
    { 
        Slower, Slow, Medium, Fast, Faster, VeryFast
    }

    public enum EncodingMode 
    { 
        ConstantQuality, AverageBitrate_SinglePass, AverageBitrate_FirstPass, AverageBitrate_SecondPass, Copy, NoEncoding
    }

    public enum PixelFormat
    {
        copy, yuv420p, yuv422p, yuv444p, yuv420p10le, yuv422p10le, yuv444p10le
    }


    public abstract class VideoEncoder
    {
        public abstract string Name { get; }
        public abstract bool IsDoublePassSupported { get; }
        public abstract PixelFormat[] SupportedPixelFormats { get; }
        public Quality Quality { get; set; }
        public Preset Preset { get; set; }
        public Bitrate Bitrate { get; set; }
        public ColorInfo ColorInfo { get; set; }
        public PixelFormat PixelFormat { get; set; }

        public abstract string GetFFMpegCommand(EncodingMode encodingMode);

        public override string ToString()
        {
            return Name;
        }
    }

    public static class EncoderExtensionsMethods
    {
        public static string GetName(this Quality quality)
        {
            return quality switch
            {
                Quality.Best => "Best",
                Quality.VeryGood => "Very good",
                Quality.Good => "Good",
                Quality.Medium => "Medium",
                Quality.Low => "Low",
                Quality.VeryLow => "Very low",
                _ => "",
            };
        }

        public static string GetName(this Preset preset)
        {
            return preset switch
            {
                Preset.Slower => "Slower",
                Preset.Slow => "Slow",
                Preset.Medium => "Medium",
                Preset.Fast => "Fast",
                Preset.Faster => "Faster",
                Preset.VeryFast => "Very fast",
                _ => "",
            };
        }

        public static string GetName(this PixelFormat pixelFormat)
        {
            return pixelFormat switch
            {
                PixelFormat.copy => "Same as source",
                PixelFormat.yuv420p => "YUV 4:2:0 at 8 bit",
                PixelFormat.yuv422p => "YUV 4:2:2 at 8 bit",
                PixelFormat.yuv444p => "YUV 4:4:4 at 8 bit",
                PixelFormat.yuv420p10le => "YUV 4:2:0 at 10 bit",
                PixelFormat.yuv422p10le => "YUV 4:2:2 at 10 bit",
                PixelFormat.yuv444p10le => "YUV 4:4:4 at 10 bit",
                _ => "",
            };
        }
    }
}