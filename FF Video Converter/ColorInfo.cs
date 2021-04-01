using System;
using System.Text.Json;

namespace FFVideoConverter
{
    public class ColorInfo
    {
        public string PixelFormat { get; private set; }
        public string ColorSpace { get; private set; }
        public string ColorPrimaries { get; private set; }
        public string ColorTransfer { get; private set; }
        public MasteringDisplayMetadata DisplayMetadata { get; private set; }
        public (int maxContent, int maxAverage) LightLevelMetadata { get; private set; }

        /*
        {
            "pix_fmt": "yuv420p10le",
            "color_space": "bt2020nc",
            "color_primaries": "bt2020",
            "color_transfer": "smpte2084",
            "side_data_list": [
                {
                    "side_data_type": "Mastering display metadata",
                    "red_x": "34000/50000",
                    "red_y": "16000/50000",
                    "green_x": "13250/50000",
                    "green_y": "34500/50000",
                    "blue_x": "7500/50000",
                    "blue_y": "3000/50000",
                    "white_point_x": "15635/50000",
                    "white_point_y": "16450/50000",
                    "min_luminance": "50/10000",
                    "max_luminance": "10000000/10000"
                },
                {
                    "side_data_type": "Content light level metadata",
                    "max_content": 0,
                    "max_average": 0
                }
            ]
        }
        */
        public static ColorInfo FromJson(JsonElement element)
        {
            ColorInfo colorInfo = new ColorInfo();

            if (element.TryGetProperty("pix_fmt", out JsonElement e))
                colorInfo.PixelFormat = e.GetString();
            if (element.TryGetProperty("color_space", out e))
                colorInfo.ColorSpace = e.GetString();
            if (element.TryGetProperty("color_primaries", out e))
                colorInfo.ColorPrimaries = e.GetString();
            if (element.TryGetProperty("color_transfer", out e))
                colorInfo.ColorTransfer = e.GetString();
            if (element.TryGetProperty("side_data_list", out e))
            {
                colorInfo.DisplayMetadata = MasteringDisplayMetadata.FromJson(e[0]);
                colorInfo.LightLevelMetadata = (e[1].GetProperty("max_content").GetInt32(), e[1].GetProperty("max_average").GetInt32());
            }

            return colorInfo;
        }

        public override string ToString()
        {
            return $"Color space: {ColorSpace}\nColor primaries: {ColorPrimaries}\nColor transfer: {ColorTransfer}\nDisplay metadata: {DisplayMetadata}\nLight level metadata: (max content: {LightLevelMetadata.maxContent}, max average: {LightLevelMetadata.maxAverage})";
        }


        public class MasteringDisplayMetadata
        {
            public (int x, int y) Red;
            public (int x, int y) Green;
            public (int x, int y) Blue;
            public (int x, int y) WhitePoint;
            public (int min, int max) Luminance;

            /*
            {
                "side_data_type": "Mastering display metadata",
                "red_x": "34000/50000",
                "red_y": "16000/50000",
                "green_x": "13250/50000",
                "green_y": "34500/50000",
                "blue_x": "7500/50000",
                "blue_y": "3000/50000",
                "white_point_x": "15635/50000",
                "white_point_y": "16450/50000",
                "min_luminance": "50/10000",
                "max_luminance": "10000000/10000"
            }
            */
            public static MasteringDisplayMetadata FromJson(JsonElement jsonElement)
            {
                return new MasteringDisplayMetadata()
                {
                    Red = (GetValue(jsonElement.GetProperty("red_x").GetString()), GetValue(jsonElement.GetProperty("red_y").GetString())),
                    Green = (GetValue(jsonElement.GetProperty("green_x").GetString()), GetValue(jsonElement.GetProperty("green_y").GetString())),
                    Blue = (GetValue(jsonElement.GetProperty("blue_x").GetString()), GetValue(jsonElement.GetProperty("blue_y").GetString())),
                    WhitePoint = (GetValue(jsonElement.GetProperty("white_point_x").GetString()), GetValue(jsonElement.GetProperty("white_point_y").GetString())),
                    Luminance = (GetValue(jsonElement.GetProperty("min_luminance").GetString()), GetValue(jsonElement.GetProperty("max_luminance").GetString())),
                };

                static int GetValue(string value)
                {
                    value = value.Substring(0, value.IndexOf('/'));
                    return Convert.ToInt32(value);
                }
            }

            public override string ToString()
            {
                //G(green_x,green_y)B(blue_x,blue_y)R(red_x,red_y)WP(white_x,white_y)L(max,min)
                return $"G({Green.x},{Green.y})B({Blue.x},{Blue.y})R({Red.x},{Red.y})WP({WhitePoint.x},{WhitePoint.y})L({Luminance.max},{Luminance.min})";
            }
        }
    }
}