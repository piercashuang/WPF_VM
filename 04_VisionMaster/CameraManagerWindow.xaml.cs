using System;
using System.Windows;

namespace VisionMaster
{
    public partial class CameraManagerWindow : Window
    {
        private readonly CameraManagerViewModel viewModel;

        public CameraManagerWindow()
        {
            InitializeComponent();
            viewModel = new CameraManagerViewModel();
            DataContext = viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
