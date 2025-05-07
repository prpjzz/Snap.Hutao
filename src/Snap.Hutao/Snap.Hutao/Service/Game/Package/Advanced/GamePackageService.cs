// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.DependencyInjection.Abstraction;
using Snap.Hutao.Core.IO.Compression.Zstandard;
using Snap.Hutao.Core.IO.Hashing;
using Snap.Hutao.Core.Threading.RateLimiting;
using Snap.Hutao.Factory.IO;
using Snap.Hutao.Factory.Progress;
using Snap.Hutao.Service.Game.Package.Advanced.PackageOperation;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.UI.Xaml.View.Window;
using Snap.Hutao.Web.Hoyolab.Downloader;
using Snap.Hutao.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Snap.Hutao.Web.Hoyolab.Takumi.Downloader.Proto;
using Snap.Hutao.Web.Response;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.RateLimiting;

namespace Snap.Hutao.Service.Game.Package.Advanced;

[ConstructorGenerated]
[Injection(InjectAs.Singleton, typeof(IGamePackageService))]
[SuppressMessage("", "CA1001")]
[SuppressMessage("", "SA1201")]
[SuppressMessage("", "SA1204")]
internal sealed partial class GamePackageService : IGamePackageService
{
    public const string HttpClientName = "SophonChunkRateLimited";

    private readonly GamePackageServiceOperationInformationTraits informationTraits;
    private readonly IMemoryStreamFactory memoryStreamFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IServiceProvider serviceProvider;

    private CancellationTokenSource? operationCts;
    private TaskCompletionSource? operationTcs;

    public async ValueTask<bool> ExecuteOperationAsync(GamePackageOperationContext operationContext)
    {
        await CancelOperationAsync().ConfigureAwait(false);

        operationCts = new();
        operationTcs = new();

        ParallelOptions options = new()
        {
            CancellationToken = operationCts.Token,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        using (IServiceScope scope = serviceProvider.CreateScope(true))
        {
            ITaskContext taskContext = scope.ServiceProvider.GetRequiredService<ITaskContext>();

            if (await informationTraits.EnsureAvailableFreeSpaceAndPrepareAsync(operationContext).ConfigureAwait(false) is not { } info)
            {
                return false;
            }

            await taskContext.SwitchToMainThreadAsync();

            GamePackageOperationWindow window = scope.ServiceProvider.GetRequiredService<GamePackageOperationWindow>();
            IProgress<GamePackageOperationReport> progress = scope.ServiceProvider
                .GetRequiredService<IProgressFactory>()
                .CreateForMainThread<GamePackageOperationReport>(window.HandleProgressUpdate);

            await taskContext.SwitchToBackgroundAsync();

            bool result;
            using (HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName))
            {
                using (TokenBucketRateLimiter? limiter = StreamCopyRateLimiter.Create(serviceProvider))
                {
                    IGamePackageOperation operation = scope.ServiceProvider.GetRequiredKeyedService<IGamePackageOperation>(operationContext.Kind);

                    try
                    {
                        GamePackageServiceContext serviceContext = new(operationContext, info, progress, options, httpClient, limiter);
                        await operation.ExecuteAsync(serviceContext).ConfigureAwait(false);
                        result = true;
                    }
                    catch (Exception ex)
                    {
                        serviceProvider.GetRequiredService<IInfoBarService>().Error(ex, SH.ServicePackageAdvancedExecuteOperationFailedTitle);
                        result = false;
                    }
                    finally
                    {
                        operationTcs.TrySetResult();
                    }
                }
            }

            await window.CloseTask.ConfigureAwait(false);
            return result;
        }
    }

    public async ValueTask CancelOperationAsync()
    {
        if (operationCts is null || operationTcs is null)
        {
            return;
        }

        await operationCts.CancelAsync().ConfigureAwait(false);
        await operationTcs.Task.ConfigureAwait(false);
        operationCts.Dispose();
        operationCts = null;
        operationTcs = null;
    }

    public async ValueTask<SophonDecodedBuild?> DecodeManifestsAsync(IGameFileSystemView gameFileSystem, BranchWrapper? branch, CancellationToken token = default)
    {
        if (branch is null)
        {
            return default;
        }

        SophonBuild? build;
        using (IServiceScope scope = serviceProvider.CreateScope(true))
        {
            Response<SophonBuild> response = await scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<ISophonClient>>()
                .Create(gameFileSystem.IsOversea())
                .GetBuildAsync(branch, token)
                .ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out build))
            {
                return default;
            }
        }

        long downloadTotalBytes = 0L;
        long totalBytes = 0L;
        ImmutableArray<SophonDecodedManifest>.Builder decodedManifests = ImmutableArray.CreateBuilder<SophonDecodedManifest>();
        using (HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName))
        {
            foreach (SophonManifest sophonManifest in build.Manifests)
            {
                bool exclude = sophonManifest.MatchingField switch
                {
                    "game" => false,
                    "zh-cn" => !gameFileSystem.Audio.Chinese,
                    "en-us" => !gameFileSystem.Audio.English,
                    "ja-jp" => !gameFileSystem.Audio.Japanese,
                    "ko-kr" => !gameFileSystem.Audio.Korean,
                    _ => true,
                };

                if (exclude)
                {
                    continue;
                }

                downloadTotalBytes += sophonManifest.Stats.CompressedSize;
                totalBytes += sophonManifest.Stats.UncompressedSize;

                string manifestDownloadUrl = $"{sophonManifest.ManifestDownload.UrlPrefix}/{sophonManifest.Manifest.Id}";
                using (Stream rawManifestStream = await httpClient.GetStreamAsync(manifestDownloadUrl, token).ConfigureAwait(false))
                {
                    using (ZstandardDecompressionStream decompressor = new(rawManifestStream))
                    {
                        using (MemoryStream inMemoryManifestStream = await memoryStreamFactory.GetStreamAsync(decompressor).ConfigureAwait(false))
                        {
                            string manifestMd5 = await Hash.ToHexStringAsync(HashAlgorithmName.MD5, inMemoryManifestStream, token).ConfigureAwait(false);
                            if (manifestMd5.Equals(sophonManifest.Manifest.Checksum, StringComparison.OrdinalIgnoreCase))
                            {
                                inMemoryManifestStream.Position = 0;
                                decodedManifests.Add(new(sophonManifest.ChunkDownload.UrlPrefix, SophonManifestProto.Parser.ParseFrom(inMemoryManifestStream)));
                            }
                        }
                    }
                }
            }
        }

        return new(build.Tag, downloadTotalBytes, totalBytes, decodedManifests.ToImmutable());
    }
}