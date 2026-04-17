using System.Windows;

namespace VisionMaster
{
    public partial class NodeEditorWindow : Window
    {
        public NodeEditorWindow()
        {
            InitializeComponent();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
