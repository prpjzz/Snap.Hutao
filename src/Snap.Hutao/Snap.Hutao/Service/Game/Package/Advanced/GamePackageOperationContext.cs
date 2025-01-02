// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.DependencyInjection.Abstraction;
using Snap.Hutao.Web.Hoyolab.Downloader;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using System.IO;

namespace Snap.Hutao.Service.Game.Package.Advanced;

internal readonly struct GamePackageOperationContext
{
    public readonly GamePackageOperationKind Kind;
    public readonly IGameAssetOperation Asset;
    public readonly IGameFileSystem GameFileSystem;
    public readonly BranchWrapper LocalBranch;
    public readonly BranchWrapper RemoteBranch;
    public readonly GameChannelSDK? GameChannelSDK;
    public readonly string ExtractOrGameDirectory;
    public readonly string ProxiedChunksDirectory;

    public readonly SophonBuild BetaBuild;

    public GamePackageOperationContext(
        IServiceProvider serviceProvider,
        GamePackageOperationKind kind,
        IGameFileSystem gameFileSystem,
        BranchWrapper localBranch,
        BranchWrapper remoteBranch,
        GameChannelSDK? gameChannelSDK,
        string? extractDirectory)
    {
        Kind = kind;
        Asset = serviceProvider.GetRequiredService<IDriverMediaTypeAwareFactory<IGameAssetOperation>>().Create(gameFileSystem.GetGameDirectory());
        GameFileSystem = gameFileSystem;
        LocalBranch = localBranch;
        RemoteBranch = remoteBranch;
        GameChannelSDK = gameChannelSDK;
        ExtractOrGameDirectory = extractDirectory ?? gameFileSystem.GetGameDirectory();

        ProxiedChunksDirectory = kind is GamePackageOperationKind.Verify
            ? Path.Combine(gameFileSystem.GetChunksDirectory(), "repair")
            : gameFileSystem.GetChunksDirectory();

        BetaBuild = default!;
    }

    public GamePackageOperationContext(
        IServiceProvider serviceProvider,
        GamePackageOperationKind kind,
        IGameFileSystem gameFileSystem,
        SophonBuild betaBuild)
    {
        Kind = kind;
        Asset = serviceProvider.GetRequiredService<IDriverMediaTypeAwareFactory<IGameAssetOperation>>().Create(gameFileSystem.GetGameDirectory());
        GameFileSystem = gameFileSystem;
        BetaBuild = betaBuild;
        ExtractOrGameDirectory = gameFileSystem.GetGameDirectory();
        ProxiedChunksDirectory = gameFileSystem.GetChunksDirectory();

        LocalBranch = default!;
        RemoteBranch = default!;
        GameChannelSDK = default;
    }
}