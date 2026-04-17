using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VmFunBase;

namespace VmFunModule
{
    public sealed class FlowEditorViewModel : ViewModelBase
    {
        private readonly Dictionary<Guid, FlowPortViewModel> portLookup = new Dictionary<Guid, FlowPortViewModel>();
        private FlowNodeViewModel selectedNode;
        private FlowConnectionViewModel selectedConnection;
        private ActiveConnectionDrag activeConnectionDrag;

        public FlowEditorViewModel()
        {
            Nodes = new ObservableCollection<FlowNodeViewModel>();
            Connections = new ObservableCollection<FlowConnectionViewModel>();
            DraftConnection = new FlowDraftConnectionViewModel();
        }

        public ObservableCollection<FlowNodeViewModel> Nodes { get; }

        public ObservableCollection<FlowConnectionViewModel> Connections { get; }

        public FlowDraftConnectionViewModel DraftConnection { get; }

        public FlowNodeViewModel SelectedNode
        {
            get { return selectedNode; }
            private set
            {
                if (SetProperty(ref selectedNode, value))
                {
                    OnPropertyChanged(nameof(HasSelectedNode));
                }
            }
        }

        public FlowConnectionViewModel SelectedConnection
        {
            get { return selectedConnection; }
            private set
            {
                if (SetProperty(ref selectedConnection, value))
                {
                    OnPropertyChanged(nameof(HasSelectedConnection));
                }
            }
        }

        public bool HasSelectedNode
        {
            get { return SelectedNode != null; }
        }

        public bool HasSelectedConnection
        {
            get { return SelectedConnection != null; }
        }

        public bool HasActiveDrag
        {
            get { return activeConnectionDrag != null; }
        }

        public FlowNodeViewModel AddNode(string title, string description, string tag, string glyph, FlowNodeKind nodeKind, Brush accentBrush, double x, double y)
        {
            var node = FlowNodeViewModel.Create(title, description, tag, glyph, nodeKind, accentBrush, x, y);
            RegisterPorts(node);
            Nodes.Add(node);
            SelectNode(node);
            return node;
        }

        public bool TryCreateConnection(FlowPortViewModel sourcePort, FlowPortViewModel targetPort, out string message)
        {
            if (sourcePort == null || targetPort == null)
            {
                message = "连线端口不能为空。";
                return false;
            }

            return TryCreateOrUpdateConnection(sourcePort.Id, targetPort.Id, null, out message);
        }

        public void SelectNode(FlowNodeViewModel node)
        {
            foreach (var item in Nodes)
            {
                item.IsSelected = item == node;
            }

            if (SelectedConnection != null)
            {
                SelectedConnection.IsSelected = false;
            }

            SelectedConnection = null;
            SelectedNode = node;
        }

        public void SelectConnection(FlowConnectionViewModel connection)
        {
            foreach (var item in Connections)
            {
                item.IsSelected = item == connection;
            }

            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = false;
            }

            SelectedNode = null;
            SelectedConnection = connection;
        }

        public void ClearSelection()
        {
            foreach (var node in Nodes)
            {
                node.IsSelected = false;
            }

            foreach (var connection in Connections)
            {
                connection.IsSelected = false;
            }

            SelectedNode = null;
            SelectedConnection = null;
        }

        public void MoveNode(FlowNodeViewModel node, double deltaX, double deltaY)
        {
            if (node == null)
            {
                return;
            }

            node.X = Math.Max(24, node.X + deltaX);
            node.Y = Math.Max(24, node.Y + deltaY);
            SelectNode(node);
            RebuildConnectionsForNode(node.Id);
        }

        public bool DeleteSelectedConnection(out string message)
        {
            if (SelectedConnection == null)
            {
                message = "当前没有选中的连线。";
                return false;
            }

            var description = SelectedConnection.Description;
            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            message = string.Format("已删除连线：{0}", description);
            return true;
        }

        public void BeginConnectionDrag(FlowPortViewModel port)
        {
            if (port == null || port.Kind != FlowPortKind.Output)
            {
                return;
            }

            ClearSelection();
            activeConnectionDrag = ActiveConnectionDrag.CreateNew(port.Id);
            DraftConnection.Show(GetAbsolutePoint(port), GetAbsolutePoint(port));
            OnPropertyChanged(nameof(HasActiveDrag));
        }

        public void BeginReconnectDrag(FlowConnectionViewModel connection, FlowConnectionHandleKind handleKind)
        {
            if (connection == null)
            {
                return;
            }

            SelectConnection(connection);

            activeConnectionDrag = handleKind == FlowConnectionHandleKind.Source
                ? ActiveConnectionDrag.CreateReconnectSource(connection.Id, connection.TargetPortId)
                : ActiveConnectionDrag.CreateReconnectTarget(connection.Id, connection.SourcePortId);

            UpdateActiveDrag(handleKind == FlowConnectionHandleKind.Source ? connection.StartPoint : connection.EndPoint);
            OnPropertyChanged(nameof(HasActiveDrag));
        }

