using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using VmFunBase;

namespace VmFunModule
{
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

    public static class ModuleCatalog
    {
        public static ObservableCollection<ToolbarActionViewModel> CreateToolbarActions()
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

        public static ObservableCollection<ToolCategoryViewModel> CreateCategories()
        {
            return new ObservableCollection<ToolCategoryViewModel>(
                new[]
                {
                    new ToolCategoryViewModel("采集", new[]
                    {
                        new ToolItemViewModel("图像源", "\uE91B", "接入工业相机或本地图像，作为流程输入。", FlowNodeKind.Source),
                        new ToolItemViewModel("多图采集", "\uE8B9", "组合多路相机输入，构建同步采集流程。", FlowNodeKind.Source),
                        new ToolItemViewModel("输出图像", "\uE7F4", "将中间结果作为最终图像输出。", FlowNodeKind.Sink),
                        new ToolItemViewModel("缓存图像", "\uE7C3", "缓存原图或中间结果用于追溯。", FlowNodeKind.Process),
                        new ToolItemViewModel("光源", "\uE793", "预留光源控制与曝光联动接口。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("定位", new[]
                    {
                        new ToolItemViewModel("模板匹配", "\uE81E", "通过模板查找工件位置与角度。", FlowNodeKind.Process),
                        new ToolItemViewModel("圆查找", "\uEA3A", "在目标区域内定位圆形特征。", FlowNodeKind.Process),
                        new ToolItemViewModel("边缘定位", "\uE7AD", "按边缘梯度定位轮廓基准。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("测量", new[]
                    {
                        new ToolItemViewModel("距离测量", "\uE1D9", "计算点线间距或边缘距离。", FlowNodeKind.Process),
                        new ToolItemViewModel("角度测量", "\uE1D4", "输出目标角度及偏差。", FlowNodeKind.Process),
                        new ToolItemViewModel("卡尺工具", "\uE121", "沿 ROI 多点扫描完成高精度测量。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("识别", new[]
                    {
                        new ToolItemViewModel("OCR", "\uE8D2", "识别字符、喷码与标签文本。", FlowNodeKind.Process),
                        new ToolItemViewModel("条码", "\uEA90", "读取一维码或二维码。", FlowNodeKind.Process),
                        new ToolItemViewModel("分类", "\uE8D4", "接入分类模型输出类别结果。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("缺陷检测", new[]
                    {
                        new ToolItemViewModel("划痕检测", "\uE7BA", "检测表面细长划痕和破损。", FlowNodeKind.Process),
                        new ToolItemViewModel("脏污检测", "\uE7B8", "识别油污、异物和附着缺陷。", FlowNodeKind.Process),
                        new ToolItemViewModel("Blob 分析", "\uE9CE", "按面积和形状过滤缺陷区域。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("标定", new[]
                    {
                        new ToolItemViewModel("相机标定", "\uE163", "完成像素到物理尺寸标定。", FlowNodeKind.Process),
                        new ToolItemViewModel("畸变矫正", "\uE178", "对镜头畸变做几何校正。", FlowNodeKind.Process),
                        new ToolItemViewModel("像素标尺", "\uE1D9", "建立像素尺寸与长度单位映射。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("运算", new[]
                    {
                        new ToolItemViewModel("四则运算", "\uE8EF", "对数值结果进行加减乘除。", FlowNodeKind.Process),
                        new ToolItemViewModel("逻辑判断", "\uE8D7", "表达式判断和条件输出。", FlowNodeKind.Process),
                        new ToolItemViewModel("统计分析", "\uE9D2", "聚合流程结果并计算统计量。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("图像处理", new[]
                    {
                        new ToolItemViewModel("滤波", "\uE790", "平滑降噪并保留主要轮廓。", FlowNodeKind.Process),
                        new ToolItemViewModel("阈值", "\uE7B3", "按灰度范围分离目标。", FlowNodeKind.Process),
                        new ToolItemViewModel("锐化", "\uE8B0", "增强边缘和局部纹理。", FlowNodeKind.Process)
                    }),
                    new ToolCategoryViewModel("通信", new[]
                    {
                        new ToolItemViewModel("TCP 发送", "\uE968", "向上位机发送检测结果。", FlowNodeKind.Process),
                        new ToolItemViewModel("串口", "\uE7F6", "与控制器进行串口通信。", FlowNodeKind.Process),
                        new ToolItemViewModel("PLC", "\uE7F1", "接入 PLC 数据交换和触发。", FlowNodeKind.Process)
                    })
                });
        }

        public static Brush CreateAccentBrush(int index)
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

        public static IList<HistoryRecordViewModel> CreateSeedHistory()
        {
            return new List<HistoryRecordViewModel>
            {
                new HistoryRecordViewModel { Sequence = 1, Time = "10:25:12", Module = "图像源 初始化完成" },
                new HistoryRecordViewModel { Sequence = 2, Time = "10:25:12", Module = "模板匹配 定位成功，角度偏差 0.03°" },
                new HistoryRecordViewModel { Sequence = 3, Time = "10:25:12", Module = "距离测量 输出 12.486 mm" },
                new HistoryRecordViewModel { Sequence = 4, Time = "10:25:12", Module = "输出图像 已刷新显示" }
            };
        }
    }
}
