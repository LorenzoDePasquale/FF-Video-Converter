using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FFVideoConverter.Encoders;

namespace FFVideoConverter
{
    public enum QueueCompletedAction { Nothing, Sleep, Shutdown };

    public partial class QueueWindow : Window
    {
        public bool QueueActive
        {
            get => buttonStartStopQueue.Content.ToString() == "Stop queue";
            set => buttonStartStopQueue.Content = value ? "Stop queue" : "Start queue";
        }
        public Job RunningJob
        {
            get => (Job)listViewRunningJob.Items[0];
            set
            {
                listViewRunningJob.Items.Clear();
                if (value != null) listViewRunningJob.Items.Add(value);
            }
        }
        public QueueCompletedAction QueueCompletedAction { get => (QueueCompletedAction)comboBoxShutdown.SelectedIndex; }
        public event Action QueueStarted, QueueStopped;

        private Point draggingStartPoint = new Point();
        private int draggedItemIndex = -1;
        private DragAdorner dragAdorner;
        private readonly ObservableCollection<Job> queuedJobs;
        private readonly MainWindow mainWindow;
        private Job selectedJob;
        private bool dropAfter = false;


        public QueueWindow(MainWindow mainWindow, ObservableCollection<Job> queuedJobs)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;
            this.queuedJobs = queuedJobs;
            listViewQueuedJobs.ItemsSource = queuedJobs;
            comboBoxShutdown.Items.Add("Do nothing");
            comboBoxShutdown.Items.Add("Sleep");
            comboBoxShutdown.Items.Add("Shutdown");
            comboBoxShutdown.SelectedIndex = 0;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (selectedJob != null)
            {
                if (new QuestionBoxWindow("Opening this job will remove it from the queue and overwrite your current conversion settings.\nAre you sure you want to open it?", "Queue").ShowDialog() == true)
                {
                    mainWindow.OpenJob(selectedJob);
                    queuedJobs.Remove(selectedJob);
                    Close();
                }
            }
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedJob != null)
            {
                queuedJobs.RemoveAt(listViewQueuedJobs.SelectedIndex);
            }
        }

        private void ButtonStartStopQueue_Click(object sender, RoutedEventArgs e)
        {
            QueueActive = !QueueActive;
            if (QueueActive) QueueStarted?.Invoke();
            else QueueStopped?.Invoke();
        }

        private void ListViewJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = (ListView)sender;
            selectedJob = (Job)listView.SelectedItem;

            stackPanelDetails.Children.Clear();

            if (listView.SelectedIndex == -1)
            {
                buttonEdit.Visibility = Visibility.Hidden;
                buttonRemove.Visibility = Visibility.Hidden;
            }
            else
            {
                buttonEdit.Visibility = Visibility.Visible;
                buttonRemove.Visibility = Visibility.Visible;

                if (selectedJob.JobType == JobType.AudioExport)
                {
                    buttonEdit.IsEnabled = false;

                    stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Track", selectedJob.AudioTrack.Title));
                    stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Codec", selectedJob.AudioTrack.Codec));
                    stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Bitrate", selectedJob.AudioTrack.Bitrate.Kbps.ToString() + " kbps"));
                    stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Size", selectedJob.AudioTrack.Size.ToBytesString()));
                }
                else
                {
                    buttonEdit.IsEnabled = true;

                    ConversionOptions conversionOptions = selectedJob.ConversionOptions;
                    
                    stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Encoder", conversionOptions.Encoder?.ToString() ?? "GIF encoder"));

                    if (conversionOptions.Encoder is not CopyEncoder && conversionOptions.Encoder is not null)
                    {
                        stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Profile", conversionOptions.Encoder.Preset.GetName()));

                        if (conversionOptions.EncodingMode == EncodingMode.ConstantQuality)
                        {
                            stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Quality", conversionOptions.Encoder.Quality.GetName()));
                        }
                        else
                        {
                            string content = conversionOptions.Encoder.Bitrate.Kbps + " Kbps";
                            if (conversionOptions.EncodingMode == EncodingMode.AverageBitrate_FirstPass) content += " (2-pass)";
                            stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Target bitrate", content));
                        }
                        if (conversionOptions.Encoder.PixelFormat != Encoders.PixelFormat.copy)
                        {
                            stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Pixel format", conversionOptions.Encoder.PixelFormat.GetName()));
                        }
                    }

                    foreach (var filter in conversionOptions.Filters)
                    {
                        stackPanelDetails.Children.Add(new Controls.LabelledTextBlock(filter.FilterName, filter.ToString()));
                    }


                    if (conversionOptions.EncodeSections?.Count > 1)
                    {
                        StackPanel stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Vertical;
                        foreach (var item in conversionOptions.EncodeSections)
                        {
                            stackPanel.Children.Add(new TextBlock() { Text = $"{item.Start.ToFormattedString(true)} - {item.End.ToFormattedString(true)}" });
                        }
                        Controls.Expander expander = new Controls.Expander();
                        expander.Header = new Controls.LabelledTextBlock($"Encode segments", conversionOptions.EncodeSections.Count.ToString());
                        expander.Child = stackPanel;
                        stackPanelDetails.Children.Add(expander);
                    }
                    else if (conversionOptions.EncodeSections?.Count == 1)
                    {
                        stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("Start", conversionOptions.EncodeSections.ActualStart.ToFormattedString(true)));
                        stackPanelDetails.Children.Add(new Controls.LabelledTextBlock("End", conversionOptions.EncodeSections.ActualEnd.ToFormattedString(true)));
                    }

                    foreach (var item in conversionOptions.AudioConversionOptions.Values)
                    {
                        StackPanel stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Vertical;
                        stackPanel.Children.Add(new Controls.LabelledTextBlock("Encoder", $"{item.Encoder.Name}"));
                        stackPanel.Children.Add(new Controls.LabelledTextBlock("Bitrate", $"{item.Encoder.Bitrate.Kbps} kbps"));
                        Controls.Expander expander = new Controls.Expander();
                        expander.Header = new Controls.LabelledTextBlock($"Audio track", item.Title);
                        expander.Child = stackPanel;
                        stackPanelDetails.Children.Add(expander);
                    }

                    if (listView == listViewQueuedJobs)
                    {
                        buttonEdit.IsEnabled = true;
                        buttonRemove.IsEnabled = true;
                        listViewRunningJob.SelectionChanged -= ListViewJobs_SelectionChanged;
                        listViewRunningJob.SelectedIndex = -1;
                        listViewRunningJob.SelectionChanged += ListViewJobs_SelectionChanged;
                    }
                    else
                    {
                        buttonEdit.IsEnabled = false;
                        buttonRemove.IsEnabled = false;
                        listViewQueuedJobs.SelectionChanged -= ListViewJobs_SelectionChanged;
                        listViewQueuedJobs.SelectedIndex = -1;
                        listViewQueuedJobs.SelectionChanged += ListViewJobs_SelectionChanged;
                    } 
                }
            }
        }

        #region Dragging

        private void ListViewJobs_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePosition = e.GetPosition(null);
            Vector mouseDifference = draggingStartPoint - mousePosition;

            if (e.LeftButton == MouseButtonState.Pressed && (Math.Abs(mouseDifference.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(mouseDifference.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListViewItem draggedItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (draggedItem != null)
                {
                    Job job = (Job)draggedItem.Content;
                    draggedItemIndex = queuedJobs.IndexOf(job);
                    listViewQueuedJobs.SelectedIndex = -1;
                    //Setup dragAdorner
                    VisualBrush brush = new VisualBrush(draggedItem);
                    dragAdorner = new DragAdorner(listViewQueuedJobs, draggedItem.RenderSize, brush);
                    dragAdorner.Opacity = 0.75;
                    AdornerLayer layer = AdornerLayer.GetAdornerLayer(listViewQueuedJobs);
                    layer.Add(dragAdorner);
                    insertionLine.Visibility = Visibility.Visible;
                    //Do drag-drop
                    DataObject dragData = new DataObject("Job", job);
                    DragDrop.DoDragDrop(draggedItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
                    //Drag ended
                    layer.Remove(dragAdorner);
                    dragAdorner = null;
                    insertionLine.Visibility = Visibility.Collapsed;
                    draggedItemIndex = -1;
                }
            }
        }

        private void ListViewJobs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            draggingStartPoint = e.GetPosition(null);
        }

        private void ListViewJobs_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("Job") || sender != e.Source)
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ListViewJobs_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Job") && sender == e.Source)
            {
                // Get the drop ListViewItem destination
                ListViewItem draggedOverItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                e.Effects = DragDropEffects.Move;
                int newIndex;
                if (draggedOverItem == null) //Dropped in empty space -> becomes last item
                {
                    newIndex = queuedJobs.Count - 1;
                    if (draggedItemIndex >= 0)
                    {
                        queuedJobs.Move(draggedItemIndex, newIndex);
                    }
                }
                else
                {
                    Job job = (Job)draggedOverItem.Content;
                    newIndex = queuedJobs.IndexOf(job);
                    if (newIndex > draggedItemIndex)
                    {
                        newIndex--;
                    }
                    if (dropAfter && newIndex < queuedJobs.Count - 1)
                    {
                        newIndex++;
                    }
                    if (draggedItemIndex >= 0 && newIndex >= 0)
                    {
                        queuedJobs.Move(draggedItemIndex, newIndex);
                    }
                }
                listViewQueuedJobs.SelectedIndex = newIndex;
            }
        }

        private void ListViewJobs_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;

            //Move dragAdorner
            Point mousePosition = e.GetPosition(listViewQueuedJobs);
            ListViewItem itemBeingDragged = (ListViewItem)listViewQueuedJobs.ItemContainerGenerator.ContainerFromIndex(draggedItemIndex);
            double topOffset = mousePosition.Y - itemBeingDragged.RenderSize.Height / 2;
            dragAdorner.SetOffsets(mousePosition.X - draggingStartPoint.X, topOffset);

            //Move insertionLine
            ListViewItem draggedOverItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (draggedOverItem != null)
            {
                mousePosition = e.GetPosition(draggedOverItem);
                dropAfter = mousePosition.Y > draggedOverItem.RenderSize.Height / 2;
                Point draggedOverPosition = draggedOverItem.TransformToAncestor(listViewQueuedJobs).Transform(new Point(0, listViewQueuedJobs.Margin.Top));
                double verticalPosition = draggedOverPosition.Y - 5;
                if (dropAfter) verticalPosition += draggedOverItem.RenderSize.Height;
                insertionLine.Margin = new Thickness(0, verticalPosition, 0, 0);
            }
        }

        private T FindAnchestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        #endregion
    }
}