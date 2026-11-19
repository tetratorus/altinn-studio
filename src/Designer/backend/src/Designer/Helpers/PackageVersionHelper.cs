#nullable disable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGet.Versioning;

namespace Altinn.Studio.Designer.Helpers;

public static class PackageVersionHelper
{
    private static readonly string[] s_versionPropertyNames = ["VersionOverride", "Version"];

    public static bool TryGetPackageVersionFromCsprojFile(
        string csprojFilePath,
        IReadOnlyList<string> packageNames,
        out SemanticVersion version
    )
    {
        return TryGetPackageVersionFromCsprojXml(XDocument.Load(csprojFilePath), packageNames, out version);
    }

    public static bool TryGetPackageVersionFromCsprojContent(
        byte[] csprojContent,
        IReadOnlyList<string> packageNames,
        out SemanticVersion version
    )
    {
        using var stream = new MemoryStream(csprojContent, writable: false);
        return TryGetPackageVersionFromCsprojXml(XDocument.Load(stream), packageNames, out version);
    }

    private static bool TryGetPackageVersionFromCsprojXml(
        XDocument doc,
        IReadOnlyList<string> packageNames,
        out SemanticVersion version
    )
    {
        var packageReferences = doc.XPathSelectElements("//PackageReference")
            .Where(element => packageNames.Contains(element.Attribute("Include")?.Value));

        foreach (var packageReference in packageReferences)
        {
            foreach (string versionString in GetVersionStrings(packageReference))
            {
                if (TryParseVersion(versionString, out version))
                {
                    return true;
                }
            }
        }

        version = default;
        return false;
    }

    /// <summary>
    /// Yields the version strings declared on a package reference, in the order NuGet applies them:
    /// a <c>VersionOverride</c> takes precedence over <c>Version</c>, and each can be an attribute or a child element.
    /// </summary>
    private static IEnumerable<string> GetVersionStrings(XElement packageReference)
    {
        foreach (string name in s_versionPropertyNames)
        {
            string attributeValue = packageReference.Attribute(name)?.Value;
            if (!string.IsNullOrWhiteSpace(attributeValue))
            {
                yield return attributeValue.Trim();
            }

            foreach (XElement child in packageReference.Elements().Where(element => element.Name.LocalName == name))
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                {
                    yield return child.Value.Trim();
                }
            }
        }
    }

    /// <summary>
    /// Parses an exact version (<c>9.0.0</c>) or a NuGet version range (<c>[9.0.0]</c>, <c>[9.0,10)</c>, <c>9.*</c>).
    /// For a range, its lower bound is used, which is the lowest version a build can resolve to.
    /// </summary>
    private static bool TryParseVersion(string versionString, out SemanticVersion version)
    {
        if (SemanticVersion.TryParse(versionString, out version))
        {
            return true;
        }

        if (VersionRange.TryParse(versionString, out VersionRange range) && range.MinVersion is { } minVersion)
        {
            version = new SemanticVersion(
                minVersion.Major,
                minVersion.Minor,
                minVersion.Patch,
                minVersion.ReleaseLabels,
                minVersion.Metadata
            );
            return true;
        }

        version = default;
        return false;
    }
}
