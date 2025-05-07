// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Snap.Hutao.Core.DependencyInjection;

internal static partial class ServiceProviderExtension
{
    internal static readonly ConcurrentDictionary<CallerInfo, Void> CallerInfos = [];

    public static IServiceScope CreateScope(this IServiceScopeFactory factory, bool deferDispose,
        [CallerFilePath] string callerFilePath = default!,
        [CallerLineNumber] int callerLineNumer = default!,
        [CallerMemberName] string callerMemberName = default!)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(factory);
            return deferDispose
                ? new DeferDisposeServiceScope(DependencyInjection.DisposeDeferral(), factory.CreateScope(), new CallerInfo(callerFilePath, callerLineNumer, callerMemberName))
                : factory.CreateScope();
        }
        catch (ObjectDisposedException)
        {
            throw new OperationCanceledException("The IServiceProvider which the factory associated with is disposed");
        }
    }

    public static IServiceScope CreateScope(this IServiceProvider provider, bool deferDispose,
        [CallerFilePath] string callerFilePath = default!,
        [CallerLineNumber] int callerLineNumer = default!,
        [CallerMemberName] string callerMemberName = default!)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(provider);
            return deferDispose
                ? new DeferDisposeServiceScope(DependencyInjection.DisposeDeferral(), provider.CreateScope(), new CallerInfo(callerFilePath, callerLineNumer, callerMemberName))
                : provider.CreateScope();
        }
        catch (ObjectDisposedException)
        {
            throw new OperationCanceledException("The IServiceProvider is disposed");
        }
    }

    public static TService LockAndGetRequiredService<TService>(this IServiceProvider serviceProvider, Lock locker)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        lock (locker)
        {
            return serviceProvider.GetRequiredService<TService>();
        }
    }

    private sealed partial class DeferDisposeServiceScope : IServiceScope
    {
        private readonly IDisposable deferToken;
        private readonly IServiceScope scope;
        private readonly CallerInfo callerInfo;

        public DeferDisposeServiceScope(IDisposable deferToken, IServiceScope scope, CallerInfo callerInfo)
        {
            this.deferToken = deferToken;
            this.scope = scope;
            this.callerInfo = callerInfo;

            CallerInfos[callerInfo] = default;
        }

        public IServiceProvider ServiceProvider { get => scope.ServiceProvider; }

        public void Dispose()
        {
            scope.Dispose();
            deferToken.Dispose();
            CallerInfos.TryRemove(callerInfo, out _);
        }
    }

    internal sealed class CallerInfo
    {
        public CallerInfo(string callerFilePath, int callerLineNumer, string callerMemberName)
        {
            CallerFilePath = callerFilePath;
            CallerLineNumer = callerLineNumer;
            CallerMemberName = callerMemberName;
        }

        public string CallerFilePath { get; }

        public int CallerLineNumer { get; }

        public string CallerMemberName { get; }

        public override int GetHashCode()
        {
            return HashCode.Combine(CallerFilePath, CallerLineNumer, CallerMemberName);
        }
    }
}