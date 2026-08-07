namespace SlashText.Services;

internal sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private SemanticVersion(int major, int minor, int patch, string[] prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public IReadOnlyList<string> Prerelease { get; }
    public bool IsPrerelease => Prerelease.Count > 0;

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = new SemanticVersion(0, 0, 0, []);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }
        var pieces = normalized.Split('-', 2);
        var numbers = pieces[0].Split('.');
        if (numbers.Length != 3 ||
            !int.TryParse(numbers[0], out var major) || major < 0 ||
            !int.TryParse(numbers[1], out var minor) || minor < 0 ||
            !int.TryParse(numbers[2], out var patch) || patch < 0)
        {
            return false;
        }

        var prerelease = pieces.Length == 1 ? [] : pieces[1].Split('.');
        if (prerelease.Any(identifier => string.IsNullOrWhiteSpace(identifier) ||
                                         identifier.Any(character =>
                                             !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            return false;
        }
        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }
        var core = Major.CompareTo(other.Major);
        core = core != 0 ? core : Minor.CompareTo(other.Minor);
        core = core != 0 ? core : Patch.CompareTo(other.Patch);
        if (core != 0 || (!IsPrerelease && !other.IsPrerelease))
        {
            return core;
        }
        if (!IsPrerelease)
        {
            return 1;
        }
        if (!other.IsPrerelease)
        {
            return -1;
        }

        for (var index = 0; index < Math.Max(Prerelease.Count, other.Prerelease.Count); index++)
        {
            if (index >= Prerelease.Count) return -1;
            if (index >= other.Prerelease.Count) return 1;
            var leftNumeric = int.TryParse(Prerelease[index], out var leftNumber);
            var rightNumeric = int.TryParse(other.Prerelease[index], out var rightNumber);
            if (leftNumeric && rightNumeric)
            {
                var numeric = leftNumber.CompareTo(rightNumber);
                if (numeric != 0) return numeric;
            }
            else if (leftNumeric != rightNumeric)
            {
                return leftNumeric ? -1 : 1;
            }
            else
            {
                var text = string.CompareOrdinal(Prerelease[index], other.Prerelease[index]);
                if (text != 0) return text;
            }
        }
        return 0;
    }

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}" +
        (IsPrerelease ? $"-{string.Join('.', Prerelease)}" : string.Empty);
}
