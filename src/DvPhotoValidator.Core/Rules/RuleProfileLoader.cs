using System.Text.Json;

namespace DvPhotoValidator.Core.Rules;

public sealed record RuleProfileLoadResult(IReadOnlyList<RuleProfile> Profiles, IReadOnlyList<string> Errors);

/// <summary>Loads rule profiles from *.json files so that rules can be updated without recompiling.</summary>
public static class RuleProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RuleProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<RuleProfile>(json, Options)
            ?? throw new InvalidDataException("Пустой файл профиля.");
        var problems = profile.Validate().ToList();
        if (problems.Count > 0)
        {
            throw new InvalidDataException(string.Join(" ", problems));
        }

        return profile;
    }

    /// <summary>
    /// Loads every profile in the directory. Broken files are reported, not thrown.
    /// Falls back to <see cref="RuleProfile.Baseline"/> when nothing valid is found.
    /// </summary>
    public static RuleProfileLoadResult LoadDirectory(string directory)
    {
        var profiles = new List<RuleProfile>();
        var errors = new List<string>();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    profiles.Add(Parse(File.ReadAllText(file)));
                }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
                {
                    errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        if (profiles.Count == 0)
        {
            profiles.Add(RuleProfile.Baseline);
        }

        return new RuleProfileLoadResult(profiles, errors);
    }
}
