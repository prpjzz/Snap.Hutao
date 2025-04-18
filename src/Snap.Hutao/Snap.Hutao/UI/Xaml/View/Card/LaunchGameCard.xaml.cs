// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;

namespace Snap.Hutao.UI.Xaml.View.Card;

internal sealed partial class LaunchGameCard : Button
{
    public LaunchGameCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeDataContext<ViewModel.Game.LaunchGameViewModelSlim>(serviceProvider);
    }
}