using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WPF_VM
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

            ToolbarActions = new ObservableCollection<ToolbarActionViewModel>(CreateToolbarActions());
            Categories = new ObservableCollection<ToolCategoryViewModel>(CreateCategories());
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

        public ObservableCollection<ToolbarActionViewModel> ToolbarActions { get; private set; }

        public ObservableCollection<ToolCategoryViewModel> Categories { get; private set; }

        public ObservableCollection<ToolItemViewModel> VisibleTools { get; private set; }

        public ObservableCollection<HistoryRecordViewModel> HistoryRecords { get; private set; }

        public FlowEditorViewModel FlowEditor { get; private set; }

        public ICommand SelectCategoryCommand { get; private set; }

        public ICommand AddToolToFlowCommand { get; private set; }

        public ICommand ExecuteToolbarActionCommand { get; private set; }

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
                CreateAccentBrush(index),
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
                    StatusMessage = "当前版本暂未实现流程新建模板，后续可接入。";
                    break;
                case ToolbarActionKey.Open:
                    StatusMessage = "当前版本暂未实现流程加载，后续可接入持久化。";
                    break;
                case ToolbarActionKey.Save:
                    StatusMessage = "当前版本暂未实现流程保存，README 中已预留设计说明。";
                    break;
                case ToolbarActionKey.Camera:
                    StatusMessage = "相机参数面板待接入海康 SDK。";
                    break;
                case ToolbarActionKey.Variables:
                    StatusMessage = "变量系统尚未接入，本轮先完成流程图和执行服务。";
                    break;
                case ToolbarActionKey.Run:
                    await flowExecutionService.RunAsync(CurrentPipelineName, FlowEditor.Nodes.ToList(), FlowEditor.Connections.ToList());
                    break;
                case ToolbarActionKey.Stop:
                    flowExecutionService.CancelCurrentRun();
                    break;
                case ToolbarActionKey.Parameters:
                    StatusMessage = "参数面板是下一阶段重点扩展点。";
                    break;
                case ToolbarActionKey.Calibration:
                    StatusMessage = "标定模块入口已保留，后续可以继续扩充。";
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
                CreateAccentBrush(0),
                76,
                76);

            var match = FlowEditor.AddNode(
                "模板匹配",
                "定位工件姿态，为后续测量提供坐标基准。",
                "定位",
                "\uE81E",
                FlowNodeKind.Process,
                CreateAccentBrush(1),
                316,
                76);

            var measure = FlowEditor.AddNode(
                "距离测量",
                "读取关键边缘，输出尺寸与偏差结果。",
                "测量",
                "\uE1D9",
                FlowNodeKind.Process,
                CreateAccentBrush(2),
                556,
                76);

            var output = FlowEditor.AddNode(
                "输出图像",
                "叠加检测结果并输出到右侧预览窗口。",
                "输出",
                "\uE7F4",
                FlowNodeKind.Sink,
                CreateAccentBrush(3),
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
            AddHistoryRecord("图像源 初始化完成", "10:25:12");
            AddHistoryRecord("模板匹配 定位成功，角度偏差 0.03°", "10:25:12");
            AddHistoryRecord("距离测量 输出 12.486 mm", "10:25:12");
            AddHistoryRecord("输出图像 已刷新显示", "10:25:12");
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

        private static ObservableCollection<ToolbarActionViewModel> CreateToolbarActions()
        {
            return new ObservableCollection<ToolbarActionViewModel>
            {
                new ToolbarActionViewModel(ToolbarActionKey.New, "新建", "\uE710"),
                new ToolbarActionViewModel(ToolbarActionKey.Open, "打开", "\uE8B7"),
                new ToolbarActionViewModel(ToolbarActionKey.Save, "保存", "\uE74E"),
                new ToolbarActionViewModel(ToolbarActionKey.Camera, "相机", "\uE722"),
                new ToolbarActionViewModel(ToolbarActionKey.Variables, "变量", "\uE943"),
                new ToolbarActionViewModel(ToolbarActionKey.Run, "运行", "\uE768"),
                new ToolbarActionViewModel(ToolbarActionKey.Stop, "停止", "\uE71A"),
                new ToolbarActionViewModel(ToolbarActionKey.Parameters, "参数", "\uE115"),
                new ToolbarActionViewModel(ToolbarActionKey.Calibration, "标定", "\uE163")
            };
        }

        private static IEnumerable<ToolCategoryViewModel> CreateCategories()
        {
            return new[]
            {
                new ToolCategoryViewModel("采集", new []
                {
                    new ToolItemViewModel("图像源", "\uE91B", "接入工业相机或本地图像，作为流程输入。", FlowNodeKind.Source),
                    new ToolItemViewModel("多图采集", "\uE8B9", "组合多路相机输入，构建同步采集流程。", FlowNodeKind.Source),
                    new ToolItemViewModel("输出图像", "\uE7F4", "将中间结果作为流程最终图像输出。", FlowNodeKind.Sink),
                    new ToolItemViewModel("缓存图像", "\uE7C3", "缓存原图或中间结果用于追溯。", FlowNodeKind.Process),
                    new ToolItemViewModel("光源", "\uE793", "预留光源控制与曝光联动接口。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("定位", new []
                {
                    new ToolItemViewModel("模板匹配", "\uE81E", "通过模板查找工件位置与角度。", FlowNodeKind.Process),
                    new ToolItemViewModel("圆查找", "\uEA3A", "在目标区域内定位圆形特征。", FlowNodeKind.Process),
                    new ToolItemViewModel("边缘定位", "\uE7AD", "按边缘梯度定位轮廓基准。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("测量", new []
                {
                    new ToolItemViewModel("距离测量", "\uE1D9", "计算点线间距或边缘距离。", FlowNodeKind.Process),
                    new ToolItemViewModel("角度测量", "\uE1D4", "输出目标角度及偏差。", FlowNodeKind.Process),
                    new ToolItemViewModel("卡尺工具", "\uE121", "沿 ROI 多点扫描完成高精度测量。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("识别", new []
                {
                    new ToolItemViewModel("OCR", "\uE8D2", "识别字符、喷码与标签文本。", FlowNodeKind.Process),
                    new ToolItemViewModel("条码", "\uEA90", "读取一维码或二维码。", FlowNodeKind.Process),
                    new ToolItemViewModel("分类", "\uE8D4", "接入分类模型输出类别结果。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("缺陷检测", new []
                {
                    new ToolItemViewModel("划痕检测", "\uE7BA", "检测表面细长划痕和破损。", FlowNodeKind.Process),
                    new ToolItemViewModel("脏污检测", "\uE7B8", "识别油污、异物和沾附缺陷。", FlowNodeKind.Process),
                    new ToolItemViewModel("Blob 分析", "\uE9CE", "按面积和形状过滤缺陷区域。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("标定", new []
                {
                    new ToolItemViewModel("相机标定", "\uE163", "完成像素到物理尺寸标定。", FlowNodeKind.Process),
                    new ToolItemViewModel("畸变矫正", "\uE178", "对镜头畸变做几何校正。", FlowNodeKind.Process),
                    new ToolItemViewModel("像素标尺", "\uE1D9", "建立像素尺寸与长度单位映射。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("运算", new []
                {
                    new ToolItemViewModel("四则运算", "\uE8EF", "对数值结果进行加减乘除。", FlowNodeKind.Process),
                    new ToolItemViewModel("逻辑判断", "\uE8D7", "表达式判断和条件输出。", FlowNodeKind.Process),
                    new ToolItemViewModel("统计分析", "\uE9D2", "聚合流程结果并计算统计量。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("图像处理", new []
                {
                    new ToolItemViewModel("滤波", "\uE790", "平滑降噪并保留主要轮廓。", FlowNodeKind.Process),
                    new ToolItemViewModel("阈值", "\uE7B3", "按灰度范围分离目标。", FlowNodeKind.Process),
                    new ToolItemViewModel("锐化", "\uE8B0", "增强边缘和局部纹理。", FlowNodeKind.Process)
                }),
                new ToolCategoryViewModel("通信", new []
                {
                    new ToolItemViewModel("TCP 发送", "\uE968", "向上位机发送检测结果。", FlowNodeKind.Process),
                    new ToolItemViewModel("串口", "\uE7F6", "与控制器进行串口通信。", FlowNodeKind.Process),
                    new ToolItemViewModel("PLC", "\uE7F1", "接入 PLC 数据交换和触发。", FlowNodeKind.Process)
                })
            };
        }

        private static Brush CreateAccentBrush(int index)
        {
            var colors = new[]
            {
                Color.FromRgb(255, 138, 36),
                Color.FromRgb(90, 172, 255),
                Color.FromRgb(79, 201, 117),
                Color.FromRgb(230, 180, 80),
                Color.FromRgb(245, 94, 94)
            };

            return new SolidColorBrush(colors[index % colors.Length]);
        }
    }

    public enum ToolbarActionKey
    {
        New,
        Open,
        Save,
        Camera,
        Variables,
        Run,
        Stop,
        Parameters,
        Calibration
    }

    public sealed class ToolbarActionViewModel
    {
        public ToolbarActionViewModel(ToolbarActionKey key, string name, string glyph)
        {
            Key = key;
            Name = name;
            Glyph = glyph;
        }

        public ToolbarActionKey Key { get; private set; }

        public string Name { get; private set; }

        public string Glyph { get; private set; }
    }

    public sealed class ToolCategoryViewModel : ViewModelBase
    {
        private bool isSelected;

        public ToolCategoryViewModel(string name, IEnumerable<ToolItemViewModel> tools)
        {
            Name = name;
            Tools = new ObservableCollection<ToolItemViewModel>(tools);
        }

        public string Name { get; private set; }

        public ObservableCollection<ToolItemViewModel> Tools { get; private set; }

        public bool IsSelected
        {
            get { return isSelected; }
            set { SetProperty(ref isSelected, value); }
        }
    }

    public sealed class ToolItemViewModel
    {
        public ToolItemViewModel(string name, string glyph, string description, FlowNodeKind nodeKind)
        {
            Name = name;
            Glyph = glyph;
            Description = description;
            NodeKind = nodeKind;
        }

        public string Name { get; private set; }

        public string Glyph { get; private set; }

        public string Description { get; private set; }

        public FlowNodeKind NodeKind { get; private set; }
    }

    public sealed class HistoryRecordViewModel
    {
        public int Sequence { get; set; }

        public string Time { get; set; }

        public string Module { get; set; }
    }
}
