using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CopilotLedger.Tests.TestSupport;

// Keeps this process out of a folder until it's disposed, with in-box APIs only: on Unix, mode 000; on Windows, ACEs that deny
// this user listing the folder and reading its files' data and attributes. Enforced is false when the denial doesn't bind,
// which happens only for root on Unix, whom permission bits don't restrict.
public sealed class DeniedDirectory : IDisposable
{
    private readonly string _path;
    private readonly string[] _files;
    private readonly UnixFileMode _mode;

    public DeniedDirectory(string path)
    {
        _path = path;
        _files = Directory.GetFiles(path);
        if (OperatingSystem.IsWindows())
        {
            Deny(path, _files);
        }
        else
        {
            _mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, UnixFileMode.None);
        }

        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (UnauthorizedAccessException)
        {
            Enforced = true;
        }
    }

    // Whether this process can no longer list the folder.
    public bool Enforced { get; }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            Allow(_path, _files);
        }
        else
        {
            File.SetUnixFileMode(_path, _mode);
        }
    }

    // The files' deny rules first, then the folder's, which would keep the files out of reach.
    [SupportedOSPlatform("windows")]
    private static void Deny(string path, string[] files)
    {
        (FileSystemAccessRule folderRule, FileSystemAccessRule fileRule) = Rules();
        foreach (string file in files)
        {
            FileInfo info = new(file);
            FileSecurity security = info.GetAccessControl();
            security.AddAccessRule(fileRule);
            info.SetAccessControl(security);
        }

        DirectoryInfo folder = new(path);
        DirectorySecurity folderSecurity = folder.GetAccessControl();
        folderSecurity.AddAccessRule(folderRule);
        folder.SetAccessControl(folderSecurity);
    }

    // The folder's rule first, then the files'. A security object persists only the rules changed in it, so the rules are
    // removed from fresh copies, never undone by setting the old copies again.
    [SupportedOSPlatform("windows")]
    private static void Allow(string path, string[] files)
    {
        (FileSystemAccessRule folderRule, FileSystemAccessRule fileRule) = Rules();
        DirectoryInfo folder = new(path);
        DirectorySecurity folderSecurity = folder.GetAccessControl();
        folderSecurity.RemoveAccessRule(folderRule);
        folder.SetAccessControl(folderSecurity);
        foreach (string file in files)
        {
            FileInfo info = new(file);
            FileSecurity security = info.GetAccessControl();
            security.RemoveAccessRule(fileRule);
            info.SetAccessControl(security);
        }
    }

    [SupportedOSPlatform("windows")]
    private static (FileSystemAccessRule Folder, FileSystemAccessRule File) Rules()
    {
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User!;
        return (
            new FileSystemAccessRule(
                user,
                FileSystemRights.ListDirectory | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes,
                AccessControlType.Deny),
            new FileSystemAccessRule(
                user,
                FileSystemRights.ReadData | FileSystemRights.ReadAttributes | FileSystemRights.ReadExtendedAttributes,
                AccessControlType.Deny));
    }
}
