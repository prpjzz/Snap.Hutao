// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Service.Game;
using Snap.Hutao.Service.Game.Package.Advanced;
using Snap.Hutao.Service.Game.Scheme;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.UI.Xaml.View.Dialog;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using Snap.Hutao.Web.Response;

namespace Snap.Hutao.ViewModel.Game;

[ConstructorGenerated]
[Injection(InjectAs.Singleton)]
internal sealed partial class GamePackageInstallViewModel : Abstraction.ViewModel
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IGamePackageService gamePackageService;
    private readonly IServiceProvider serviceProvider;
    private readonly IInfoBarService infoBarService;
    private readonly ITaskContext taskContext;

    public Version? RemoteVersion { get; set => SetProperty(ref field, value, nameof(RemoteVersionText)); }

    public string RemoteVersionText { get => SH.FormatViewModelGamePackageRemoteVersion(RemoteVersion); }

    protected override async ValueTask<bool> LoadOverrideAsync()
    {
        // TODO: Why we are using this instead of Selected one?
        LaunchScheme launchScheme = KnownLaunchSchemes.Values.First(scheme => scheme.IsNotCompatOnly);

        using (IServiceScope scope = serviceProvider.CreateScope(true))
        {
            HoyoPlayClient hoyoPlayClient = scope.ServiceProvider.GetRequiredService<HoyoPlayClient>();

            Response<GameBranchesWrapper> branchResp = await hoyoPlayClient.GetBranchesAsync(launchScheme).ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(branchResp, serviceProvider, out GameBranchesWrapper? branchesWrapper))
            {
                return false;
            }

            if (branchesWrapper.GameBranches.FirstOrDefault(b => b.Game.Id == launchScheme.GameId) is { } branch)
            {
                await taskContext.SwitchToMainThreadAsync();
                RemoteVersion = new(branch.Main.Tag);
                return true;
            }
        }

        return false;
    }

    [Command("StartCommand")]
    private async Task StartAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Start install operation", "GamePackageInstallViewModel.Command"));

        if (!IsInitialized)
        {
            return;
        }

        GameInstallOptions gameInstallOptions;
        using (IServiceScope scope = serviceProvider.CreateScope(true))
        {
            LaunchGameInstallGameDialog dialog = await contentDialogFactory.CreateInstanceAsync<LaunchGameInstallGameDialog>(scope.ServiceProvider).ConfigureAwait(false);
            dialog.KnownSchemes = KnownLaunchSchemes.Values;
            dialog.SelectedScheme = dialog.KnownSchemes.First(scheme => scheme.IsNotCompatOnly);
            (bool isOk, gameInstallOptions) = await dialog.GetGameInstallOptionsAsync().ConfigureAwait(false);

            if (!isOk)
            {
                return;
            }
        }

        (IGameFileSystem gameFileSystem, LaunchScheme launchScheme) = gameInstallOptions;

        GameBranchesWrapper? branchesWrapper;
        GameChannelSDKsWrapper? channelSDKsWrapper;
        using (IServiceScope scope = serviceProvider.CreateScope(true))
        {
            HoyoPlayClient hoyoPlayClient = scope.ServiceProvider.GetRequiredService<HoyoPlayClient>();

            Response<GameBranchesWrapper> branchResp = await hoyoPlayClient.GetBranchesAsync(launchScheme).ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(branchResp, serviceProvider, out branchesWrapper))
            {
                return;
            }

            Response<GameChannelSDKsWrapper> sdkResp = await hoyoPlayClient.GetChannelSDKAsync(launchScheme).ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(sdkResp, serviceProvider, out channelSDKsWrapper))
            {
                return;
            }
        }

        GameBranch? branch = branchesWrapper.GameBranches.FirstOrDefault(b => b.Game.Id == launchScheme.GameId);
        GameChannelSDK? gameChannelSDK = channelSDKsWrapper.GameChannelSDKs.FirstOrDefault(sdk => sdk.Game.Id == launchScheme.GameId);

        ArgumentNullException.ThrowIfNull(branch);

        ContentDialog fetchManifestDialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.UIXamlViewSpecializedSophonProgressDefault)
            .ConfigureAwait(false);

        SophonDecodedBuild? build;
        using (await contentDialogFactory.BlockAsync(fetchManifestDialog).ConfigureAwait(false))
        {
            build = await gamePackageService.DecodeManifestsAsync(gameFileSystem, branch.Main).ConfigureAwait(false);
            if (build is null)
            {
                infoBarService.Error(SH.ServiceGamePackageAdvancedDecodeManifestFailed);
                return;
            }
        }

        GamePackageOperationContext context = new(
            serviceProvider,
            GamePackageOperationKind.Install,
            gameFileSystem,
            default!,
            build,
            gameChannelSDK,
            default);

        if (!GameInstallPrerequisite.TryLock(gameFileSystem, branch.Main.Tag, launchScheme, out GameInstallPrerequisite? installToken))
        {
            infoBarService.Error(SH.ViewDialogLaunchGameInstallGameDirectoryExistsFileSystemEntry);
            return;
        }

        if (!await gamePackageService.ExecuteOperationAsync(context).ConfigureAwait(false))
        {
            // Operation canceled or failed
            return;
        }

        installToken.Release();
    }
}