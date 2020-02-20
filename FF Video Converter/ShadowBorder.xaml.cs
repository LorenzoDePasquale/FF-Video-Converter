using System.Windows;
using System.Windows.Controls;

namespace FFVideoConverter
{
    public partial class ShadowBorder : UserControl
    {
        public CornerRadius CornerRadius 
        { 
            get
            {
                return border.CornerRadius;
            }
            set
            {
                border.CornerRadius = value;
            }
        }

        public ShadowBorder()
        {
            InitializeComponent();
            CornerRadius = new CornerRadius(4);
        }
    }
}
