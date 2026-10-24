using System.Collections.ObjectModel;
using DvPhotoValidator.Core.Models;

namespace DvPhotoValidator.App.ViewModels;

public sealed class IssueGroupViewModel : ObservableObject
{
    private ValidationStatus? _status;
    private string _placeholder;

    public IssueGroupViewModel(string title, string hint, string placeholder)
    {
        Title = title;
        Hint = hint;
        _placeholder = placeholder;
    }

    public string Title { get; }

    public string Hint { get; }

    public ObservableCollection<ValidationIssue> Items { get; } = [];

    public ValidationStatus? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Placeholder
    {
        get => _placeholder;
        private set => SetProperty(ref _placeholder, value);
    }

    public bool HasItems => Items.Count > 0;

    public void Update(IEnumerable<ValidationIssue> issues)
    {
        Items.Clear();
        foreach (var issue in issues)
        {
            Items.Add(issue);
        }

        Status = ValidationReport.Summarize(Items);
        OnPropertyChanged(nameof(HasItems));
    }

    public void Clear(string? placeholder = null)
    {
        if (placeholder is not null)
        {
            Placeholder = placeholder;
        }

        Update([]);
    }
}

public sealed record HistoryEntry(DateTime Time, string Action, string Details)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}
