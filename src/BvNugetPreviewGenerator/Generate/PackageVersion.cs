using System.IO;

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
        version.Revision = 0; // Valor por defecto
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

    public override string ToString()
    {
        var baseVersion = $"{Major}.{Minor}.{Patch}";
        if (Revision != 0)
            baseVersion += $".{Revision}";
        if (!string.IsNullOrEmpty(PreviewSuffix))
            baseVersion += "-" + PreviewSuffix;
        return baseVersion;
    }
}
