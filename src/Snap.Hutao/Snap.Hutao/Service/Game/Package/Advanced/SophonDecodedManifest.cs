// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Web.Hoyolab.Downloader;
using Snap.Hutao.Web.Hoyolab.Takumi.Downloader.Proto;

namespace Snap.Hutao.Service.Game.Package.Advanced;

internal sealed class SophonDecodedManifest
{
    public SophonDecodedManifest(ManifestDownloadInfo manifestDownloadInfo, SophonManifestProto sophonManifestProto)
        : this(manifestDownloadInfo.UrlPrefix, manifestDownloadInfo.UrlSuffix, sophonManifestProto)
    {
    }

    public SophonDecodedManifest(string urlPrefix, string urlSuffix, SophonManifestProto sophonManifestProto)
    {
        UrlPrefix = string.Intern(urlPrefix);
        UrlSuffix = string.IsNullOrEmpty(urlSuffix) ? string.Empty : string.Intern($"?{urlSuffix}");
        ManifestProto = sophonManifestProto;
    }

    public string UrlPrefix { get; }

    public string UrlSuffix { get; }

    public SophonManifestProto ManifestProto { get; }
}