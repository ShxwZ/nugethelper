using System.IO;
using System.Text.RegularExpressions;

public class PackageVersion
{
    public PackageVersion()
    {
        PreviewSuffix = string.Empty;
    }
    public PackageVersion(string version)
    {
        const string errorMsg = "Invalid Version string";
        if (!TrySet(version, this))
            throw new InvalidDataException(errorMsg);
    }
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public int Revision { get; set; }
    public string PreviewSuffix { get; set; }

    private static bool TrySet(string input, PackageVersion version)
    {
        version.Revision = 0; // Default value
        version.PreviewSuffix = string.Empty;

        string[] mainAndSuffix = input.Split('-');
        string mainPart = mainAndSuffix[0];
        if (mainAndSuffix.Length > 1)
            version.PreviewSuffix = mainAndSuffix[1];

        var versionParts = mainPart.Split('.');
        if (versionParts.Length != 3 && versionParts.Length != 4)
            return false;

        if (!int.TryParse(versionParts[0], out var major)) return false;
        if (!int.TryParse(versionParts[1], out var minor)) return false;
        if (!int.TryParse(versionParts[2], out var patch)) return false;

        version.Major = major;
        version.Minor = minor;
        version.Patch = patch;

        if (versionParts.Length == 4)
        {
            if (!int.TryParse(versionParts[3], out var revision)) return false;
            version.Revision = revision;
        }

        return true;
    }

    public static bool TryParse(string input, out PackageVersion version)
    {
        version = new PackageVersion();
        return TrySet(input, version);
    }

    /// <summary>
    /// Validates if a version string has a valid format
    /// </summary>
    /// <param name="version">Version string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidFormat(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Semantic version validation (Major.Minor.Patch[.Revision] with optional pre-release)
        var versionRegex = new Regex(@"^\d+(\.\d+){2,3}(-[a-zA-Z0-9\-\.]+)?$");
        return versionRegex.IsMatch(version.Trim());
    }

    /// <summary>
    /// Validates a version string and returns detailed validation result
    /// </summary>
    /// <param name="version">Version string to validate</param>
    /// <returns>Validation result with success status and error message</returns>
    public static ValidationResult Validate(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return new ValidationResult(false, "Version is required");
        }

        version = version.Trim();

        // Try to parse with PackageVersion to ensure full compatibility
        if (TryParse(version, out _))
        {
            return new ValidationResult(true, "Valid format");
        }

        // Provide specific error messages
        if (!IsValidFormat(version))
        {
            return new ValidationResult(false, "Invalid version format (e.g., 1.0.0, 1.2.3-alpha)");
        }

        return new ValidationResult(false, "Invalid version format");
    }

    public override string ToString()
    {
        var baseVersion = $"{Major}.{Minor}.{Patch}";
        if (Revision != 0)
            baseVersion += $".{Revision}";
        if (!string.IsNullOrEmpty(PreviewSuffix))
            baseVersion += "-" + PreviewSuffix;
        return baseVersion;
    }

    /// <summary>
    /// Represents the result of a version validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }
    }
}
