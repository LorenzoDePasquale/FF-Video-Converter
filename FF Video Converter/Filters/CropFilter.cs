namespace FFVideoConverter.Filters
{
    class CropFilter : IFilter
    {
        public short Left { get; }
        public short Top { get; }
        public short Right { get; }
        public short Bottom { get; }
        public string FilterName { get => "Crop"; }

        public CropFilter(short left, short top, short right, short bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public string GetFilter()
        {
            return HasValue() ? $"crop=in_w-{Left + Right}:in_h-{Top + Bottom}:{Left}:{Top}" : ""; // in_w is input width, in_h is input height
        }

        public bool HasValue()
        {
            return Left > 0 || Top > 0 || Right > 0 || Bottom > 0;
        }

        public override string ToString()
        {
            return HasValue() ? $"Left: {Left}, Top: {Top}\nRight: {Right}, Bottom: {Bottom}" : "no crop";
        }
    }
}
