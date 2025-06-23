using P2ModLoader.Data;
using P2ModLoader.Forms;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Saves;

public class SavesTreeViewManager {
    private readonly TreeView _treeView;
    private readonly ProfileManager _profileManager;
    private readonly SavesTreeViewBuilder _treeViewBuilder;
    private readonly ContextMenuStrip _contextMenu;

    public SavesTreeViewManager(TreeView treeView, ProfileManager manager, SavesTreeViewBuilder treeViewBuilder) {
        using var perf = PerformanceLogger.Log();
        _treeView = treeView;
        _profileManager = manager;
        _treeViewBuilder = treeViewBuilder;

        _contextMenu = CreateContextMenu();
        InitializeTreeView();
    }

    private void InitializeTreeView() {
        using var perf = PerformanceLogger.Log();
        _treeView.ShowLines = true;
        _treeView.HideSelection = false;
        _treeView.ContextMenuStrip = _contextMenu;
        _treeView.Font = FontHelper.GetMonospaceFont();

        _treeView.KeyDown += HandleKeyDown;
        _treeView.MouseClick += HandleMouseClick;
        _treeView.MouseDown += HandleMouseDown;
    }

    private ContextMenuStrip CreateContextMenu() {
        using var perf = PerformanceLogger.Log();
        var menu = new ContextMenuStrip();
        var editMenuItem = new ToolStripMenuItem("Edit Profile Name", null, HandleEditProfile);
        var deleteMenuItem = new ToolStripMenuItem("Delete Selected", null, HandleDeleteSelected);
        menu.Items.AddRange([editMenuItem, deleteMenuItem]);
        return menu;
    }

    public void RefreshTreeView() {
        using var perf = PerformanceLogger.Log();
        try {
            _profileManager.LoadProfiles();

            _treeView.BeginUpdate();
            _treeView.Nodes.Clear();

            foreach (var (profile, index) in _profileManager.GetProfiles())
                _treeView.Nodes.Add(_treeViewBuilder.CreateProfileNode(profile, index));

            _treeView.CollapseAll();
            _treeView.EndUpdate();
        } catch (Exception ex) {
            ErrorHandler.Handle("Error refreshing tree view", ex);
        }
    }

    private void HandleMouseClick(object? sender, MouseEventArgs e) {
        using var perf = PerformanceLogger.Log();
        if (e.Button != MouseButtons.Right) return;

        var clickedNode = _treeView.GetNodeAt(e.X, e.Y);
        if (clickedNode == null)  return;

        _treeView.SelectedNode = clickedNode;
        UpdateContextMenuState(clickedNode);
        _contextMenu.Show(_treeView, e.Location);
    }
    
    private void HandleMouseDown(object? sender, MouseEventArgs e) {
        using var perf = PerformanceLogger.Log();
        if (e.Clicks != 2) return; 
        var node = _treeView.GetNodeAt(e.X, e.Y);
        
        var wasExpanded = node.IsExpanded;
        EditProfileNode(node);
        _treeView.BeginInvoke(() => {
            if (wasExpanded) 
                node.Expand();
            else 
                node.Collapse();
        });
    }

    private void UpdateContextMenuState(TreeNode node) {
        using var perf = PerformanceLogger.Log();
        var editMenuItem = (ToolStripMenuItem)_contextMenu.Items[0];
        var deleteMenuItem = (ToolStripMenuItem)_contextMenu.Items[1];
        
        if (node.Tag is not NodeData nodeData) {
            editMenuItem.Enabled = deleteMenuItem.Enabled = false;
            return;
        }

        editMenuItem.Enabled = nodeData.Type == NodeData.NodeType.Profile;
        deleteMenuItem.Enabled = nodeData.Type is NodeData.NodeType.Profile or NodeData.NodeType.Save;
    }

    private void HandleEditProfile(object? sender, EventArgs e) {
        using var perf = PerformanceLogger.Log();
        EditProfileNode(_treeView.SelectedNode);
    }

    private void HandleDeleteSelected(object? sender, EventArgs e) {
        using var perf = PerformanceLogger.Log();
        var selectedNode = _treeView.SelectedNode;
        if (selectedNode?.Tag is not NodeData data) return;

        DeleteNode(selectedNode, data);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e) {
        using var perf = PerformanceLogger.Log();
        if (e.KeyCode == Keys.Delete && _treeView.SelectedNode != null)
            HandleDeleteSelected(sender, e);
    }

    private void EditProfileNode(TreeNode node) {
        using var perf = PerformanceLogger.Log();
        if (_treeView.SelectedNode?.Tag is not NodeData { Type: NodeData.NodeType.Profile } nodeData)
            return;
    
        var parts = nodeData.XElement?.Element("Name")!.Value.Split(" ");
        if (parts?.Length < 2) return; 

        using var editDialog = new ProfileEditDialog(parts[0], parts[1]);
        if (editDialog.ShowDialog() != DialogResult.OK)
            return;

        var newName = $"{parts[0]} {editDialog.UniqueName}"; 

        try {
            var wasCurrent = node.Text.EndsWith("(current)");
            RenameProfile(node, nodeData, newName);  
            node.Text = wasCurrent ? $"{newName} (current)" : newName;
        } catch (Exception ex) {
            ErrorHandler.Handle("Error renaming profile", ex);
        }
    }
    
    private void RenameProfile(TreeNode node, NodeData nodeData, string newName) {
        using var perf = PerformanceLogger.Log();
        var oldPath = Path.Combine(_profileManager.GetSavesDirectory(), node.Text.Replace(" (current)", ""));
        var newPath = Path.Combine(_profileManager.GetSavesDirectory(), newName);

        if (Directory.Exists(newPath) && !oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A profile with this name already exists");

        _profileManager.RenameProfile(nodeData.XElement!, newName);
    }

    private void DeleteNode(TreeNode node, NodeData data) {
        using var perf = PerformanceLogger.Log();
        var message = data.Type switch {
            NodeData.NodeType.Profile => "Are you sure you want to delete this profile?",
            NodeData.NodeType.Save => "Are you sure you want to delete this save?",
        };

        if (MessageBox.Show(message, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try {
            switch (data.Type) {
                case NodeData.NodeType.Profile:
                    _profileManager.DeleteProfile(data.XElement!);
                    break;
                case NodeData.NodeType.Save:
                    if (Directory.Exists(data.Path!))
                        Directory.Delete(data.Path!, true);
                    break;
            }

            node.Remove();
        } catch (Exception ex) {
            ErrorHandler.Handle("Cannot delete", ex);
        }
    }
}