using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;


namespace FFVideoConverter
{
    public partial class QueueWindow : Window
    {
        private Point draggingStartPoint = new Point();
        private int draggedItemIndex = -1;
        private DragAdorner dragAdorner;
        private readonly ObservableCollection<Job> queuedJobs;
        private readonly ObservableCollection<Job> completedJobs;


        public QueueWindow(ObservableCollection<Job> queuedJobs, ObservableCollection<Job> completedJobs)
        {
            InitializeComponent();

            this.queuedJobs = queuedJobs;
            this.completedJobs = completedJobs;
            listViewJobs.ItemsSource = queuedJobs;
            listViewCompletedJobs.ItemsSource = completedJobs;
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

        #endregion


        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            if (listViewJobs.SelectedIndex > -1)
            {
                queuedJobs.RemoveAt(listViewJobs.SelectedIndex);
            }

            //TODO: if user removes the job that is running, ask confirmation before removing it, then ask MainWindow to stop it before removing
        }

        private void ListViewJobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listViewJobs.SelectedIndex == -1) return;

            ConversionOptions conversionOptions = queuedJobs[listViewJobs.SelectedIndex].ConversionOptions;

            textBlockEncoder.Text = conversionOptions.Encoder.ToString();
            if (conversionOptions.Encoder is NativeEncoder)
            {
                textBlockProfile.Visibility = Visibility.Collapsed;
                textBlockProfileLabel.Visibility = Visibility.Collapsed;
                textBlockQuality.Visibility = Visibility.Collapsed;
                textBlockQualityLabel.Visibility = Visibility.Collapsed;
                textBlockFramerate.Visibility = Visibility.Collapsed;
                textBlockFramerateLabel.Visibility = Visibility.Collapsed;
                textBlockResolution.Visibility = Visibility.Collapsed;
                textBlockResolutionLabel.Visibility = Visibility.Collapsed;
                textBlockCrop.Visibility = Visibility.Collapsed;
                textBlockCropLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                textBlockProfile.Visibility = Visibility.Visible;
                textBlockProfileLabel.Visibility = Visibility.Visible;
                textBlockQuality.Visibility = Visibility.Visible;
                textBlockQualityLabel.Visibility = Visibility.Visible;
                textBlockFramerate.Visibility = Visibility.Visible;
                textBlockFramerateLabel.Visibility = Visibility.Visible;
                textBlockCrop.Visibility = Visibility.Visible;
                textBlockCropLabel.Visibility = Visibility.Visible;
                textBlockResolution.Visibility = Visibility.Visible;
                textBlockResolutionLabel.Visibility = Visibility.Visible; 
                textBlockProfile.Text = conversionOptions.Encoder.Preset.GetName();
                textBlockQuality.Text = conversionOptions.Encoder.Quality.GetName();
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
            }
            if (conversionOptions.Start != TimeSpan.Zero)
            {
                textBlockStart.Visibility = Visibility.Visible;
                textBlockStartLabel.Visibility = Visibility.Visible;
                textBlockStart.Text = conversionOptions.Start.ToString(@"hh\:mm\:ss\.ff");
            }
            else
            {
                textBlockStart.Visibility = Visibility.Collapsed;
                textBlockStartLabel.Visibility = Visibility.Collapsed;
            }
            if (conversionOptions.End != TimeSpan.Zero)
            {
                textBlockEnd.Visibility = Visibility.Visible;
                textBlockEndLabel.Visibility = Visibility.Visible;
                textBlockEnd.Text = conversionOptions.End.ToString(@"hh\:mm\:ss\.ff");
            }
            else
            {
                textBlockEnd.Visibility = Visibility.Collapsed;
                textBlockEndLabel.Visibility = Visibility.Collapsed;
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
                    //Setup dragAdorner
                    VisualBrush brush = new VisualBrush(draggedItem);
                    dragAdorner = new DragAdorner(listViewJobs, draggedItem.RenderSize, brush);
                    AdornerLayer layer = AdornerLayer.GetAdornerLayer(listViewJobs);
                    layer.Add(dragAdorner);
                    //Do drag-drop
                    DataObject dragData = new DataObject("Job", job);
                    DragDrop.DoDragDrop(draggedItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
                    //Drag ended
                    layer.Remove(dragAdorner);
                    dragAdorner = null;
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
                    if (draggedItemIndex >= 0 && newIndex >= 0)
                    {
                        queuedJobs.Move(draggedItemIndex, newIndex);
                    }
                }
            }
        }

        private void ListViewJobs_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            Point mousePosition = e.GetPosition(listViewJobs);
            ListViewItem itemBeingDragged = (ListViewItem)listViewJobs.ItemContainerGenerator.ContainerFromIndex(draggedItemIndex);
            Point itemPosition = itemBeingDragged.TranslatePoint(new Point(0, 0), listViewJobs);
            double topOffset = mousePosition.Y - itemBeingDragged.RenderSize.Height / 2;
            dragAdorner.SetOffsets(itemPosition.X, topOffset);
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