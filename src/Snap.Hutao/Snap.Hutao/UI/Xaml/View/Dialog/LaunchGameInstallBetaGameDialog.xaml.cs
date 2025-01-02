// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core.IO;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Factory.Picker;
using Snap.Hutao.Service.Game;
using Snap.Hutao.Service.Game.Package.Advanced;
using Snap.Hutao.Service.Game.Scheme;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Web.Hoyolab.Downloader;
using Snap.Hutao.Web.Response;
using System.IO;

namespace Snap.Hutao.UI.Xaml.View.Dialog;

[ConstructorGenerated(InitializeComponent = true)]
[DependencyProperty("Chinese", typeof(bool))]
[DependencyProperty("English", typeof(bool))]
[DependencyProperty("Japanese", typeof(bool))]
[DependencyProperty("Korean", typeof(bool))]
[DependencyProperty("IsOversea", typeof(bool))]
[DependencyProperty("GameDirectory", typeof(string), default(string), nameof(OnGameDirectoryChanged))]
[DependencyProperty("GetBuildBodyFilePath", typeof(string))]
[DependencyProperty("IsParallelSupported", typeof(bool), true)]
internal sealed partial class LaunchGameInstallBetaGameDialog : ContentDialog
{
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;
    private readonly JsonSerializerOptions jsonSerializerOptions;
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IInfoBarService infoBarService;

    public async ValueTask<ValueResult<bool, (IGameFileSystem GameFileSystem, SophonBuild Build)>> GetGameFileSystemAsync()
    {
        ContentDialogResult result = await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false);
        if (result is not ContentDialogResult.Primary)
        {
            return new(false, default!);
        }

        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();

        if (string.IsNullOrWhiteSpace(GameDirectory))
        {
            infoBarService.Error(SH.ViewDialogLaunchGameInstallGameDirectoryInvalid);
            return new(false, default!);
        }

        if (string.IsNullOrEmpty(GetBuildBodyFilePath))
        {
            infoBarService.Error("GetBuildWithStokenLogin Body File is required.");
            return new(false, default!);
        }

        SophonBuild build;
        using (FileStream stream = File.OpenRead(GetBuildBodyFilePath))
        {
            build = (await JsonSerializer.DeserializeAsync<Response<SophonBuild>>(stream, jsonSerializerOptions).ConfigureAwait(true))!.Data!;
        }

        Directory.CreateDirectory(GameDirectory);
        if (!Directory.Exists(GameDirectory))
        {
            infoBarService.Error(SH.ViewDialogLaunchGameInstallGameDirectoryCreationFailed);
            return new(false, default!);
        }

        if (Directory.EnumerateFileSystemEntries(GameDirectory).Any())
        {
            infoBarService.Error(SH.ViewDialogLaunchGameInstallGameDirectoryExistsFileSystemEntry);
            return new(false, default!);
        }

        if (!Chinese && !English && !Japanese && !Korean)
        {
            infoBarService.Error(SH.ViewDialogLaunchGameInstallGameNoAudioPackageSelected);
            return new(false, default!);
        }

        GameAudioSystem gameAudioSystem = new(Chinese, English, Japanese, Korean);
        string gamePath = Path.Combine(GameDirectory, IsOversea ? GameConstants.GenshinImpactFileName : GameConstants.YuanShenFileName);
        return new(true, (GameFileSystem.CreateForPackageOperation(gamePath, gameAudioSystem), build));
    }

    private static void OnGameDirectoryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((LaunchGameInstallBetaGameDialog)sender).IsParallelSupported = PhysicalDriver.DangerousGetIsSolidState((string)args.NewValue);
    }

    [Command("PickGameDirectoryCommand")]
    private void PickGameDirectory()
    {
        (bool isPickerOk, string gameDirectory) = fileSystemPickerInteraction.PickFolder(SH.ViewDialogLaunchGameInstallGamePickDirectoryTitle);

        if (isPickerOk)
        {
            GameDirectory = gameDirectory;
        }
    }

    [Command("PickGetBuildBodyFilePathCommand")]
    private void PickGetBuildBodyFilePath()
    {
        (bool isPickerOk, ValueFile getBuildBodyFilePath) = fileSystemPickerInteraction.PickFile("GetBuildWithStokenLogin Body File", [("JSON", "*.json")]);

        if (isPickerOk)
        {
            GetBuildBodyFilePath = getBuildBodyFilePath;
        }
    }
}