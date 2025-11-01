using System.Collections.Generic;
using System.Windows;
using Pw.Hub.ViewModels;

namespace Pw.Hub.Windows
{
    public partial class DiffPreviewWindow : Window
    {
        public IList<string> DiffLines { get; }
        public DiffPreviewWindow(IList<string> lines)
        {
            DiffLines = lines ?? new List<string>();
            InitializeComponent();
            DataContext = this;
        }

        public DiffPreviewWindow(LuaEditorAiViewModel aiVm)
        {
            InitializeComponent();
            DataContext = aiVm; // Привязываем напрямую к VM для живого обновления DiffLines
        }
    }
}