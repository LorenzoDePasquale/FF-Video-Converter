using System;
using System.Runtime.InteropServices;


namespace FFVideoConverter
{
    [StructLayout(LayoutKind.Auto)]
    public struct ProgressData
    {
        public short EncodingSpeedFps { get; set; }
        public long CurrentByteSize { get; set; }
        public float AverageBitrate { get; set; }
        public float EncodingSpeed { get; set; }
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan TotalTime { get; set; }
        public uint CurrentFrame { get; set; }
        public string ErrorMessage { get; set; }
    }

    public struct CropData
    {
        public static readonly CropData NoCrop = new CropData(0, 0, 0, 0);

        public short Left { get; }
        public short Top { get; }
        public short Right { get; }
        public short Bottom { get; }
        public string FilterString 
        { 
            get
            {
                //in_w is input width, in_h is input height
                return HasValue() ? $"crop=in_w-{Left + Right}:in_h-{Top + Bottom}:{Left}:{Top}" : "";
            }
        }

        public CropData(short left, short top, short right, short bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
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

    public struct Resolution
    {
        public static readonly Resolution SameAsSource = new Resolution(0, 0);

        public short Width { get; }
        public short Height { get; }
        public AspectRatio AspectRatio { get; }
        public string FilterString 
        { 
            get
            {
                return HasValue() ? $"scale={Width}:{Height}" : "";
            }
        }

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

        public static Resolution FromString(string resolution) //resolution must be a string in this form: 1920x1080
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
        public float Ratio { get; }
        public string IntegerName { get; }
        public string DecimalName { get; }

        public AspectRatio(short width, short height)
        {
            int gcd = GCD(width, height);
            Width = (short)(width / gcd);
            Heigth = (short)(height / gcd);
            Ratio = width / (float)height;
            IntegerName = $"{Width}:{Heigth}";
            DecimalName = $"{Ratio:0.##}:1";
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

    public struct Rotation
    {
        public static readonly Rotation NoRotation = new Rotation(0);
        public string FilterString { get; }
        public int RotationType { get; }

        public Rotation(int rotationType)
        {
            RotationType = rotationType >= 0 && rotationType <= 7 ? rotationType : 0;
            switch (rotationType)
            {
                case 0:
                    FilterString = "";
                    break;
                case 1:
                    FilterString = "hflip";
                    break;
                case 2:
                    FilterString = "transpose=1";
                    break;
                case 3:
                    FilterString = "transpose=3";
                    break;
                case 4:
                    FilterString = "transpose=1,transpose=1";
                    break;
                case 5:
                    FilterString = "vflip";
                    break;
                case 6:
                    FilterString = "transpose=2";
                    break;
                case 7:
                    FilterString = "transpose=0";
                    break;
                default:
                    FilterString = "";
                    break;
            }
        }

        public static implicit operator Rotation(int rotationType)
        {
            return new Rotation(rotationType);
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

    [StructLayout(LayoutKind.Auto)]
    public struct ConversionOptions
    {
        public Encoder Encoder { get; set; }
        public byte Framerate { get; set; }
        public Resolution Resolution { get; set; }
        public CropData CropData { get; set; }
        public Rotation Rotation { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public bool SkipAudio { get; set; }


        public ConversionOptions(Encoder encoder)
        {
            Encoder = encoder;
            CropData = CropData.NoCrop;
            Resolution = Resolution.SameAsSource;
            Rotation = Rotation.NoRotation;
            Framerate = 0;
            Start = TimeSpan.Zero;
            End = TimeSpan.Zero;
            SkipAudio = false;

            
        }

        /*public T GetFilter<T>() where T : IFilter
        {
            //Returns the first IFilter object of type T, or a default instance of T if there is no such object
            return (T)(Filters.Find(x => x is T) ?? default(T));
        }*/
    }
}