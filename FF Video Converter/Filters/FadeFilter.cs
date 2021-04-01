namespace FFVideoConverter.Filters
{
    public enum FadeMode { In, Out }

    class FadeFilter : IFilter
    {
        public FadeMode FadeMode { get; set; }
        public double Duration { get; set; }
        public double StartSeconds { get; set; }
        public string FilterName { get => "Fade"; }

        public FadeFilter(FadeMode fadeMode, double duration, double startSeconds = 0)
        {
            FadeMode = fadeMode;
            Duration = duration;
            StartSeconds = startSeconds;
        }

        public string GetFilter()
        {
            return $"fade=t={(FadeMode == FadeMode.In ? "in" : "out")}:d={Duration}:st={StartSeconds}";
        }
    }
}
