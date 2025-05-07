// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Service;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Web.Response;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Globalization;

namespace Snap.Hutao.Core.DependencyInjection;

internal static class DependencyInjection
{
    private static readonly AsyncReaderWriterLock Lock = new();

    public static IDisposable DisposeDeferral()
    {
        if (XamlApplicationLifetime.Exited)
        {
            HutaoException.OperationCanceled("Application has exited");
        }

        if (!Lock.TryReaderLock(out AsyncReaderWriterLock.Releaser releaser))
        {
            HutaoException.OperationCanceled("Root ServiceProvider is disposing");
        }

        return releaser;
    }

    public static void WaitForDispose()
    {
        _ = ServiceProviderExtension.CallerInfos;
        Lock.WriterLockAsync().GetAwaiter().GetResult().Dispose();
    }

    public static ServiceProvider Initialize()
    {
        ServiceProvider serviceProvider = new ServiceCollection()

            // Microsoft extension
            .AddLogging(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter(DbLoggerCategory.Database.Command.Name, level => level >= LogLevel.Information)
                    .AddFilter(DbLoggerCategory.Query.Name, level => level >= LogLevel.Information)
                    .AddDebug()
                    .AddSentryTelemetry()
                    .AddConsoleWindow();
            })
            .AddMemoryCache()

            // Quartz
            .AddQuartz()

            // Hutao extensions
            .AddJsonOptions()
            .AddDatabase()
            .AddInjections()
            .AddResponseValidation()
            .AddConfiguredHttpClients()

            // Discrete services
            .AddSingleton<IMessenger, WeakReferenceMessenger>()
            .BuildServiceProvider(true);

        Ioc.Default.ConfigureServices(serviceProvider);

        serviceProvider.InitializeConsoleWindow();
        serviceProvider.InitializeCulture();
        serviceProvider.InitializeNotification();

        return serviceProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeCulture(this IServiceProvider serviceProvider)
    {
        CultureOptions cultureOptions = serviceProvider.GetRequiredService<CultureOptions>();
        cultureOptions.SystemCulture = CultureInfo.CurrentCulture;

        CultureInfo cultureInfo = cultureOptions.CurrentCulture;

        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;

        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = cultureInfo.Name;
        }
        catch (COMException)
        {
            // 0x80070032 ERROR_NOT_SUPPORTED
        }

        SH.Culture = cultureInfo;
        XamlApplicationLifetime.CultureInfoInitialized = true;

        ILogger<CultureOptions> logger = serviceProvider.GetRequiredService<ILogger<CultureOptions>>();
        logger.LogDebug("System Culture: \e[1m\e[36m{System}\e[37m", cultureOptions.SystemCulture);
        logger.LogDebug("Current Culture: \e[1m\e[36m{Current}\e[37m", cultureInfo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeConsoleWindow(this IServiceProvider serviceProvider)
    {
        _ = serviceProvider.GetRequiredService<ConsoleWindowLifeTime>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InitializeNotification(this IServiceProvider serviceProvider)
    {
        _ = serviceProvider.GetRequiredService<IAppNotificationLifeTime>();
    }
}