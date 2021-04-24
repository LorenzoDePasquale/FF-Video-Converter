using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;


namespace FFVideoConverter
{
    public partial class CompletedWindow : Window
    {
        private readonly MainWindow mainWindow;

        public CompletedWindow(MainWindow mainWindow, ObservableCollection<Job> completedJobs)
        {
            InitializeComponent();
            
            this.mainWindow = mainWindow;
            listViewCompletedJobs.ItemsSource = completedJobs;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            // If the job is removed from the listview, the application crashes because for some fucking reason the framework, after removing the item, instead of fucking deleting it, sets the IsExpanded property of the expander to false, 
            // then tries to animate the expander that doesn't exists anymore and cries because it's fucking null...
            // That's why the only way to remove a job is to set it's corresponding listviewitem to collapsed
            Button button = (Button)sender;
            ListViewItem selectedItem = (ListViewItem)button.TemplatedParent;
            selectedItem.Visibility = Visibility.Collapsed;
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Job selectedJob = (Job)button.DataContext;
            if (File.Exists(selectedJob.Destination))
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = selectedJob.Destination,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                new MessageBoxWindow($"\"{selectedJob.Destination}\" does not exist anymore", "FF Video Converter").ShowDialog();
            }
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (new QuestionBoxWindow("Opening this job will overwrite your current conversion settings.\nAre you sure you want to open it?", "FF Video Converter").ShowDialog() == true)
            {
                Button button = (Button)sender;
                mainWindow.OpenJob((Job)button.DataContext);
            }
        }
    }


    public class MultiplyConverter : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double result = 1.0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is double)
                    result *= (double)values[i];
            }

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new Exception("Not implemented");
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}