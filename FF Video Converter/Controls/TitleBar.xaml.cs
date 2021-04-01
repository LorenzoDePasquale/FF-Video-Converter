using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace FFVideoConverter.Controls
{
    public partial class TitleBar : UserControl
    {
        public static readonly DependencyProperty ParentWindowProperty = DependencyProperty.RegisterAttached("ParentWindow", typeof(Window), typeof(TitleBar), new UIPropertyMetadata(null));
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TitleBar), new UIPropertyMetadata(""));
        public static readonly DependencyProperty ShowMinimizeButtonProperty = DependencyProperty.RegisterAttached("ShowMinimizeButton", typeof(bool), typeof(TitleBar), new UIPropertyMetadata(true));

        public Window ParentWindow
        {
            get => (Window)GetValue(ParentWindowProperty);
            set => SetValue(ParentWindowProperty, value);
        }
        public string Text 
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
        public bool ShowMinimizeButton
        {
            get => (bool)GetValue(ShowMinimizeButtonProperty);
            set => SetValue(ShowMinimizeButtonProperty, value);
        }


        public TitleBar()
        {
            InitializeComponent();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) ParentWindow.DragMove();
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            ParentWindow.WindowState = WindowState.Minimized;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            ParentWindow.Close();
        }

    }
}