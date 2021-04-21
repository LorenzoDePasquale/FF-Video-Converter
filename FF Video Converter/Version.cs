using System;


namespace FFVideoConverter
{
    public struct Version
    {
        public int Major { get; }
        public int Minor { get; }
        public int Build { get; }

        public Version(int major, int minor, int build = 0)
        {
            Major = major;
            Minor = minor;
            Build = build;
        }

        public static Version Parse(string version)  //format: v0.1-beta or v1.2 or v1.2.1
        {
            version = version.Substring(1);
            if (version.Contains("-"))
            {
                version = version.Split('-')[0];
            }
            string[] versionNumbers = version.Split('.');
            return new Version(Convert.ToInt32(versionNumbers[0]), Convert.ToInt32(versionNumbers[1]), versionNumbers.Length > 2 ? Convert.ToInt32(versionNumbers[2]) : 0);
        }

        public static bool operator >(Version v1, Version v2)
        {
            if (v1.Major > v2.Major)
            {
                return true;
            }
            else if (v1.Major == v2.Major && v1.Minor > v2.Minor)
            {
                return true;
            }
            else if (v1.Minor == v2.Minor && v1.Build > v2.Build)
            {
                return true;
            }
            return false;
        }

        public static bool operator <(Version v1, Version v2)
        {
            if (v1.Major < v2.Major)
            {
                return true;
            }
            else if (v1.Major == v2.Major && v1.Minor < v2.Minor)
            {
                return true;
            }
            else if (v1.Minor == v2.Minor && v1.Build < v2.Build)
            {
                return true;
            }
            return false;
        }
    }
}