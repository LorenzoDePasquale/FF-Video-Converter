using System.Globalization;
using System.Windows;


namespace FFVideoConverter
{
    public partial class App : Application
    {
        public App()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }
    }
}
