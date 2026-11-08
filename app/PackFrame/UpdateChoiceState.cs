using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PackFrame;

internal sealed class UpdateChoiceRow : INotifyPropertyChanged
{
    private bool _useUpdate = true;
    private readonly string? _language;
    internal UpdateChoiceRow(string path, string? language) { Path = path; _language = language; }
    public string Path { get; }
    public bool UseUpdate
    {
        get => _useUpdate;
        set
        {
            if (_useUpdate == value) return;
            _useUpdate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseUpdate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleName)));
        }
    }
    public string AccessibleName => (UseUpdate
        ? OperationText.Choose(_language, "Use update: ", "使用更新：", "使用更新：", "更新版を使用: ")
        : OperationText.Choose(_language, "Keep local: ", "保留本地：", "保留本機：", "ローカル版を保持: ")) + Path;
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Filtering changes only the view. Confirmation always includes hidden decisions.
internal sealed class UpdateChoiceState
{
    internal UpdateChoiceState(IEnumerable<string> paths, string? language) =>
        Rows = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path => new UpdateChoiceRow(path, language)).ToList();
    internal IReadOnlyList<UpdateChoiceRow> Rows { get; }
    internal int UseUpdateCount => Rows.Count(row => row.UseUpdate);
    internal IReadOnlyList<UpdateChoiceRow> Filter(string? query)
    {
        var term = query?.Trim() ?? string.Empty;
        return Rows.Where(row => term.Length == 0 || row.Path.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    internal void SetAll(bool useUpdate)
    {
        foreach (var row in Rows) row.UseUpdate = useUpdate;
    }
    internal UpdateChoices Confirm(bool keepOrder) => UpdateChoices.FromReplacements(
        Rows.Select(row => new KeyValuePair<string, bool>(row.Path, row.UseUpdate)), keepOrder);
}
