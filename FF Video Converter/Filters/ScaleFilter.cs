namespace FFVideoConverter.Filters
{
    class ScaleFilter : IFilter
    {
        public Resolution OutputResolution { get; set; }
        public string FilterName { get => "Resolution"; }

        public ScaleFilter(Resolution outputResolution)
        {
            OutputResolution = outputResolution;
        }

        public string GetFilter()
        {
            return OutputResolution.HasValue() ? $"scale={OutputResolution.Width}:{OutputResolution.Height}" : "";
        }

        public override string ToString()
        {
            return OutputResolution.ToString();
        }
    }
}
