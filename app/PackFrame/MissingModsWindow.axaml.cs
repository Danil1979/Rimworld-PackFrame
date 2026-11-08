using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MissingModsWindow : Window
{
    private readonly ManagedInstance? _instance;
    private readonly string? _language;

    private ModStatusList? _rowList;

    public MissingModsWindow()
    {
        InitializeComponent();
    }

    public MissingModsWindow(
        ManagedInstance instance,
        IReadOnlyList<MissingModRow> rows,
        string? language
    )
        : this()
    {
        _instance = instance;
        _language = language;
        _rowList = new ModStatusList(this, language);
        var title = Localization.ModAvailabilityTitle(language);
        Title = $"Rimworld PackFrame - {title}";
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("RefreshNoteText")!.Text =
            Localization.WorkshopAutoRefreshNote(language);
        this.FindControl<TextBlock>("LabelHeader")!.Text = Localization.FieldName(language);
        this.FindControl<TextBlock>("LinkHeader")!.Text = Localization.Link(language);
        this.FindControl<TextBlock>("StatusHeader")!.Text = Localization.StatusHeader(language);
        this.FindControl<Button>("HistoryButton")!.Content =
            Localization.MissingHistory(language);
        this.FindControl<Button>("FooterCloseButton")!.Content =
            Localization.CloseButton(language);
        UpdateRows(rows);

        KeyDown += OnWindowKeyDown;
    }

    public void UpdateRows(IReadOnlyList<MissingModRow> rows)
    {
        var missing = rows.Count(row => row.Availability == WorkshopModAvailability.Missing);
        var downloading = rows.Count(row => row.IsDownloading);
        var unknown = rows.Count(row => row.Availability == WorkshopModAvailability.Unknown);
        var checking = rows.Count(row => row.Availability == WorkshopModAvailability.CheckingSteam);
        this.FindControl<TextBlock>("SummaryText")!.Text = Localization.ModAvailabilitySummary(
            _language, _instance?.DisplayName ?? string.Empty, missing, downloading, unknown, checking);
        this.FindControl<TextBlock>("RefreshNoteText")!.IsVisible = _instance?.IsSteamCmd == false &&
            rows.Any(row => row.WorkshopId is not null && row.Availability != WorkshopModAvailability.Missing);
        _rowList?.Update(rows.Select(row => ModStatusDisplayRow.From(row, _language)));
        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.Text = ModStatusDisplayRow.EmptyAvailabilityText(_language);
        empty.IsVisible = rows.Count == 0;
    }

    private async void OnHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (_instance is null)
        {
            return;
        }

        var window = new MissingHistoryWindow(_instance, _language);
        await window.ShowDialog(this);
    }

    private void OnOpenLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModStatusListRow { Snapshot: var row } }) return;
        var status = this.FindControl<TextBlock>("LinkStatusText")!;
        if (ModStatusLink.TryOpen(row.Link, out var error))
        {
            status.IsVisible = false;
            return;
        }
        status.Text = ModStatusLink.FailureText(_language);
        status.IsVisible = true;
        if (error is not null) AppLog.Error("Open mod web link", error);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
