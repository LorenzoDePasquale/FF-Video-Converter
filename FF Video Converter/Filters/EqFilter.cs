using System;
using System.Linq;

namespace FFVideoConverter.Filters
{
    class EqFilter : IFilter
    {
        public string FilterName => "Picture";

        // 0 - 2 (default 1)
        private double contrast = 1;
        public double Contrast
        {
            get => contrast - 1;
            set => contrast = value + 1;
        }
        // -1 - 1 (default 0)
        private double brightness = 0;
        public double Brightness 
        { 
            get => ScaleFrom(brightness, -0.5, 0.5, 0); 
            set => brightness = ScaleTo(value, -0.5, 0.5, 0); 
        }
        // 0 - 3 (default 1)
        private double saturation = 1;
        public double Saturation
        {
            get => ScaleFrom(saturation, 0, 3, 1);
            set => saturation = ScaleTo(value, 0, 3, 1);
        }
        // 0 - 10 (default 1)
        private double gamma = 1;
        public double Gamma 
        { 
            get => ScaleFrom(gamma, 0, 5, 1);
            set => gamma = ScaleTo(value, 0, 5, 1);
        }
        // 0 - 10 (default 1)
        private double gammaRed = 1;
        public double GammaRed
        {
            get => ScaleFrom(gammaRed, 0, 4, 1);
            set => gammaRed = ScaleTo(value, 0, 4, 1);
        }
        // 0 - 10 (default 1)
        private double gammaGreen = 1;
        public double GammaGreen
        {
            get => ScaleFrom(gammaGreen, 0, 4, 1);
            set => gammaGreen = ScaleTo(value, 0, 4, 1);
        }
        // 0 - 10 (default 1)
        private double gammaBlue = 1;
        public double GammaBlue
        {
            get => ScaleFrom(gammaBlue, 0, 4, 1);
            set => gammaBlue = ScaleTo(value, 0, 4, 1);
        }


        public string GetFilter()
        {
            return $"eq=contrast={contrast}:brightness={brightness}:saturation={saturation}:gamma={gamma}:gamma_r={gammaRed}:gamma_g={gammaGreen}:gamma_b={gammaBlue}:gamma_weight=1";
        }

        public override string ToString()
        {
            string[] properties = { PropertyToString("Contrast", Contrast), PropertyToString("Brightness", Brightness), PropertyToString("Saturation", Saturation), PropertyToString("Gamma", Gamma), PropertyToString("Red", GammaRed), PropertyToString("Green", GammaGreen), PropertyToString("Blue", GammaBlue), };
            return string.Join('\n', properties.Where(s => s != ""));
        }

        private string PropertyToString(string name, double value)
        {
            sbyte percentage = Convert.ToSByte(value * 100);
            if (value > 0)
            {
                return $"{name}: +{percentage}%";
            }
            else if (value < 0)
            {
                return $"{name}: {percentage}%";
            }
            return "";
        }

        //Receives a number in the [-1,0,+1] range and scales it to the [min,center,max] range
        private double ScaleTo(double value, double min, double max, double center)
        {
            //f(t) = c + (d−c) / (b−a) * (t−a)   will map t ∈ [a,b] to the interval [c,d]
            if (value < 0)
            {
                //a = -1, b = 0, c = min, d = center
                return min + (center - min) * (value + 1);
            }
            else if (value > 0)
            {
                //a = 0, b = 1, c = center, d = max
                return center + (max - center) * value;
            }
            return center;

            //return value < 0 ? value * (min + center) * -1 : value * (max + center);
        }

        //Receives a number in the [min,center,max] range and scales it back to the [-1,0,+1] range
        private double ScaleFrom(double value, double min, double max, double center)
        {
            //f(t) = c + (d−c) / (b−a) * (t−a)   will map t ∈ [a,b] to the interval [c,d]
            if (value < center)
            {
                //a = min, b = center, c = -1, d = 0
                return -1 + 1 / (center - min) * (value - min);
            }
            else if (value > center)
            {
                //a = center, b = max, c = 0, d = 1
                return 1 / (max - center) * (value - center);
            }
            return 0;
        }
    }
}