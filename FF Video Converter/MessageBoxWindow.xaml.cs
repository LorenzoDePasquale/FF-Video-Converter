using System;
using System.Windows;
using System.Windows.Input;


namespace FFVideoConverter
{
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow(string message, string title)
        {
            InitializeComponent();

            textBlockMessage.Text = message;
            labelTitle.Content = title;
        }

        #region "Title Bar controls"

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            //Since the actual size of the window is unknow before creating it, it's necessary to recalculate the layout after the window is rendered, so that SizeToContent works properly 
            InvalidateVisual();
        }
    }
}