        public void UpdateActiveDrag(Point canvasPoint)
        {
            if (activeConnectionDrag == null)
            {
                return;
            }

            var fixedPort = portLookup[activeConnectionDrag.FixedPortId];
            var fixedPoint = GetAbsolutePoint(fixedPort);

            if (activeConnectionDrag.IsStartDynamic)
            {
                DraftConnection.Show(canvasPoint, fixedPoint);
            }
            else
            {
                DraftConnection.Show(fixedPoint, canvasPoint);
            }
        }

        public bool TryCompleteActiveDrag(FlowPortViewModel targetPort, out string message)
        {
            if (activeConnectionDrag == null)
            {
                message = "当前没有正在进行的连线操作。";
                return false;
            }

            if (targetPort == null)
            {
                CancelActiveDrag();
                message = "已取消连线。";
                return false;
            }

            if (targetPort.Kind != activeConnectionDrag.ExpectedPortKind)
            {
                CancelActiveDrag();
                message = activeConnectionDrag.ExpectedPortKind == FlowPortKind.Input
                    ? "新建连线时必须拖到输入端口。"
                    : "重连起点时必须拖到输出端口。";
                return false;
            }

            Guid sourcePortId;
            Guid targetPortId;

            if (activeConnectionDrag.IsStartDynamic)
            {
                sourcePortId = targetPort.Id;
                targetPortId = activeConnectionDrag.FixedPortId;
            }
            else
            {
                sourcePortId = activeConnectionDrag.FixedPortId;
                targetPortId = targetPort.Id;
            }

            var result = TryCreateOrUpdateConnection(sourcePortId, targetPortId, activeConnectionDrag.ConnectionId, out message);
            CancelActiveDrag();
            return result;
        }

        public void CancelActiveDrag()
        {
            activeConnectionDrag = null;
            DraftConnection.Hide();
            OnPropertyChanged(nameof(HasActiveDrag));
        }

        public Point GetAbsolutePoint(FlowPortViewModel port)
        {
            if (port == null)
            {
                return new Point();
            }

            var node = Nodes.First(item => item.Id == port.NodeId);
            return new Point(node.X + port.CenterX, node.Y + port.CenterY);
        }

        private void RegisterPorts(FlowNodeViewModel node)
        {
            foreach (var port in node.InputPorts.Concat(node.OutputPorts))
            {
                portLookup[port.Id] = port;
            }
        }

        private bool TryCreateOrUpdateConnection(Guid sourcePortId, Guid targetPortId, Guid? existingConnectionId, out string message)
        {
            var sourcePort = portLookup[sourcePortId];
            var targetPort = portLookup[targetPortId];

            if (!CanCreateConnection(sourcePort, targetPort, existingConnectionId, out message))
            {
                return false;
            }

            var connection = existingConnectionId.HasValue
                ? Connections.First(item => item.Id == existingConnectionId.Value)
                : new FlowConnectionViewModel();

            connection.SourcePortId = sourcePort.Id;
            connection.TargetPortId = targetPort.Id;
            connection.Description = string.Format("{0} -> {1}", GetNodeTitle(sourcePort.Id), GetNodeTitle(targetPort.Id));

            UpdateConnectionGeometry(connection);

            if (!existingConnectionId.HasValue)
            {
                Connections.Add(connection);
            }

            SelectConnection(connection);

            message = existingConnectionId.HasValue
                ? string.Format("已更新连线：{0}", connection.Description)
                : string.Format("已创建连线：{0}", connection.Description);

            return true;
        }

