using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using VmCamera;
using VmFunBase;
using VmFunModule;

namespace VisionMaster
{
    public static class NodeEditorFactory
    {
        public static NodeEditorSession Create(FlowNodeViewModel node, Window owner)
        {
            if (node == null)
            {
                return null;
            }

            if (node.DefinitionKey == "图像源")
            {
                var model = ImageSourceNodeEditorModel.FromNode(node);
                var fields = BuildFields(model, owner);

                return new NodeEditorSession(
                    "图像源配置",
                    "通过工厂模式返回编辑器会话，内部字段通过反射元数据自动生成。",
                    fields,
                    () =>
                    {
                        node.Configuration = model.ToConfiguration();
                        node.SecondaryText = model.SourceType == ImageSourceOrigin.Camera
                            ? GetCameraDisplay(fields, model.CameraKey)
                            : GetLocalFileDisplay(model.LocalImagePath);
                    });
            }

            return null;
        }

        private static ObservableCollection<NodeEditorFieldViewModel> BuildFields(object target, Window owner)
        {
            var fields = target.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => new
                {
                    Property = property,
                    Attribute = property.GetCustomAttribute<NodeEditorFieldAttribute>()
                })
                .Where(item => item.Attribute != null)
                .OrderBy(item => item.Attribute.Order)
                .Select(item => new NodeEditorFieldViewModel(target, item.Property, item.Attribute, owner))
                .ToList();

            return new ObservableCollection<NodeEditorFieldViewModel>(fields);
        }

        private static string GetCameraDisplay(IEnumerable<NodeEditorFieldViewModel> fields, string cameraKey)
        {
            var field = fields.FirstOrDefault(item => item.PropertyName == nameof(ImageSourceNodeEditorModel.CameraKey));
            var option = field != null ? field.Options.FirstOrDefault(item => Equals(item.Value, cameraKey)) : null;
            return option != null ? option.DisplayName : "相机";
        }

