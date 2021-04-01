namespace FFVideoConverter.Filters
{
    class RotationFilter : IFilter
    {
        public string FilterString { get; }
        public int RotationType { get; }
        public string FilterName { get => "Rotation"; }

        public RotationFilter(int rotationType)
        {
            RotationType = rotationType >= 0 && rotationType <= 7 ? rotationType : 0;
        }

        public static implicit operator RotationFilter(int rotationType)
        {
            return new RotationFilter(rotationType);
        }

        public string GetFilter()
        {
            switch (RotationType)
            {
                case 0:
                    return "";
                case 1:
                    return "hflip";
                case 2:
                    return "transpose=1";
                case 3:
                    return "transpose=3";
                case 4:
                    return "transpose=1,transpose=1";
                case 5:
                    return "vflip";
                case 6:
                    return "transpose=2";
                case 7:
                    return "transpose=0";
                default:
                    return "";
            }
        }

        public bool HasValue()
        {
            return RotationType > 0;
        }

        public override string ToString()
        {
            switch (RotationType)
            {
                case 0:
                    return "No rotation";
                case 1:
                    return "Horizontal flip";
                case 2:
                    return "90° clockwise";
                case 3:
                    return "90° clockwise and flip";
                case 4:
                    return "180°";
                case 5:
                    return "180° and flip";
                case 6:
                    return "270° clockwise";
                case 7:
                    return "270° clockwise and flip";
                default:
                    return "No rotation";
            }
        }
    }
}