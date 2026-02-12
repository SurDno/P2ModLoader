using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Backups;

public static class ModifiedFilesPrompt {
	public static bool? Show(int matchingCount, List<BackupFile>? modified, DateTime? backupDate) {
		var isSteamInstall = SettingsHolder.SelectedInstall?.IsSteamInstall == true;

		var possibleCauses = new List<string> {
			"• Steam integrity verification after mod installation",
			"• Mods installed outside of P2ModLoader (overwriting P2ML changes)"
		};

		possibleCauses.Insert(0, isSteamInstall ? "• Game updates (checking Steam for updates...)" 
												: "• Game updates (not a Steam install, please check manually)");

		var messageBox = new RichTextBox {
			Text = BuildMessage(possibleCauses, modified, matchingCount),
			ReadOnly = true,
			BorderStyle = BorderStyle.None,
			BackColor = SystemColors.Control,
			DetectUrls = false,
			ScrollBars = RichTextBoxScrollBars.None,
			Width = 800
		};

		BoldOptions(messageBox);
		
		messageBox.ContentsResized += (_, e) => messageBox.Height = e.NewRectangle.Height + 4;

		var restoreAllButton = new Button {
			Text = "Restore All",
			Width = 265,
			Height = 40
		};
		restoreAllButton.Click += (_, _) => {
			restoreAllButton.FindForm()!.DialogResult = DialogResult.Yes;
			restoreAllButton.FindForm()!.Close();
		};

		var restoreUnmodifiedButton = new Button {
			Text = modified == null ? "Delete Backups" : "Restore Unmodified",
			Width = 265,
			Height = 40
		};
		restoreUnmodifiedButton.Click += (_, _) => {
			restoreUnmodifiedButton.FindForm()!.DialogResult = DialogResult.No;
			restoreUnmodifiedButton.FindForm()!.Close();
		};

		var cancelButton = new Button {
			Text = "Cancel",
			Width = 265,
			Height = 40
		};
		cancelButton.Click += (_, _) => {
			cancelButton.FindForm()!.DialogResult = DialogResult.Cancel;
			cancelButton.FindForm()!.Close();
		};

		using var form = new Form {
			Text = "WARNING — Modified Files Detected",
			FormBorderStyle = FormBorderStyle.FixedDialog,
			StartPosition = FormStartPosition.CenterParent,
			MaximizeBox = false,
			MinimizeBox = false,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Icon = SystemIcons.Warning
		};

		var layout = new TableLayoutPanel {
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			AutoSize = true,
			Padding = new Padding(0)
		};

		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var buttonPanel = new FlowLayoutPanel {
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Padding = new Padding(0)
		};
		buttonPanel.Controls.AddRange([restoreAllButton, restoreUnmodifiedButton, cancelButton]);

		var warningIcon = new PictureBox {
			Image = SystemIcons.Warning.ToBitmap(),
			SizeMode = PictureBoxSizeMode.AutoSize,
			BackColor = Color.Transparent,
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		form.Controls.Add(warningIcon);

		form.Shown += (_, _) => {
			const int margin = 16;
			warningIcon.Location = new Point(
				form.ClientSize.Width - warningIcon.Width - margin,
				margin
			);
		};
		
		layout.Controls.Add(messageBox, 0, 0);
		layout.Controls.Add(buttonPanel, 0, 1);

		form.Controls.Add(layout);

		if (isSteamInstall) {
			_ = Task.Run(async () => {
				try {
					var lastUpdate = await SteamUpdateChecker.GetLastUpdateDate(SettingsHolder.SelectedInstall!.SteamAppId);
					
					string updateText;
					if (lastUpdate == null) {
						updateText = "(unable to check — no internet connection or Steam API is unavailable)";
						Logger.Log(LogLevel.Warning, $"Could not check for game updates via Steam API!");
					} else if (backupDate != null && lastUpdate.Value > backupDate) {
						updateText = $"(most likely - game had news meaning potential update since last backup)\n" +
						             $"   Backup created: {backupDate:yyyy-MM-dd HH:mm:ss}\n" +
						             $"   Last Steam news: {lastUpdate.Value:yyyy-MM-dd HH:mm:ss}";
						Logger.Log(LogLevel.Info, $"Game was updated after backup: backup={backupDate}, update={lastUpdate.Value}");
					} else if (backupDate != null) {
						updateText = $"(unlikely - no Steam news since last backup)\n" +
						             $"   Backup created: {backupDate:yyyy-MM-dd HH:mm:ss}\n" +
						             $"   Last Steam news: {lastUpdate.Value:yyyy-MM-dd HH:mm:ss}";
						Logger.Log(LogLevel.Info, $"Game was not updated after backup: backup={backupDate}, update={lastUpdate.Value}");
					} else {
						updateText = $"(impossible to determine without backup date)\n" +
						             $"   Last Steam news: {lastUpdate.Value:yyyy-MM-dd HH:mm:ss}";
						Logger.Log(LogLevel.Info, $"No backup info, update={lastUpdate.Value}");
					}

					possibleCauses[0] = $"• Game updates {updateText}";
					form.Invoke(() => { 
						messageBox.Text = BuildMessage(possibleCauses, modified, matchingCount);
						BoldOptions(messageBox);
					});
				} catch (Exception ex) {
					Logger.Log(LogLevel.Error, $"Error during update check: {ex.Message}");
				}
			});
		}

		var result = form.ShowDialog();

		return result switch {
			DialogResult.Yes => true,
			DialogResult.No => false,
			_ => null
		};
	}

	private static void BoldOptions(RichTextBox box) {
		string[] options = ["• Restore All", "• Restore Unmodified", "• Delete Backups", "• Cancel"];

		foreach (var opt in options) {
			var start = box.Text.IndexOf(opt, StringComparison.Ordinal);
			if (start < 0) continue;

			box.Select(start + 2, opt.Length - 2);
			box.SelectionFont = new Font(box.Font, FontStyle.Bold);
		}

		box.Select(0, 0);
	}

	private static string BuildMessage(List<string> causes, List<BackupFile>? modified, int matchingCount) {
		var installPath = SettingsHolder.InstallPath ?? "";
		var modifiedPaths = modified?
			.Take(15)
			.Select(f => {
				var relativePath = f.RelativePath;
				if (relativePath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase)) {
					relativePath = relativePath[installPath.Length..]
						.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				}

				return relativePath;
			});


		string msg;
		if (modified != null)
			msg = $"{modified.Count} file(s) have been modified externally, meaning some mods aren't applied and\n" +
			      $"backups may be outdated.\n\nPossible causes:";
		else
			msg = $"Backup metadata is missing, this is most likely caused by mods being applied on an older\n" +
			      $"version of the tool or failed patch attempt.\n\nMods may or may not have been overriden by:";
		msg += $"\n{string.Join("\n", causes)}\n\n";
		
		if (modified != null && matchingCount > 0) 
			msg += $"{matchingCount} unmodified file(s) will be restored normally. ";

		if (modified != null) {
			msg += $"Modified files requiring attention:\n• {string.Join("\n• ", modifiedPaths!)}";
			if (modified.Count > 15) 
				msg += $"\n• ... and {modified.Count - 15} more";
	 	} else
			msg += $"This can be easily fixed by reapplying the mod list after ensuring no updates happened.";


		msg += "\n\nYour options are:\n" +
		       "• Restore All — replace game files with backups (may overwrite official updates and cause bugs!)\n";
		if (modified != null)
			msg += "• Restore Unmodified — only restore backups for files that haven't been modified (recommended)\n";
		else 
			msg += "• Delete Backups — ignore backups completely (will leave some mod changes in \"unmodded\" files)\n";
		msg += "• Cancel — stop patching / backup restoration to resolve the issue manually.\n\n";
		
		msg += $"If unsure, {(modified != null ? "restore unmodified" : "delete backups")}, verify integrity, " +
		       $"then reapply mods.";

		return msg;
	}
}