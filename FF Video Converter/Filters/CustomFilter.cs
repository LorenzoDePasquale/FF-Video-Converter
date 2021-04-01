namespace FFVideoConverter.Filters
{
    class CustomFilter : IFilter
    {
        public string FilterName { get; }

        private string filter;

        public CustomFilter(string name, string filter)
        {
            FilterName = name;
            this.filter = filter;
        }

        public string GetFilter()
        {
            return filter;
        }
    }
}
