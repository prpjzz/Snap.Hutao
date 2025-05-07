// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Snap.Hutao.Core.Graphics;
using Snap.Hutao.Core.LifeCycle;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Service.Hutao;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Windows.Graphics;

namespace Snap.Hutao.UI.Xaml.View.Window;

internal sealed partial class ExceptionWindow : Microsoft.UI.Xaml.Window, INotifyPropertyChanged
{
    private readonly SentryId id;

    public ExceptionWindow(SentryId id)
    {
        // Message pump will die if we introduce XamlWindowController
        InitializeComponent();
        this.id = id;

        AppWindow.Title = "Snap Hutao Exception Report";

        AppWindowTitleBar titleBar = AppWindow.TitleBar;
        titleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        titleBar.ExtendsContentIntoTitleBar = true;

        Closed += (_, _) => Process.GetCurrentProcess().Kill();

        UpdateDragRectangles();
        DraggableGrid.SizeChanged += (_, _) => UpdateDragRectangles();

        SizeInt32 size = new(800, 600);
        AppWindow.Resize(size.Scale(this.GetRasterizationScale()));
        Bindings.Update();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TraceId { get => $"trace.id: {id}"; }

    public string? Comment { get; set => SetProperty(ref field, value); }

    public static void Show(SentryId id)
    {
        ExceptionWindow window = new(id);
        window.AppWindow.Show(true);
        window.AppWindow.MoveInZOrderAtTop();
    }

    [Command("CloseCommand")]
    private async Task CloseWindow()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Close Window", "ExceptionWindow.Command"));

        Bindings.Update();
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            string email = await Ioc.Default.GetRequiredService<HutaoUserOptions>().GetActualUserNameAsync().ConfigureAwait(true) ?? "Anonymous";
            SentrySdk.CaptureFeedback(Comment, email);
        }

        await SentrySdk.FlushAsync().ConfigureAwait(true);
        Close();
    }

    private void UpdateDragRectangles()
    {
        if (!DraggableGrid.IsLoaded)
        {
            return;
        }

        Point position = DraggableGrid.TransformToVisual(Content).TransformPoint(default);
        RectInt32 dragRect = RectInt32Convert.RectInt32(position, DraggableGrid.ActualSize).Scale(this.GetRasterizationScale());
        InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Caption, [dragRect]);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}