        private bool CanCreateConnection(FlowPortViewModel sourcePort, FlowPortViewModel targetPort, Guid? existingConnectionId, out string message)
        {
            if (sourcePort.Kind != FlowPortKind.Output)
            {
                message = "连线起点必须是输出端口。";
                return false;
            }

            if (targetPort.Kind != FlowPortKind.Input)
            {
                message = "连线终点必须是输入端口。";
                return false;
            }

            if (sourcePort.NodeId == targetPort.NodeId)
            {
                message = "不允许模块自连。";
                return false;
            }

            if (Connections.Any(item => item.Id != existingConnectionId && item.TargetPortId == targetPort.Id))
            {
                message = "该输入端口已经被占用。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void RebuildConnectionsForNode(Guid nodeId)
        {
            foreach (var connection in Connections)
            {
                var sourcePort = portLookup[connection.SourcePortId];
                var targetPort = portLookup[connection.TargetPortId];

                if (sourcePort.NodeId == nodeId || targetPort.NodeId == nodeId)
                {
                    UpdateConnectionGeometry(connection);
                }
            }
        }

        private void UpdateConnectionGeometry(FlowConnectionViewModel connection)
        {
            var sourcePoint = GetAbsolutePoint(portLookup[connection.SourcePortId]);
            var targetPoint = GetAbsolutePoint(portLookup[connection.TargetPortId]);
            connection.UpdateGeometry(BuildOrthogonalGeometry(sourcePoint, targetPoint), sourcePoint, targetPoint);
        }

        private string GetNodeTitle(Guid portId)
        {
            var port = portLookup[portId];
            return Nodes.First(item => item.Id == port.NodeId).Title;
        }

        private static PathGeometry BuildOrthogonalGeometry(Point start, Point end)
        {
            var points = new List<Point> { start };
            const double laneOffset = 40.0;

            if (end.X >= start.X + laneOffset)
            {
                var middleX = start.X + ((end.X - start.X) / 2.0);
                AddPoint(points, new Point(middleX, start.Y));
                AddPoint(points, new Point(middleX, end.Y));
            }
            else
            {
                var doglegX = Math.Max(start.X, end.X) + laneOffset;
                AddPoint(points, new Point(doglegX, start.Y));
                AddPoint(points, new Point(doglegX, end.Y));
            }

            AddPoint(points, end);

            var figure = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = false,
                IsFilled = false
            };

            if (points.Count > 1)
            {
                figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
            }

            return new PathGeometry(new[] { figure });
        }

        private static void AddPoint(IList<Point> points, Point point)
        {
            if (!points.Any() || points[points.Count - 1] != point)
            {
                points.Add(point);
            }
        }

        private sealed class ActiveConnectionDrag
        {
            private ActiveConnectionDrag(Guid? connectionId, Guid fixedPortId, FlowPortKind expectedPortKind, bool isStartDynamic)
            {
                ConnectionId = connectionId;
                FixedPortId = fixedPortId;
                ExpectedPortKind = expectedPortKind;
                IsStartDynamic = isStartDynamic;
            }

            public Guid? ConnectionId { get; }

            public Guid FixedPortId { get; }

            public FlowPortKind ExpectedPortKind { get; }

            public bool IsStartDynamic { get; }

            public static ActiveConnectionDrag CreateNew(Guid sourcePortId)
            {
                return new ActiveConnectionDrag(null, sourcePortId, FlowPortKind.Input, false);
            }

            public static ActiveConnectionDrag CreateReconnectSource(Guid connectionId, Guid targetPortId)
            {
                return new ActiveConnectionDrag(connectionId, targetPortId, FlowPortKind.Output, true);
            }

            public static ActiveConnectionDrag CreateReconnectTarget(Guid connectionId, Guid sourcePortId)
            {
                return new ActiveConnectionDrag(connectionId, sourcePortId, FlowPortKind.Input, false);
            }
        }
    }

    public enum FlowNodeKind
    {
        Source,
        Process,
        Sink
    }

    public enum FlowPortKind
    {
        Input,
        Output
    }

    public enum FlowConnectionHandleKind
    {
        Source,
        Target
    }

    public sealed class FlowNodeViewModel : ViewModelBase
    {
        private double x;
        private double y;
        private bool isSelected;

        private FlowNodeViewModel()
        {
            Id = Guid.NewGuid();
            Width = 184;
            Height = 96;
            InputPorts = new ObservableCollection<FlowPortViewModel>();
            OutputPorts = new ObservableCollection<FlowPortViewModel>();
        }

        public Guid Id { get; private set; }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public string Tag { get; private set; }

        public string Glyph { get; private set; }

        public Brush AccentBrush { get; private set; }

        public FlowNodeKind NodeKind { get; private set; }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public ObservableCollection<FlowPortViewModel> InputPorts { get; private set; }

        public ObservableCollection<FlowPortViewModel> OutputPorts { get; private set; }

        public double X
        {
            get { return x; }
            set { SetProperty(ref x, value); }
        }

        public double Y
        {
            get { return y; }
            set { SetProperty(ref y, value); }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set { SetProperty(ref isSelected, value); }
        }