        private static string GetLocalFileDisplay(string localImagePath)
        {
            if (string.IsNullOrWhiteSpace(localImagePath))
            {
                return "本地图片";
            }

            return Path.GetFileName(localImagePath);
        }
    }

    public sealed class NodeEditorSession
    {
        private readonly Action applyAction;

        public NodeEditorSession(string title, string description, ObservableCollection<NodeEditorFieldViewModel> fields, Action applyAction)
        {
            Title = title;
            Description = description;
            Fields = fields;
            this.applyAction = applyAction;
        }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public ObservableCollection<NodeEditorFieldViewModel> Fields { get; private set; }

        public void Apply()
        {
            applyAction();
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NodeEditorFieldAttribute : Attribute
    {
        public NodeEditorFieldAttribute(string label, NodeEditorFieldKind kind, int order)
        {
            Label = label;
            Kind = kind;
            Order = order;
        }

        public string Label { get; private set; }

        public NodeEditorFieldKind Kind { get; private set; }

        public int Order { get; private set; }

        public Type OptionsProviderType { get; set; }

        public string VisibleWhenPropertyName { get; set; }

        public string VisibleWhenEquals { get; set; }

        public string FileFilter { get; set; }
    }

    public enum NodeEditorFieldKind
    {
        Text,
        ComboBox,
        FilePath
    }

    public interface INodeEditorOptionsProvider
    {
        IEnumerable<NodeEditorOptionItem> GetOptions();
    }

    public sealed class NodeEditorOptionItem
    {
        public NodeEditorOptionItem(object value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public object Value { get; private set; }

        public string DisplayName { get; private set; }
    }

    public sealed class NodeEditorFieldViewModel : ViewModelBase
    {
        private readonly object target;
        private readonly PropertyInfo propertyInfo;
        private readonly NodeEditorFieldAttribute attribute;
        private readonly Window owner;

        public NodeEditorFieldViewModel(object target, PropertyInfo propertyInfo, NodeEditorFieldAttribute attribute, Window owner)
        {
            this.target = target;
            this.propertyInfo = propertyInfo;
            this.attribute = attribute;
            this.owner = owner;

            Options = new ObservableCollection<NodeEditorOptionItem>(BuildOptions());
            BrowseCommand = new RelayCommand(BrowseFile, () => Kind == NodeEditorFieldKind.FilePath);

            var notifySource = target as INotifyPropertyChanged;
            if (notifySource != null)
            {
                notifySource.PropertyChanged += (_, __) => OnPropertyChanged(nameof(IsVisible));
            }
        }

        public string PropertyName
        {
            get { return propertyInfo.Name; }
        }

        public string Label
        {
            get { return attribute.Label; }
        }

        public NodeEditorFieldKind Kind
        {
            get { return attribute.Kind; }
        }

        public ObservableCollection<NodeEditorOptionItem> Options { get; }

        public ICommand BrowseCommand { get; }

        public object Value
        {
            get { return propertyInfo.GetValue(target); }
            set
            {
                propertyInfo.SetValue(target, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextValue));
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public string TextValue
        {
            get { return Convert.ToString(propertyInfo.GetValue(target)) ?? string.Empty; }
            set
            {
                propertyInfo.SetValue(target, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public bool IsComboBox
        {
            get { return Kind == NodeEditorFieldKind.ComboBox; }
        }

        public bool IsText
        {
            get { return Kind == NodeEditorFieldKind.Text; }
        }

        public bool IsFilePath
        {
            get { return Kind == NodeEditorFieldKind.FilePath; }
        }

        public bool IsVisible
        {
            get
            {
                if (string.IsNullOrWhiteSpace(attribute.VisibleWhenPropertyName))
                {
                    return true;
                }

                var dependencyProperty = target.GetType().GetProperty(attribute.VisibleWhenPropertyName);
                if (dependencyProperty == null)
                {
                    return true;
                }

                var currentValue = dependencyProperty.GetValue(target);
                var compareValue = currentValue != null ? currentValue.ToString() : string.Empty;
                return string.Equals(compareValue, attribute.VisibleWhenEquals, StringComparison.OrdinalIgnoreCase);
            }
        }

        private IEnumerable<NodeEditorOptionItem> BuildOptions()
        {
            if (Kind != NodeEditorFieldKind.ComboBox)
            {
                return Array.Empty<NodeEditorOptionItem>();
            }

            if (attribute.OptionsProviderType != null)
            {
                var provider = Activator.CreateInstance(attribute.OptionsProviderType) as INodeEditorOptionsProvider;
                return provider != null ? provider.GetOptions() : Array.Empty<NodeEditorOptionItem>();
            }

            if (propertyInfo.PropertyType.IsEnum)
            {
                return Enum.GetValues(propertyInfo.PropertyType)
                    .Cast<object>()
                    .Select(value => new NodeEditorOptionItem(value, GetEnumDisplay(value)))
                    .ToList();
            }

            return Array.Empty<NodeEditorOptionItem>();
        }

        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = string.IsNullOrWhiteSpace(attribute.FileFilter)
                    ? "所有文件|*.*"
                    : attribute.FileFilter
            };

            if (dialog.ShowDialog(owner) == true)
            {
                TextValue = dialog.FileName;
            }
        }

        private static string GetEnumDisplay(object value)
        {
            if (value is ImageSourceOrigin origin)
            {
                switch (origin)
                {
                    case ImageSourceOrigin.Camera:
                        return "相机";
                    case ImageSourceOrigin.LocalImage:
                        return "本地图片";
                }
            }

            return value.ToString();
        }
    }

    public enum ImageSourceOrigin
    {
        Camera,
        LocalImage
    }

    public sealed class ImageSourceNodeConfiguration
    {
        public ImageSourceOrigin SourceType { get; set; }

        public string CameraKey { get; set; }

        public string LocalImagePath { get; set; }
    }

    public sealed class ImageSourceNodeEditorModel : ViewModelBase
    {
        private ImageSourceOrigin sourceType;
        private string cameraKey;
        private string localImagePath;

        [NodeEditorField("图像来源", NodeEditorFieldKind.ComboBox, 0)]
        public ImageSourceOrigin SourceType
        {
            get { return sourceType; }
            set { SetProperty(ref sourceType, value); }
        }

        [NodeEditorField("相机设备", NodeEditorFieldKind.ComboBox, 1, OptionsProviderType = typeof(CameraDeviceOptionsProvider), VisibleWhenPropertyName = nameof(SourceType), VisibleWhenEquals = "Camera")]
        public string CameraKey
        {
            get { return cameraKey; }
            set { SetProperty(ref cameraKey, value); }
        }

        [NodeEditorField("本地图片", NodeEditorFieldKind.FilePath, 2, VisibleWhenPropertyName = nameof(SourceType), VisibleWhenEquals = "LocalImage", FileFilter = "图片文件|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff")]
        public string LocalImagePath
        {
            get { return localImagePath; }
            set { SetProperty(ref localImagePath, value); }
        }

        public static ImageSourceNodeEditorModel FromNode(FlowNodeViewModel node)
        {
            var configuration = node.Configuration as ImageSourceNodeConfiguration;
            return new ImageSourceNodeEditorModel
            {
                SourceType = configuration != null ? configuration.SourceType : ImageSourceOrigin.Camera,
                CameraKey = configuration != null ? configuration.CameraKey : string.Empty,
                LocalImagePath = configuration != null ? configuration.LocalImagePath : string.Empty
            };
        }

        public ImageSourceNodeConfiguration ToConfiguration()
        {
            return new ImageSourceNodeConfiguration
            {
                SourceType = SourceType,
                CameraKey = CameraKey,
                LocalImagePath = LocalImagePath
            };
        }
    }

    public sealed class CameraDeviceOptionsProvider : INodeEditorOptionsProvider
    {
        public IEnumerable<NodeEditorOptionItem> GetOptions()
        {
            try
            {
                using (var service = new HikCameraService())
                {
                    var devices = service.ScanDevices()
                        .Select(device => new NodeEditorOptionItem(device.UniqueKey, device.DisplayName))
                        .ToList();

                    if (devices.Count == 0)
                    {
                        devices.Add(new NodeEditorOptionItem(string.Empty, "未扫描到相机"));
                    }

                    return devices;
                }
            }
            catch
            {
                return new[]
                {
                    new NodeEditorOptionItem(string.Empty, "未扫描到相机")
                };
            }
        }
    }
}
