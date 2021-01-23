using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;


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
        public event Action QueueStarted;

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

        #region Title Bar controls

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        #endregion

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
        }

        private void ListViewJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = (ListView)sender;
            selectedJob = (Job)listView.SelectedItem;
            if (listView.SelectedIndex == -1)
            {
                stackPanelContent.Visibility = Visibility.Hidden;
                stackPanelLabel.Visibility = Visibility.Hidden;
                buttonEdit.Visibility = Visibility.Hidden;
                buttonRemove.Visibility = Visibility.Hidden;
            }
            else
            {
                stackPanelContent.Visibility = Visibility.Visible;
                stackPanelLabel.Visibility = Visibility.Visible;
                buttonEdit.Visibility = Visibility.Visible;
                buttonRemove.Visibility = Visibility.Visible;

                ConversionOptions conversionOptions = selectedJob.ConversionOptions;

                textBlockEncoder.Text = conversionOptions.Encoder.ToString();
                if (conversionOptions.Encoder is NativeEncoder)
                {
                    textBlockProfile.Visibility = Visibility.Collapsed;
                    textBlockProfileLabel.Visibility = Visibility.Collapsed;
                    textBlockQuality.Visibility = Visibility.Collapsed;
                    textBlockTargetSize.Visibility = Visibility.Collapsed;
                    textBlockQualityLabel.Visibility = Visibility.Collapsed;
                    textBlockTargetSizeLabel.Visibility = Visibility.Collapsed;
                    textBlockFramerate.Visibility = Visibility.Collapsed;
                    textBlockFramerateLabel.Visibility = Visibility.Collapsed;
                    textBlockResolution.Visibility = Visibility.Collapsed;
                    textBlockResolutionLabel.Visibility = Visibility.Collapsed;
                    textBlockCrop.Visibility = Visibility.Collapsed;
                    textBlockCropLabel.Visibility = Visibility.Collapsed;
                    textBlockRotation.Visibility = Visibility.Collapsed;
                    textBlockRotationLabel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    textBlockProfile.Visibility = Visibility.Visible;
                    textBlockProfileLabel.Visibility = Visibility.Visible;
                    textBlockFramerate.Visibility = Visibility.Visible;
                    textBlockFramerateLabel.Visibility = Visibility.Visible;
                    textBlockCrop.Visibility = Visibility.Visible;
                    textBlockCropLabel.Visibility = Visibility.Visible;
                    textBlockResolution.Visibility = Visibility.Visible;
                    textBlockResolutionLabel.Visibility = Visibility.Visible;
                    textBlockRotation.Visibility = Visibility.Visible;
                    textBlockRotationLabel.Visibility = Visibility.Visible;
                    textBlockProfile.Text = conversionOptions.Encoder.Preset.GetName();
                    if (conversionOptions.EncodingMode == EncodingMode.ConstantQuality)
                    {
                        textBlockQuality.Visibility = Visibility.Visible;
                        textBlockTargetSize.Visibility = Visibility.Collapsed;
                        textBlockQualityLabel.Visibility = Visibility.Visible;
                        textBlockTargetSizeLabel.Visibility = Visibility.Collapsed;
                        textBlockQuality.Text = conversionOptions.Encoder.Quality.GetName();
                    }
                    else
                    {
                        textBlockQuality.Visibility = Visibility.Collapsed;
                        textBlockTargetSize.Visibility = Visibility.Visible;
                        textBlockQualityLabel.Visibility = Visibility.Collapsed;
                        textBlockTargetSizeLabel.Visibility = Visibility.Visible;
                        textBlockTargetSize.Text = (selectedJob.SourceInfo.Size * (long)selectedJob.SliderTargetSizeValue / 100).ToBytesString();
                    }
                    if (conversionOptions.Framerate > 0)
                    {
                        textBlockFramerate.Text = conversionOptions.Framerate.ToString() + " fps";
                    }
                    else
                    {
                        textBlockFramerate.Text = "same as source";
                    }
                    if (conversionOptions.CropData.HasValue())
                    {
                        textBlockResolution.Visibility = Visibility.Collapsed;
                        textBlockResolutionLabel.Visibility = Visibility.Collapsed;
                        textBlockCrop.Text = conversionOptions.CropData.ToString();
                    }
                    else
                    {
                        textBlockCrop.Visibility = Visibility.Collapsed;
                        textBlockCropLabel.Visibility = Visibility.Collapsed;
                        textBlockResolution.Text = conversionOptions.Resolution.ToString();
                    }
                    textBlockRotation.Text = conversionOptions.Rotation.ToString();
                }

                if (conversionOptions.EncodeSections?.Count > 0)
                {
                    textBlockStart.Visibility = Visibility.Visible;
                    textBlockStartLabel.Visibility = Visibility.Visible;
                    textBlockStart.Text = conversionOptions.EncodeSections.ActualStart.ToFormattedString(true);
                    textBlockEnd.Visibility = Visibility.Visible;
                    textBlockEndLabel.Visibility = Visibility.Visible;
                    textBlockEnd.Text = conversionOptions.EncodeSections.ActualEnd.ToFormattedString(true);
                }
                else
                {
                    textBlockStart.Visibility = Visibility.Collapsed;
                    textBlockStartLabel.Visibility = Visibility.Collapsed;
                    textBlockEnd.Visibility = Visibility.Collapsed;
                    textBlockEndLabel.Visibility = Visibility.Collapsed;
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