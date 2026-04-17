using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VmFunModule;

namespace VisionMaster
{
    public partial class MainWindow : Window
    {
        private CameraManagerWindow cameraManagerWindow;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += OnLoaded;

            if (ViewModel != null)
            {
                ViewModel.CameraWindowRequested += ViewModelOnCameraWindowRequested;
            }
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Focus();
            FlowCanvas.Focus();
        }

        private void ViewModelOnCameraWindowRequested(object sender, System.EventArgs e)
        {
            if (cameraManagerWindow == null)
            {
                cameraManagerWindow = new CameraManagerWindow
                {
                    Owner = this
                };
                cameraManagerWindow.Closed += CameraManagerWindowOnClosed;
            }

            cameraManagerWindow.Show();
            cameraManagerWindow.Activate();
        }

        private void CameraManagerWindowOnClosed(object sender, System.EventArgs e)
        {
            cameraManagerWindow.Closed -= CameraManagerWindowOnClosed;
            cameraManagerWindow = null;
        }

        private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NodeThumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowNodeViewModel node && ViewModel != null)
            {
                ViewModel.FlowEditor.SelectNode(node);
                ViewModel.NotifyCanvasAction("选择节点", string.Format("已选中模块：{0}", node.Title));
                FlowCanvas.Focus();
            }
        }

        private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowNodeViewModel node && ViewModel != null)
            {
                ViewModel.FlowEditor.MoveNode(node, e.HorizontalChange, e.VerticalChange);
                ViewModel.NotifyCanvasAction("移动节点", string.Format("正在移动模块：{0}", node.Title));
            }
        }

        private void PortThumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowPortViewModel port && ViewModel != null)
            {
                ViewModel.FlowEditor.BeginConnectionDrag(port);

                if (ViewModel.FlowEditor.HasActiveDrag)
                {
                    ViewModel.NotifyCanvasAction("开始连线", "拖动到输入端口以创建连线。");
                    FlowCanvas.Focus();
                }
            }
        }

        private void PortThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel != null && ViewModel.FlowEditor.HasActiveDrag)
            {
                ViewModel.FlowEditor.UpdateActiveDrag(Mouse.GetPosition(FlowCanvas));
            }
        }

        private void PortThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            CompleteActiveConnectionDrag();
        }

        private void ConnectionHandleThumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (ViewModel == null || ViewModel.FlowEditor.SelectedConnection == null)
            {
                return;
            }

            if (sender is Thumb thumb && thumb.Tag is string tag)
            {
                var handleKind = tag == "Source" ? FlowConnectionHandleKind.Source : FlowConnectionHandleKind.Target;
                ViewModel.FlowEditor.BeginReconnectDrag(ViewModel.FlowEditor.SelectedConnection, handleKind);
                ViewModel.NotifyCanvasAction("重连连线", "拖动到新的合法端口以完成重连。");
                FlowCanvas.Focus();
            }
        }

        private void ConnectionHandleThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel != null && ViewModel.FlowEditor.HasActiveDrag)
            {
                ViewModel.FlowEditor.UpdateActiveDrag(Mouse.GetPosition(FlowCanvas));
            }
        }

        private void ConnectionHandleThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            CompleteActiveConnectionDrag();
        }

        private void ConnectionHitPath_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Path path && path.DataContext is FlowConnectionViewModel connection && ViewModel != null)
            {
                ViewModel.FlowEditor.SelectConnection(connection);
                ViewModel.NotifyCanvasAction("选择连线", string.Format("已选中连线：{0}", connection.Description));
                FlowCanvas.Focus();
                e.Handled = true;
            }
        }

        private void FlowCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            if (e.OriginalSource == FlowCanvas)
            {
                ViewModel.FlowEditor.ClearSelection();
                ViewModel.NotifyCanvasAction("清空选择", "已取消当前节点和连线选择。");
            }

            FlowCanvas.Focus();
        }

        private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewModel != null)
            {
                string message;

                if (ViewModel.DeleteSelectedConnection(out message))
                {
                    e.Handled = true;
                    return;
                }

                ViewModel.NotifyCanvasAction("删除连线", message);
            }
        }

        private void CompleteActiveConnectionDrag()
        {
            if (ViewModel == null || !ViewModel.FlowEditor.HasActiveDrag)
            {
                return;
            }

            var targetPort = FindPortAt(Mouse.GetPosition(FlowCanvas));
            string message;
            ViewModel.FlowEditor.TryCompleteActiveDrag(targetPort, out message);
            ViewModel.NotifyCanvasAction("连线操作", message);
        }

        private FlowPortViewModel FindPortAt(Point canvasPoint)
        {
            var hit = FlowCanvas.InputHitTest(canvasPoint) as DependencyObject;

            while (hit != null)
            {
                var element = hit as FrameworkElement;

                if (element != null && element.DataContext is FlowPortViewModel port)
                {
                    return port;
                }

                hit = VisualTreeHelper.GetParent(hit);
            }

            return null;
        }
    }
}
