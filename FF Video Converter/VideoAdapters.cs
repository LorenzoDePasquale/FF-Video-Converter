using System.Collections.Generic;
using System.Management;


namespace FFVideoConverter
{
    public static class VideoAdapters
    {
        public static List<string> ADAPTERS = new List<string>();

        static VideoAdapters()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                ADAPTERS.Add(obj["Name"].ToString().ToLower());
            }
        }

        public static bool Contains(string name)
        {
            foreach (var item in ADAPTERS)
            {
                if (item.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }
    }
}