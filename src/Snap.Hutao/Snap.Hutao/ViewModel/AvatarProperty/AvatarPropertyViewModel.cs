// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Model;
using Snap.Hutao.Model.Calculable;
using Snap.Hutao.Model.Entity.Primitive;
using Snap.Hutao.Model.Intrinsic;
using Snap.Hutao.Model.Intrinsic.Frozen;
using Snap.Hutao.Model.Metadata.Converter;
using Snap.Hutao.Service.AvatarInfo;
using Snap.Hutao.Service.AvatarInfo.Factory;
using Snap.Hutao.Service.Cultivation;
using Snap.Hutao.Service.Cultivation.Consumption;
using Snap.Hutao.Service.Metadata.ContextAbstraction;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.User;
using Snap.Hutao.UI.Xaml.Control.AutoSuggestBox;
using Snap.Hutao.UI.Xaml.View.Dialog;
using Snap.Hutao.ViewModel.User;
using Snap.Hutao.Web.Hoyolab.Takumi.Event.Calculate;
using Snap.Hutao.Web.Response;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CalculatorAvatarPromotionDelta = Snap.Hutao.Web.Hoyolab.Takumi.Event.Calculate.AvatarPromotionDelta;
using CalculatorBatchConsumption = Snap.Hutao.Web.Hoyolab.Takumi.Event.Calculate.BatchConsumption;
using CalculatorConsumption = Snap.Hutao.Web.Hoyolab.Takumi.Event.Calculate.Consumption;
using CalculatorItemHelper = Snap.Hutao.Web.Hoyolab.Takumi.Event.Calculate.ItemHelper;

namespace Snap.Hutao.ViewModel.AvatarProperty;

[ConstructorGenerated]
[Injection(InjectAs.Scoped)]
internal sealed partial class AvatarPropertyViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>
{
    private readonly AvatarPropertyViewModelScopeContext scopeContext;

    private SummaryFactoryMetadataContext? metadataContext;
    private FrozenDictionary<string, SearchToken> availableTokens;

    public Summary? Summary { get; set => SetProperty(ref field, value); }

    public ObservableCollection<SearchToken>? FilterTokens { get; set => SetProperty(ref field, value); }

    public string? FilterToken { get; set => SetProperty(ref field, value); }

    public FrozenDictionary<string, SearchToken> AvailableTokens { get => availableTokens; set => SetProperty(ref availableTokens, value); }

    public string TotalAvatarCount
    {
        get => SH.FormatViewModelAvatarPropertyTotalAvatarCountHint(Summary?.Avatars.Count ?? 0);
    }

    public ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> SortDescriptionKinds { get; } = ImmutableCollectionsNameValue.FromEnum<AvatarPropertySortDescriptionKind>(type => type.GetLocalizedDescription());

