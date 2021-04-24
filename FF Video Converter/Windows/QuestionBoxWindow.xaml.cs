using System;
using System.Windows;


namespace FFVideoConverter
{
    public partial class QuestionBoxWindow : Window
    {
        public QuestionBoxWindow(string question, string title)
        {
            InitializeComponent();

            textBlockQuestion.Text = question;
            titleBar.Text = title;
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Since the actual size of the window is unknow before creating it, it's necessary to recalculate the layout after the window is rendered, so that SizeToContent works properly 
            InvalidateVisual();
        }
    }
}
