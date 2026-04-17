using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VmFunBase;
using VmFunModule;

namespace VisionMaster
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IFlowExecutionService flowExecutionService;
        private ToolCategoryViewModel selectedCategory;
        private string statusMessage;
        private string lastAction;
        private string flowDurationText;
        private string previewSummary;
        private int flowNodeCount;
        private int flowConnectionCount;
        private int nextHistorySequence;
        private bool isRunning;

        public MainViewModel()
            : this(new FlowExecutionService(Application.Current.Dispatcher))
        {
        }

        public MainViewModel(IFlowExecutionService flowExecutionService)
        {
            this.flowExecutionService = flowExecutionService;

            ToolbarActions = ModuleCatalog.CreateToolbarActions();
            Categories = ModuleCatalog.CreateCategories();
            VisibleTools = new ObservableCollection<ToolItemViewModel>();
            HistoryRecords = new ObservableCollection<HistoryRecordViewModel>();
            FlowEditor = new FlowEditorViewModel();

            SelectCategoryCommand = new RelayCommand<ToolCategoryViewModel>(SelectCategory);
            AddToolToFlowCommand = new RelayCommand<ToolItemViewModel>(AddToolToFlow);
            ExecuteToolbarActionCommand = new RelayCommand<ToolbarActionViewModel>(ExecuteToolbarAction);

            FlowEditor.Nodes.CollectionChanged += FlowEditorCollectionChanged;
            FlowEditor.Connections.CollectionChanged += FlowEditorCollectionChanged;
            this.flowExecutionService.StatusChanged += FlowExecutionServiceOnStatusChanged;
            this.flowExecutionService.HistoryRecorded += FlowExecutionServiceOnHistoryRecorded;

            CurrentUserName = "管理员";
            CurrentPipelineName = "流程1";
            CanvasZoomText = "100%";
            FlowDurationText = "流程 0.00ms";
            StatusMessage = "等待添加模块";
            LastAction = "初始化工作台";

            SelectCategory(Categories.FirstOrDefault());
            SeedDefaultFlow();
            SeedHistory();
            RefreshFlowSummary();
        }

        public event EventHandler CameraWindowRequested;

        public ObservableCollection<ToolbarActionViewModel> ToolbarActions { get; }

        public ObservableCollection<ToolCategoryViewModel> Categories { get; }

        public ObservableCollection<ToolItemViewModel> VisibleTools { get; }

        public ObservableCollection<HistoryRecordViewModel> HistoryRecords { get; }

        public FlowEditorViewModel FlowEditor { get; }

        public ICommand SelectCategoryCommand { get; }

        public ICommand AddToolToFlowCommand { get; }

        public ICommand ExecuteToolbarActionCommand { get; }

        public string CurrentUserName { get; private set; }

        public string CurrentPipelineName { get; private set; }

        public string CanvasZoomText { get; private set; }

        public ToolCategoryViewModel SelectedCategory
        {
            get { return selectedCategory; }
            private set { SetProperty(ref selectedCategory, value); }
        }

        public string StatusMessage
        {
            get { return statusMessage; }
            private set { SetProperty(ref statusMessage, value); }
        }

        public string LastAction
        {
            get { return lastAction; }
            private set { SetProperty(ref lastAction, value); }
        }

        public string FlowDurationText
        {
            get { return flowDurationText; }
            private set { SetProperty(ref flowDurationText, value); }
        }

        public string PreviewSummary
        {
            get { return previewSummary; }
            private set { SetProperty(ref previewSummary, value); }
        }

        public int FlowNodeCount
        {
            get { return flowNodeCount; }
            private set { SetProperty(ref flowNodeCount, value); }
        }

        public int FlowConnectionCount
        {
            get { return flowConnectionCount; }
            private set { SetProperty(ref flowConnectionCount, value); }
        }

        public bool IsRunning
        {
            get { return isRunning; }
            private set { SetProperty(ref isRunning, value); }
        }

        public void NotifyCanvasAction(string action, string message)
        {
            LastAction = action;
            StatusMessage = message;
        }

        public bool DeleteSelectedConnection(out string message)
        {
            var deleted = FlowEditor.DeleteSelectedConnection(out message);

            if (deleted)
            {
                LastAction = "删除连线";
                StatusMessage = message;
            }

            return deleted;
        }

        private void SelectCategory(ToolCategoryViewModel category)
        {
            if (category == null)
            {
                return;
            }

            foreach (var item in Categories)
            {
                item.IsSelected = item == category;
            }

            SelectedCategory = category;
            VisibleTools.Clear();

            foreach (var tool in category.Tools)
            {
                VisibleTools.Add(tool);
            }

            LastAction = string.Format("浏览分类：{0}", category.Name);
            StatusMessage = string.Format("已切换到“{0}”工具箱。", category.Name);
        }

        private void AddToolToFlow(ToolItemViewModel tool)
        {
            if (tool == null)
            {
                return;
            }

            var index = FlowEditor.Nodes.Count;
            var row = index / 3;
            var column = index % 3;

            FlowEditor.AddNode(
                tool.Name,
                tool.Description,
                SelectedCategory != null ? SelectedCategory.Name : "模块",
                tool.Glyph,
                tool.NodeKind,
                ModuleCatalog.CreateAccentBrush(index),
                72 + (column * 230),
                64 + (row * 150));

            LastAction = string.Format("添加模块：{0}", tool.Name);
            StatusMessage = string.Format("已将“{0}”加入流程。", tool.Name);
            AddHistoryRecord(string.Format("{0} 已加入流程并等待配置", tool.Name));
        }

        private async void ExecuteToolbarAction(ToolbarActionViewModel action)
        {
            if (action == null)
            {
                return;
            }

            LastAction = string.Format("工具栏：{0}", action.Name);

            switch (action.Key)
            {
                case ToolbarActionKey.New:
                    StatusMessage = "流程新建模板入口已预留，后续可接入持久化。";
                    break;
                case ToolbarActionKey.Open:
                    StatusMessage = "流程加载能力将在下一阶段接入。";
                    break;
                case ToolbarActionKey.Save:
                    StatusMessage = "流程保存能力将在下一阶段接入。";
                    break;
                case ToolbarActionKey.Camera:
                    StatusMessage = "正在打开海康相机管理窗口。";
                    CameraWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case ToolbarActionKey.Variables:
                    StatusMessage = "变量系统入口已预留。";
                    break;
                case ToolbarActionKey.Run:
                    await flowExecutionService.RunAsync(CurrentPipelineName, FlowEditor.Nodes.ToList(), FlowEditor.Connections.ToList());
                    break;
                case ToolbarActionKey.Stop:
                    flowExecutionService.CancelCurrentRun();
                    break;
                case ToolbarActionKey.Parameters:
                    StatusMessage = "参数面板将结合相机与算子配置继续扩展。";
                    break;
                case ToolbarActionKey.Calibration:
                    StatusMessage = "标定模块入口已保留。";
                    break;
            }
        }

        private void FlowEditorCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshFlowSummary();
        }

        private void FlowExecutionServiceOnStatusChanged(object sender, FlowExecutionStatusChangedEventArgs e)
        {
            IsRunning = e.IsRunning;
            StatusMessage = e.Message;

            if (e.Elapsed.HasValue)
            {
                FlowDurationText = FormatDuration(e.Elapsed.Value);
            }
        }

        private void FlowExecutionServiceOnHistoryRecorded(object sender, FlowExecutionHistoryEventArgs e)
        {
            AddHistoryRecord(e.Module, e.Time);
        }

        private void RefreshFlowSummary()
        {
            FlowNodeCount = FlowEditor.Nodes.Count;
            FlowConnectionCount = FlowEditor.Connections.Count;
            PreviewSummary = string.Format("当前流程：{0} 个模块 / {1} 条连线", FlowNodeCount, FlowConnectionCount);
        }

        private void SeedDefaultFlow()
        {
            var source = FlowEditor.AddNode(
                "图像源",
                "采集海康工业相机图像，作为流程输入。",
                "采集",
                "\uE91B",
                FlowNodeKind.Source,
                ModuleCatalog.CreateAccentBrush(0),
                76,
                76);

            var match = FlowEditor.AddNode(
                "模板匹配",
                "定位工件姿态，为后续测量提供坐标基准。",
                "定位",
                "\uE81E",
                FlowNodeKind.Process,
                ModuleCatalog.CreateAccentBrush(1),
                316,
                76);

            var measure = FlowEditor.AddNode(
                "距离测量",
                "读取关键边缘，输出尺寸与偏差结果。",
                "测量",
                "\uE1D9",
                FlowNodeKind.Process,
                ModuleCatalog.CreateAccentBrush(2),
                556,
                76);

            var output = FlowEditor.AddNode(
                "输出图像",
                "叠加检测结果并输出到右侧预览窗口。",
                "输出",
                "\uE7F4",
                FlowNodeKind.Sink,
                ModuleCatalog.CreateAccentBrush(3),
                796,
                76);

            string message;
            FlowEditor.TryCreateConnection(source.OutputPorts[0], match.InputPorts[0], out message);
            FlowEditor.TryCreateConnection(match.OutputPorts[0], measure.InputPorts[0], out message);
            FlowEditor.TryCreateConnection(measure.OutputPorts[0], output.InputPorts[0], out message);
            FlowEditor.ClearSelection();
        }

        private void SeedHistory()
        {
            foreach (var record in ModuleCatalog.CreateSeedHistory())
            {
                HistoryRecords.Add(record);
                nextHistorySequence = Math.Max(nextHistorySequence, record.Sequence);
            }
        }

        private void AddHistoryRecord(string module, string time = null)
        {
            nextHistorySequence++;
            HistoryRecords.Insert(0, new HistoryRecordViewModel
            {
                Sequence = nextHistorySequence,
                Time = time ?? DateTime.Now.ToString("HH:mm:ss"),
                Module = module
            });
        }

        private static string FormatDuration(TimeSpan elapsed)
        {
            return string.Format("流程 {0:0.00}ms", elapsed.TotalMilliseconds);
        }
    }
}
