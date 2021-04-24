using System;


namespace FFVideoConverter
{
    public struct Resolution
    {
        public static readonly Resolution SameAsSource = new Resolution(0, 0);

        public short Width { get; }
        public short Height { get; }
        public AspectRatio AspectRatio { get; }


        public Resolution(short width, short height)
        {
            Width = width;
            Height = height;
            if (width > 0 && height > 0)
            {
                AspectRatio = new AspectRatio(width, height);
            }
            else
            {
                AspectRatio = new AspectRatio();
            }
        }

        public static Resolution FromString(string resolution) // resolution must be a string in this form: 1920x1080
        {
            string[] numbers = resolution.Split('x');
            return new Resolution(Convert.ToInt16(numbers[0]), Convert.ToInt16(numbers[1]));
        }

        public bool HasValue()
        {
            return Width > 0 || Height > 0;
        }

        public override string ToString()
        {
            return HasValue() ? $"{Width}x{Height}" : "Same as source";
        }
    }


    public struct AspectRatio
    {
        public short Width { get; }
        public short Heigth { get; }
        public float Ratio => Width / (float)Heigth;
        public string IntegerName => $"{Width}:{Heigth}";
        public string DecimalName => $"{Ratio:0.##}:1";

        public AspectRatio(short width, short height)
        {
            int gcd = GCD(width, height);
            Width = (short)(width / gcd);
            Heigth = (short)(height / gcd);
        }

        public override string ToString()
        {
            return $"{DecimalName} ({IntegerName})";
        }

        private static int GCD(int a, int b)
        {
            return b == 0 ? Math.Abs(a) : GCD(b, a % b);
        }
    }
}