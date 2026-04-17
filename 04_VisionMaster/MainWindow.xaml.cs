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


        private void NodeCard_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 1. 获取当前被点击的节点数据
            if (!(sender is FrameworkElement frameworkElement) ||
                !(frameworkElement.DataContext is FlowNodeViewModel node) ||
                ViewModel == null)
            {
                return;
            }

            // 2. 单击时：选中该节点
            ViewModel.FlowEditor.SelectNode(node);

            // 3. 双击时：拦截底层事件并触发弹窗
            if (e.ClickCount >= 2)
            {
                e.Handled = true; // 关键：吃掉这个事件，防止 Thumb 触发拖拽
                TryOpenNodeEditor(node);
            }
        }

        private bool TryOpenNodeEditor(FlowNodeViewModel node)
        {
            var session = NodeEditorFactory.Create(node, this);

            if (session == null)
            {
                ViewModel.NotifyCanvasAction("双击节点", $"节点“{node.Title}”当前没有可配置的编辑器。");
                return false;
            }

            var editorWindow = new NodeEditorWindow
            {
                Owner = this,
                DataContext = session
            };

            if (editorWindow.ShowDialog() == true)
            {
                session.Apply();
                ViewModel.NotifyCanvasAction("双击节点", $"已更新节点“{node.Title}”配置。");
            }
            else
            {
                ViewModel.NotifyCanvasAction("双击节点", $"已取消节点“{node.Title}”配置。");
            }

            return true;
        }

        private void NodeThumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowNodeViewModel node && ViewModel != null)
            {
                ViewModel.FlowEditor.SelectNode(node);
                ViewModel.NotifyCanvasAction("选择节点", $"已选中模块：{node.Title}");
                FlowCanvas.Focus();
            }
        }

        private void NodeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowNodeViewModel node && ViewModel != null)
            {
                ViewModel.FlowEditor.MoveNode(node, e.HorizontalChange, e.VerticalChange);
                ViewModel.NotifyCanvasAction("移动节点", $"正在移动模块：{node.Title}");
            }
        }

        private void PortThumb_OnDragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is FlowPortViewModel port && ViewModel != null)
            {
                ViewModel.FlowEditor.BeginConnectionDrag(port);

                if (ViewModel.FlowEditor.HasActiveDrag)
                {
                    ViewModel.NotifyCanvasAction("开始连线", "从任意边缘拖出，落到目标节点上即可完成连接。");
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
                ViewModel.NotifyCanvasAction("重连连线", "拖动到新的节点即可完成重连。");
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
                ViewModel.NotifyCanvasAction("选择连线", $"已选中连线：{connection.Description}");
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
                if (ViewModel.DeleteSelectedConnection(out var message))
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

            var canvasPoint = Mouse.GetPosition(FlowCanvas);
            var targetPort = FindPortAt(canvasPoint);
            var targetNode = targetPort == null ? FindNodeAt(canvasPoint) : null;

            ViewModel.FlowEditor.TryCompleteActiveDrag(targetPort, targetNode, canvasPoint, out var message);
            ViewModel.NotifyCanvasAction("连线操作", message);
        }

        private FlowPortViewModel FindPortAt(Point canvasPoint)
        {
            var hit = FlowCanvas.InputHitTest(canvasPoint) as DependencyObject;

            while (hit != null)
            {
                if (hit is FrameworkElement element && element.DataContext is FlowPortViewModel port)
                {
                    return port;
                }

                hit = VisualTreeHelper.GetParent(hit);
            }

            return null;
        }

        private FlowNodeViewModel FindNodeAt(Point canvasPoint)
        {
            var hit = FlowCanvas.InputHitTest(canvasPoint) as DependencyObject;

            while (hit != null)
            {
                if (hit is FrameworkElement element && element.DataContext is FlowNodeViewModel node)
                {
                    return node;
                }

                hit = VisualTreeHelper.GetParent(hit);
            }

            return null;
        }
    }
}
