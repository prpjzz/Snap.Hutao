// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Setting;

namespace Snap.Hutao.ViewModel.Setting;

internal sealed class HomeCardOptions
{
    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardLaunchGamePresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardLaunchGamePresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardLaunchGamePresented, value);
    }

    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardGachaStatisticsPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardGachaStatisticsPresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardGachaStatisticsPresented, value);
    }

    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardAchievementPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardAchievementPresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardAchievementPresented, value);
    }

    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardDailyNotePresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardDailyNotePresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardDailyNotePresented, value);
    }

    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardCalendarPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardCalendarPresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardCalendarPresented, value);
    }

    [SuppressMessage("", "CA1822")]
    public static bool IsHomeCardSignInPresented
    {
        get => LocalSetting.Get(SettingKeys.IsHomeCardSignInPresented, true);
        set => LocalSetting.Set(SettingKeys.IsHomeCardSignInPresented, value);
    }
}