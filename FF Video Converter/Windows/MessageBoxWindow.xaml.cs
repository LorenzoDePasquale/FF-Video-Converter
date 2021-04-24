using System;
using System.Windows;


namespace FFVideoConverter
{
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow(string message, string title)
        {
            InitializeComponent();

            textBlockMessage.Text = message;
            titleBar.Text = title;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Since the actual size of the window is unknow before creating it, it's necessary to recalculate the layout after the window is rendered, so that SizeToContent works properly 
            InvalidateVisual();
        }
    }
}