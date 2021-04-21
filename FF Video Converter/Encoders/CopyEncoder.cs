namespace FFVideoConverter.Encoders
{
    public class CopyEncoder : VideoEncoder
    {
        public override string Name => "Copy";
        public override bool IsDoublePassSupported => false;
        public override PixelFormat[] SupportedPixelFormats => new[] { PixelFormat.copy };

        public override string GetFFMpegCommand(EncodingMode encodingMode)
        {
            return "copy";
        }
    }
}