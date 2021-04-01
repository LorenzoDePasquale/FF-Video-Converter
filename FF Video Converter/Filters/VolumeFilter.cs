namespace FFVideoConverter.Filters
{
    class VolumeFilter : IFilter
    {
        public int Percentage { get; set; }
        public string FilterName { get => "Volume"; }

        public VolumeFilter(int percentage)
        {
            Percentage = percentage;
        }

        public string GetFilter()
        {
            return $"volume={(Percentage / 100f):0.##}";
        }

        public override string ToString()
        {
            return $"{Percentage}%";
        }
    }
}
