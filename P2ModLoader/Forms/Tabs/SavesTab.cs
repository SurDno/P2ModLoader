using P2ModLoader.Abstract;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.Saves;

namespace P2ModLoader.Forms.Tabs;

public class SavesTab : BaseTab {
	private TreeView? _savesTreeView;
	private Label? _messageLabel;
	private ProfileManager? _profileManager;
	private SavesTreeViewManager? _treeViewManager;
	private readonly string? _savesDirectory;
	private readonly string? _profilesPath;

	public SavesTab(TabPage page) : base(page) {
		using var perf = PerformanceLogger.Log();
		var appData = InstallationLocator.FindAppData();
		if (appData != null) {
			_savesDirectory = Path.Combine(appData, "Saves");
			_profilesPath = Path.Combine(_savesDirectory, "Profiles.xml");

			try {
				_profileManager = new ProfileManager(_savesDirectory, _profilesPath);
				InitializeComponents();
			} catch (Exception ex) {
				ErrorHandler.Handle("Saves initialization exception", ex);
			}
		} else {
			ShowErrorMessage();
		}
	}

	protected sealed override void InitializeComponents() {
		using var perf = PerformanceLogger.Log();
		Logger.Log(LogLevel.Info, $"Appdata found, initializing saves tree view.");
		_savesTreeView = new TreeView { Dock = DockStyle.Fill };
		_profileManager = new ProfileManager(_savesDirectory!, _profilesPath!);
		var treeViewBuilder = new SavesTreeViewBuilder(_savesDirectory!, _profileManager);
		_treeViewManager = new SavesTreeViewManager(_savesTreeView, _profileManager, treeViewBuilder);

		Tab.Controls.Add(_savesTreeView);
		_treeViewManager.RefreshTreeView();
		Logger.Log(LogLevel.Info, $"Saves tree view initialized.");
	}

	private void ShowErrorMessage() {
		using var perf = PerformanceLogger.Log();
		Logger.Log(LogLevel.Info, $"Appdata not found, cannot view save files.");
		_messageLabel = new Label {
			Text = "AppData for Pathologic 2 not found. " +
			       "Ensure you launch the game at least once before trying to view saves in Pathologic 2 Mod Loader.",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			AutoSize = false
		};
		Tab.Controls.Add(_messageLabel);
	}
}