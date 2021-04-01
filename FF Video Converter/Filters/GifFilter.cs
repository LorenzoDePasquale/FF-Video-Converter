namespace FFVideoConverter.Filters
{
    class GifFilter : IFilter
    {
        public enum PaletteMode { Single, OnePerFrame }

        public string FilterName => "GIF encoder";

        public bool UseMultiplePalette { get; set; }
        public byte MaxColors { get; set; }

        public GifFilter(bool useMultiplePalette = false, byte maxColors = 255)
        {
            UseMultiplePalette = useMultiplePalette;
            MaxColors = maxColors;
        }

        public string GetFilter()
        {
            if (UseMultiplePalette)
            {
                return $"split[a][b];[a]palettegen=stats_mode=single:max_colors={MaxColors}[p];[b]fifo[c];[c][p]paletteuse=new=1";
            }

            return $"split[a][b];[a]palettegen=max_colors={MaxColors}[p];[b]fifo[c];[c][p]paletteuse";
        }

        public override string ToString()
        {
            return $"Palette: {(UseMultiplePalette ? "per-frame" : "global")}\nColors: {MaxColors}";
        }
    }
}