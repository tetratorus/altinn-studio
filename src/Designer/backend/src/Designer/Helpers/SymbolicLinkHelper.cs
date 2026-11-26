using System;
using System.IO;

namespace Altinn.Studio.Designer.Helpers;

/// <summary>
/// Helper methods for detecting symbolic links and other reparse points inside repository directories.
/// </summary>
public static class SymbolicLinkHelper
{
    /// <summary>
    /// Returns true if the entry is a symbolic link or another kind of reparse point.
    /// </summary>
    public static bool IsSymbolicLink(FileSystemInfo entry) =>
        entry.LinkTarget is not null || (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint));

    /// <summary>
    /// Returns true if the path is outside the root directory, or if the path or any of its
    /// parent directories below the root directory is a symbolic link.
    /// </summary>
    /// <param name="rootDirectory">The trusted root directory, e.g. the local clone of a repository.</param>
    /// <param name="path">Absolute path, or path relative to the root directory, to check.</param>
    public static bool IsOutsideOrTraversesSymbolicLink(string rootDirectory, string path)
    {
        string fullRoot = Path.GetFullPath(rootDirectory);
        string fullPath = Path.GetFullPath(path, fullRoot);
        string relativePath = Path.GetRelativePath(fullRoot, fullPath);

        if (relativePath == ".")
        {
            return false;
        }

        if (
            Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
        )
        {
            return true;
        }

        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );
        string currentPath = fullRoot;
        foreach (string segment in segments)
        {
            currentPath = Path.Join(currentPath, segment);
            FileSystemInfo entry = Directory.Exists(currentPath)
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);
            if (IsSymbolicLink(entry))
            {
                return true;
            }
        }

        return false;
    }
}