    public NameValue<AvatarPropertySortDescriptionKind>? SortDescriptionKind
    {
        get => field ??= SortDescriptionKinds.FirstOrDefault();
        set
        {
            if (value is not null && SetProperty(ref field, value))
            {
                using (Summary?.Avatars.DeferRefresh())
                {
                    Summary?.Avatars.SortDescriptions.Clear();
                    foreach (ref readonly SortDescription sd in AvatarPropertySortDescriptions.Get(value.Value).AsSpan())
                    {
                        Summary?.Avatars.SortDescriptions.Add(sd);
                    }
                }

                Summary?.Avatars.MoveCurrentToFirst();
            }
        }
    }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is not { } userAndUid)
        {
            return;
        }

        WeakReference<AvatarPropertyViewModel> weakThis = new(this);

        // 1. We need to wait for the view initialization (mainly for metadata context).
        // 2. We need to refresh summary data. otherwise, the view can be un-synced.
        Initialization.Task.ContinueWith(
            init =>
            {
                if (!init.Result)
                {
                    return;
                }

                if (weakThis.TryGetTarget(out AvatarPropertyViewModel? viewModel) && !viewModel.IsViewDisposed)
                {
                    viewModel.PrivateRefreshAsync(userAndUid, RefreshOptionKind.None, viewModel.CancellationToken).SafeForget();
                }
            },
            TaskScheduler.Current);
    }

    protected override async ValueTask<bool> LoadOverrideAsync()
    {
        if (!await scopeContext.MetadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await scopeContext.MetadataService.GetContextAsync<SummaryFactoryMetadataContext>().ConfigureAwait(false);
        availableTokens = FrozenDictionary.ToFrozenDictionary(
        [
            .. IntrinsicFrozen.ElementNameValues.Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.ElementName, nv.Name, nv.Value, iconUri: ElementNameIconConverter.ElementNameToUri(nv.Name)))),
            .. IntrinsicFrozen.ItemQualityNameValues.Where(nv => nv.Value >= QualityType.QUALITY_PURPLE).Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.ItemQuality, nv.Name, (int)nv.Value, quality: QualityColorConverter.QualityToColor(nv.Value)))),
            .. IntrinsicFrozen.WeaponTypeNameValues.Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.WeaponType, nv.Name, (int)nv.Value, iconUri: WeaponTypeIconConverter.WeaponTypeToIconUri(nv.Value)))),
        ]);

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            await PrivateRefreshAsync(userAndUid, RefreshOptionKind.None, CancellationToken).ConfigureAwait(false);
        }

        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        FilterTokens = [];
        return true;
    }

    [Command("RefreshFromHoyolabGameRecordCommand")]
    private async Task RefreshByHoyolabGameRecordAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh", "AvatarPropertyViewModel.Command"));

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            await PrivateRefreshAsync(userAndUid, RefreshOptionKind.RequestFromHoyolabGameRecord, CancellationToken).ConfigureAwait(false);
        }
    }

    [SuppressMessage("", "SH003")]
    private async Task PrivateRefreshAsync(UserAndUid userAndUid, RefreshOptionKind optionKind, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(metadataContext);

        try
        {
            await scopeContext.TaskContext.SwitchToMainThreadAsync();
            Summary = default;

            ValueResult<RefreshResultKind, Summary?> summaryResult;
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                ContentDialog dialog = await scopeContext.ContentDialogFactory
                    .CreateForIndeterminateProgressAsync(SH.ViewModelAvatarPropertyFetch)
                    .ConfigureAwait(false);

                using (await scopeContext.ContentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
                {
                    summaryResult = await scopeContext.AvatarInfoService
                        .GetSummaryAsync(metadataContext, userAndUid, optionKind, token)
                        .ConfigureAwait(false);
                }
            }

            (RefreshResultKind result, Summary? summary) = summaryResult;
            if (result is RefreshResultKind.Ok)
            {
                await scopeContext.TaskContext.SwitchToMainThreadAsync();
                Summary = summary;
                Summary?.Avatars.MoveCurrentToFirst();
            }
            else
            {
                switch (result)
                {
                    case RefreshResultKind.APIUnavailable:
                        scopeContext.InfoBarService.Warning(SH.ViewModelAvatarPropertyEnkaApiUnavailable);
                        break;

                    case RefreshResultKind.StatusCodeNotSucceed:
                        ArgumentNullException.ThrowIfNull(summary);
                        scopeContext.InfoBarService.Warning(summary.Message);
                        break;

                    case RefreshResultKind.ShowcaseNotOpen:
                        scopeContext.InfoBarService.Warning(SH.ViewModelAvatarPropertyShowcaseNotOpen);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Command("CultivateCommand")]
    private async Task CultivateAsync(AvatarView? avatar)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Cultivate", "AvatarPropertyViewModel.Command"));

        if (avatar is null)
        {
            return;
        }

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            scopeContext.InfoBarService.Warning(SH.MustSelectUserAndUid);
            return;
        }

        if (avatar.Weapon is null)
        {
            scopeContext.InfoBarService.Warning(SH.ViewModelAvatarPropertyCalculateWeaponNull);
            return;
        }

        CalculableOptions options = new(avatar.ToCalculable(), avatar.Weapon.ToCalculable());
        CultivatePromotionDeltaDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaDialog>(scopeContext.ServiceProvider, options).ConfigureAwait(false);
        (bool isOk, CultivatePromotionDeltaOptions deltaOptions) = await dialog.GetPromotionDeltaAsync().ConfigureAwait(false);

        if (!isOk)
        {
            return;
        }

        CalculatorBatchConsumption? batchConsumption;
        using (IServiceScope scope = scopeContext.ServiceScopeFactory.CreateScope(true))
        {
            CalculateClient calculatorClient = scope.ServiceProvider.GetRequiredService<CalculateClient>();
            Response<CalculatorBatchConsumption> response = await calculatorClient
                .BatchComputeAsync(userAndUid, deltaOptions.Delta).ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(response, scopeContext.InfoBarService, out batchConsumption))
            {
                return;
            }
        }

        if (!await SaveCultivationAsync(batchConsumption.Items.Single(), deltaOptions).ConfigureAwait(false))
        {
            scopeContext.InfoBarService.Warning(SH.ViewModelCultivationEntryAddWarning);
        }
    }

    [Command("BatchCultivateCommand")]
    private async Task BatchCultivateAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Batch cultivate", "AvatarPropertyViewModel.Command"));

        if (Summary is not { Avatars: { } avatars })
        {
            return;
        }

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            scopeContext.InfoBarService.Warning(SH.MustSelectUserAndUid);
            return;
        }

        CultivatePromotionDeltaBatchDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaBatchDialog>(scopeContext.ServiceProvider).ConfigureAwait(false);
        (bool isOk, CultivatePromotionDeltaOptions deltaOptions) = await dialog.GetPromotionDeltaBaselineAsync().ConfigureAwait(false);

        if (!isOk)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(deltaOptions.Delta.Weapon);

        ContentDialog progressDialog = await scopeContext.ContentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelAvatarPropertyBatchCultivateProgressTitle)
            .ConfigureAwait(false);

        BatchCultivateResult result = default;
        using (await scopeContext.ContentDialogFactory.BlockAsync(progressDialog).ConfigureAwait(false))
        {
            ImmutableArray<CalculatorAvatarPromotionDelta>.Builder deltasBuilder = ImmutableArray.CreateBuilder<CalculatorAvatarPromotionDelta>();
            foreach (AvatarView avatar in avatars)
            {
                if (!deltaOptions.Delta.TryGetNonErrorCopy(avatar, out CalculatorAvatarPromotionDelta? copy))
                {
                    ++result.SkippedCount;
                    continue;
                }

                deltasBuilder.Add(copy);
            }

            ImmutableArray<CalculatorAvatarPromotionDelta> deltas = deltasBuilder.ToImmutable();

            CalculatorBatchConsumption? batchConsumption;
            using (IServiceScope scope = scopeContext.ServiceScopeFactory.CreateScope(true))
            {
                CalculateClient calculatorClient = scope.ServiceProvider.GetRequiredService<CalculateClient>();
                Response<CalculatorBatchConsumption> response = await calculatorClient.BatchComputeAsync(userAndUid, deltas).ConfigureAwait(false);

                if (!ResponseValidator.TryValidate(response, scopeContext.InfoBarService, out batchConsumption))
                {
                    return;
                }
            }

            foreach ((CalculatorConsumption consumption, CalculatorAvatarPromotionDelta delta) in batchConsumption.Items.Zip(deltas))
            {
                if (!await SaveCultivationAsync(consumption, new(delta, deltaOptions.Strategy)).ConfigureAwait(false))
                {
                    break;
                }

                ++result.SucceedCount;
            }
        }

        if (result.SkippedCount > 0)
        {
            scopeContext.InfoBarService.Warning(SH.FormatViewModelCultivationBatchAddIncompletedFormat(result.SucceedCount, result.SkippedCount));
        }
        else
        {
            scopeContext.InfoBarService.Success(SH.FormatViewModelCultivationBatchAddCompletedFormat(result.SucceedCount, result.SkippedCount));
        }
    }

    /// <returns><see langword="true"/> if we can continue saving consumptions, otherwise <see langword="false"/>.</returns>
    private async ValueTask<bool> SaveCultivationAsync(CalculatorConsumption consumption, CultivatePromotionDeltaOptions options)
    {
        LevelInformation levelInformation = LevelInformation.From(options.Delta);

        InputConsumption avatarInput = new()
        {
            Type = CultivateType.AvatarAndSkill,
            ItemId = options.Delta.AvatarId,
            Items = CalculatorItemHelper.Merge(consumption.AvatarConsume, consumption.AvatarSkillConsume),
            LevelInformation = levelInformation,
            Strategy = options.Strategy,
        };

        ConsumptionSaveResultKind avatarSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(avatarInput).ConfigureAwait(false);

        _ = avatarSaveKind switch
        {
            ConsumptionSaveResultKind.NoProject => scopeContext.InfoBarService.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.Skipped => scopeContext.InfoBarService.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
            ConsumptionSaveResultKind.NoItem => scopeContext.InfoBarService.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => scopeContext.InfoBarService.Success(SH.ViewModelCultivationEntryAddSuccess),
            _ => default,
        };

        if (avatarSaveKind is ConsumptionSaveResultKind.NoProject)
        {
            return false;
        }

        try
        {
            ArgumentNullException.ThrowIfNull(options.Delta.Weapon);

            InputConsumption weaponInput = new()
            {
                Type = CultivateType.Weapon,
                ItemId = options.Delta.Weapon.Id,
                Items = consumption.WeaponConsume,
                LevelInformation = levelInformation,
                Strategy = options.Strategy,
            };

            ConsumptionSaveResultKind weaponSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(weaponInput).ConfigureAwait(false);
            _ = weaponSaveKind switch
            {
                ConsumptionSaveResultKind.NoProject => scopeContext.InfoBarService.Warning(SH.ViewModelCultivationEntryAddWarning),
                ConsumptionSaveResultKind.Skipped => scopeContext.InfoBarService.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
                ConsumptionSaveResultKind.NoItem => scopeContext.InfoBarService.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
                ConsumptionSaveResultKind.Added => scopeContext.InfoBarService.Success(SH.ViewModelCultivationEntryAddSuccess),
                _ => default,
            };

            return weaponSaveKind is not ConsumptionSaveResultKind.NoProject;
        }
        catch (HutaoException ex)
        {
            scopeContext.InfoBarService.Error(ex, SH.ViewModelCultivationAddWarning);
        }

        return false;
    }

    [Command("ExportToTextCommand")]
    private async Task ExportToTextAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Export as text to ClipBoard", "AvatarPropertyViewModel.Command"));

        if (Summary is not { Avatars.CurrentItem: { } avatar })
        {
            return;
        }

        if (await scopeContext.ClipboardProvider.SetTextAsync(AvatarViewTextTemplating.GetTemplatedText(avatar)).ConfigureAwait(false))
        {
            scopeContext.InfoBarService.Success(SH.ViewModelAvatatPropertyExportTextSuccess);
        }
        else
        {
            scopeContext.InfoBarService.Warning(SH.ViewModelAvatatPropertyExportTextError);
        }
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Filter", "AvatarPropertyViewModel.Command"));

        if (Summary is null)
        {
            return;
        }

        Summary.Avatars.Filter = FilterTokens is null or [] ? default! : AvatarViewFilter.Compile(FilterTokens);
        OnPropertyChanged(nameof(TotalAvatarCount));

        if (Summary.Avatars.CurrentItem is null)
        {
            Summary.Avatars.MoveCurrentToFirst();
        }
    }
}