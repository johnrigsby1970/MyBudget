using CommunityToolkit.Mvvm.ComponentModel;

namespace StayOnTarget.ViewModels;

public partial class NavigationItemViewModel : ObservableObject {
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _iconKind = string.Empty; // PackIcon / MaterialDesign Icon Kind name

    [ObservableProperty]
    private int _tabIndex;

    [ObservableProperty]
    private int _badgeCount;

    [ObservableProperty]
    private bool _hasBadge;

    partial void OnBadgeCountChanged(int value) {
        HasBadge = value > 0;
    }
}