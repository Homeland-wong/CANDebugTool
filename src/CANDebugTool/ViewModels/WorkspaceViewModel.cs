using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CANDebugTool.Models;

namespace CANDebugTool.ViewModels
{
    public partial class WorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _workspacePath = "";

        [ObservableProperty]
        private string _workspaceName = "未选择工作区";

        [ObservableProperty]
        private bool _hasWorkspace;

        public event Action<string>? OnWorkspaceChanged;

        public WorkspaceConfig? Config { get; private set; }

        [RelayCommand]
        private void NewWorkspace()
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                WorkspacePath = dialog.FolderName;
                WorkspaceName = System.IO.Path.GetFileName(WorkspacePath);
                Config = new WorkspaceConfig { WorkspacePath = WorkspacePath, Name = WorkspaceName };
                HasWorkspace = true;
                OnWorkspaceChanged?.Invoke(WorkspacePath);
            }
        }

        [RelayCommand]
        private void LoadWorkspace()
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                WorkspacePath = dialog.FolderName;
                WorkspaceName = System.IO.Path.GetFileName(WorkspacePath);
                Config = new WorkspaceConfig { WorkspacePath = WorkspacePath, Name = WorkspaceName };
                HasWorkspace = true;
                OnWorkspaceChanged?.Invoke(WorkspacePath);
            }
        }
    }
}