        public static FlowNodeViewModel Create(string title, string description, string tag, string glyph, FlowNodeKind nodeKind, Brush accentBrush, double x, double y)
        {
            var node = new FlowNodeViewModel
            {
                Title = title,
                Description = description,
                Tag = tag,
                Glyph = glyph,
                NodeKind = nodeKind,
                AccentBrush = accentBrush,
                X = x,
                Y = y
            };

            const double portRadius = 7.0;
            var middleY = node.Height / 2.0;

            if (nodeKind != FlowNodeKind.Source)
            {
                node.InputPorts.Add(new FlowPortViewModel(node.Id, FlowPortKind.Input, 0, middleY, portRadius));
            }

            if (nodeKind != FlowNodeKind.Sink)
            {
                node.OutputPorts.Add(new FlowPortViewModel(node.Id, FlowPortKind.Output, node.Width, middleY, portRadius));
            }

            return node;
        }
    }

    public sealed class FlowPortViewModel
    {
        public FlowPortViewModel(Guid nodeId, FlowPortKind kind, double centerX, double centerY, double radius)
        {
            Id = Guid.NewGuid();
            NodeId = nodeId;
            Kind = kind;
            CenterX = centerX;
            CenterY = centerY;
            Radius = radius;
        }

        public Guid Id { get; }

        public Guid NodeId { get; }

        public FlowPortKind Kind { get; }

        public double CenterX { get; }

        public double CenterY { get; }

        public double Radius { get; }

        public double Diameter
        {
            get { return Radius * 2.0; }
        }

        public double Left
        {
            get { return CenterX - Radius; }
        }

        public double Top
        {
            get { return CenterY - Radius; }
        }
    }

    public sealed class FlowConnectionViewModel : ViewModelBase
    {
        private static readonly Brush NormalStroke = new SolidColorBrush(Color.FromRgb(255, 138, 36));
        private static readonly Brush SelectedStroke = new SolidColorBrush(Color.FromRgb(255, 197, 120));

        private PathGeometry geometry;
        private Point startPoint;
        private Point endPoint;
        private bool isSelected;
        private string description;

        public FlowConnectionViewModel()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }

        public Guid SourcePortId { get; set; }

        public Guid TargetPortId { get; set; }

        public string Description
        {
            get { return description; }
            set { SetProperty(ref description, value); }
        }

        public PathGeometry Geometry
        {
            get { return geometry; }
            private set { SetProperty(ref geometry, value); }
        }

        public Point StartPoint
        {
            get { return startPoint; }
            private set
            {
                if (SetProperty(ref startPoint, value))
                {
                    OnPropertyChanged(nameof(StartHandleLeft));
                    OnPropertyChanged(nameof(StartHandleTop));
                }
            }
        }

        public Point EndPoint
        {
            get { return endPoint; }
            private set
            {
                if (SetProperty(ref endPoint, value))
                {
                    OnPropertyChanged(nameof(EndHandleLeft));
                    OnPropertyChanged(nameof(EndHandleTop));
                }
            }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (SetProperty(ref isSelected, value))
                {
                    OnPropertyChanged(nameof(Stroke));
                    OnPropertyChanged(nameof(StrokeThickness));
                }
            }
        }

        public Brush Stroke
        {
            get { return IsSelected ? SelectedStroke : NormalStroke; }
        }

        public double StrokeThickness
        {
            get { return IsSelected ? 4.0 : 3.0; }
        }

        public double StartHandleLeft
        {
            get { return StartPoint.X - 8; }
        }

        public double StartHandleTop
        {
            get { return StartPoint.Y - 8; }
        }

        public double EndHandleLeft
        {
            get { return EndPoint.X - 8; }
        }

        public double EndHandleTop
        {
            get { return EndPoint.Y - 8; }
        }

        public void UpdateGeometry(PathGeometry pathGeometry, Point sourcePoint, Point targetPoint)
        {
            Geometry = pathGeometry;
            StartPoint = sourcePoint;
            EndPoint = targetPoint;
        }
    }

    public sealed class FlowDraftConnectionViewModel : ViewModelBase
    {
        private bool isVisible;
        private PathGeometry geometry;

        public bool IsVisible
        {
            get { return isVisible; }
            private set { SetProperty(ref isVisible, value); }
        }

        public PathGeometry Geometry
        {
            get { return geometry; }
            private set { SetProperty(ref geometry, value); }
        }

        public void Show(Point start, Point end)
        {
            IsVisible = true;
            Geometry = BuildPreviewGeometry(start, end);
        }

        public void Hide()
        {
            IsVisible = false;
            Geometry = null;
        }

        private static PathGeometry BuildPreviewGeometry(Point start, Point end)
        {
            var points = new List<Point> { start };
            var middleX = start.X + ((end.X - start.X) / 2.0);
            points.Add(new Point(middleX, start.Y));
            points.Add(new Point(middleX, end.Y));
            points.Add(end);

            var figure = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = false,
                IsFilled = false
            };

            figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
            return new PathGeometry(new[] { figure });
        }
    }
}
