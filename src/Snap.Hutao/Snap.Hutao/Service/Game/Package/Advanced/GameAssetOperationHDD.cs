// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Win32.SafeHandles;
using Snap.Hutao.Core.IO;
using Snap.Hutao.Core.IO.Compression.Zstandard;
using Snap.Hutao.Web.Hoyolab.Takumi.Downloader.Proto;
using System.Buffers;
using System.IO;

namespace Snap.Hutao.Service.Game.Package.Advanced;

[ConstructorGenerated(CallBaseConstructor = true)]
[Injection(InjectAs.Transient)]
internal sealed partial class GameAssetOperationHDD : GameAssetOperation
{
    public override async ValueTask InstallAssetsAsync(GamePackageServiceContext context, SophonDecodedBuild remoteBuild)
    {
        foreach (SophonDecodedManifest manifest in remoteBuild.Manifests)
        {
            IEnumerable<SophonAssetOperation> assets = manifest.ManifestProto.Assets.Select(asset => SophonAssetOperation.AddOrRepair(manifest.UrlPrefix, manifest.UrlSuffix, asset));
            foreach (SophonAssetOperation asset in assets)
            {
                await EnsureAssetAsync(context, asset).ConfigureAwait(false);
            }
        }
    }

    public override async ValueTask UpdateDiffAssetsAsync(GamePackageServiceContext context, List<SophonAssetOperation> diffAssets)
    {
        foreach (SophonAssetOperation asset in diffAssets)
        {
            ValueTask task = asset.Kind switch
            {
                SophonAssetOperationKind.AddOrRepair or SophonAssetOperationKind.Modify => EnsureAssetAsync(context, asset),
                SophonAssetOperationKind.Delete => DeleteAssetAsync(context, asset.OldAsset),
                _ => ValueTask.CompletedTask,
            };

            await task.ConfigureAwait(false);
        }
    }

    public override async ValueTask PredownloadDiffAssetsAsync(GamePackageServiceContext context, List<SophonAssetOperation> diffAssets)
    {
        foreach (SophonAssetOperation asset in diffAssets)
        {
            IList<SophonChunk> chunks = asset.Kind switch
            {
                SophonAssetOperationKind.AddOrRepair => asset.NewAsset.AssetChunks.Select(c => new SophonChunk(asset.UrlPrefix, asset.UrlSuffix, c)).ToList(),
                SophonAssetOperationKind.Modify => asset.DiffChunks,
                _ => [],
            };

            await DownloadChunksAsync(context, chunks).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VerifyManifestsAsync(GamePackageServiceContext context, SophonDecodedBuild build, Action<SophonAssetOperation> conflictHandler)
    {
        foreach (SophonDecodedManifest manifest in build.Manifests)
        {
            await VerifyManifestAsync(context, manifest, conflictHandler).ConfigureAwait(false);
        }
    }

    protected override async ValueTask VerifyManifestAsync(GamePackageServiceContext context, SophonDecodedManifest manifest, Action<SophonAssetOperation> conflictHandler)
    {
        foreach (AssetProperty asset in manifest.ManifestProto.Assets)
        {
            await VerifyAssetAsync(context, new(manifest.UrlPrefix, manifest.UrlSuffix, asset), conflictHandler).ConfigureAwait(false);
        }
    }

    protected override async ValueTask RepairAssetsAsync(GamePackageServiceContext context, GamePackageIntegrityInfo info)
    {
        foreach (SophonAssetOperation asset in info.ConflictedAssets)
        {
            await EnsureAssetAsync(context, asset).ConfigureAwait(false);
        }
    }

    protected override async ValueTask DownloadChunksAsync(GamePackageServiceContext context, IList<SophonChunk> sophonChunks)
    {
        foreach (SophonChunk chunk in sophonChunks)
        {
            await DownloadChunkAsync(context, chunk).ConfigureAwait(false);
        }
    }

    protected override async ValueTask MergeNewAssetAsync(GamePackageServiceContext context, AssetProperty assetProperty)
    {
        CancellationToken token = context.CancellationToken;

        string path = context.EnsureAssetTargetDirectoryExists(assetProperty.AssetName);
        using (SafeFileHandle fileHandle = File.OpenHandle(path, FileMode.Create, FileAccess.Write, FileShare.None, preallocationSize: 32 * 1024))
        {
            using (IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent(81920))
            {
                Memory<byte> buffer = memoryOwner.Memory;

                foreach (AssetChunk chunk in assetProperty.AssetChunks)
                {
                    string chunkPath = Path.Combine(context.Operation.ProxiedChunksDirectory, chunk.ChunkName);
                    if (!File.Exists(chunkPath))
                    {
                        continue;
                    }

                    using (await context.ExclusiveProcessChunkAsync(chunk.ChunkName, token).ConfigureAwait(false))
                    {
                        using (FileStream chunkFile = File.OpenRead(chunkPath))
                        {
                            using (ZstandardDecompressionStream decompressor = new(chunkFile))
                            {
                                long offset = chunk.ChunkOnFileOffset;
                                do
                                {
                                    int bytesRead = await decompressor.ReadAsync(buffer, token).ConfigureAwait(false);
                                    if (bytesRead <= 0)
                                    {
                                        break;
                                    }

                                    await RandomAccess.WriteAsync(fileHandle, buffer[..bytesRead], offset, token).ConfigureAwait(false);
                                    context.Progress.Report(new GamePackageOperationReport.Install(bytesRead, 0, chunk.ChunkName));
                                    offset += bytesRead;
                                }
                                while (true);
                            }
                        }

                        if (context.Operation.Kind is GamePackageOperationKind.Update && !context.DuplicatedChunkNames.ContainsKey(chunk.ChunkName))
                        {
                            FileOperation.Delete(chunkPath);
                        }
                    }

                    context.Progress.Report(new GamePackageOperationReport.Install(0, 1, chunk.ChunkName));
                }
            }
        }
    }
}