namespace FFVideoConverter.Filters
{
    class FpsFilter : IFilter
    {
        public double Framerate { get; set; }
        public string FilterName { get => "Framerate"; }

        public FpsFilter(double framerate)
        {
            Framerate = framerate;
        }

        public string GetFilter()
        {
            return $"fps={Framerate}";
        }

        public override string ToString()
        {
            return $"{Framerate} fps";
        }
    }
}
