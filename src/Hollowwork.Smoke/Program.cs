using System.Security.Cryptography;
using Hollowwork.Core.Archives;
using Hollowwork.Core.Backup;

// Read-only by default. With --apply, writes the same bytes back to a single small file
// (exercising the real write path) after a verified backup. GGPK only for now.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        usage: hollowwork-smoke <archive|bundles-dir> [--oodle <path>] [--apply --backup <dir>]

          opens a GGPK or a Bundles2 directory, lists .env files and reads the smallest one.
          --oodle  path to liboo2core*.so (Linux) or oo2core*.dll (Windows); bundles only.
          --apply  GGPK only: writes the same bytes back, flushes, reopens and compares hashes.
                   requires --backup <dir> so the archive is copied first.
        """);
    return 2;
}

var archivePath = Path.GetFullPath(args[0]);
var apply = args.Contains("--apply");
var backupRoot = Option(args, "--backup");
var oodlePath = Option(args, "--oodle");

if (apply && string.IsNullOrWhiteSpace(backupRoot))
{
    Console.Error.WriteLine("--apply requires --backup <dir>");
    return 2;
}

var kind = ArchiveProbe.DetectKind(archivePath);
Console.WriteLine($"archive : {archivePath}");
Console.WriteLine($"kind    : {kind}");

if (apply && kind != ArchiveKind.Ggpk)
{
    Console.Error.WriteLine("--apply is currently limited to GGPK archives.");
    return 2;
}

using var session = ArchiveSessionFactory.Open(archivePath, new ArchiveSessionOptions(oodlePath));

if (session is BundleArchiveSession bundle)
{
    Console.WriteLine($"oodle   : {Hollowwork.Core.Oodle.OodleLibrary.ResolvedPath ?? "(system search)"}");
    Console.WriteLine($"bundles : {bundle.BundlesDirectory}");
}

var files = session.ListFiles("metadata/environmentsettings", [".env"]);
Console.WriteLine($"env files: {files.Count}");

if (files.Count == 0)
{
    Console.Error.WriteLine("no .env file found under metadata/environmentsettings");
    return 3;
}

var targetPath = files
    .Select(path => (path, size: session.TryRead(path, out var bytes) ? bytes.Length : int.MaxValue))
    .MinBy(item => item.size)
    .path;

if (!session.TryRead(targetPath, out var original))
{
    Console.Error.WriteLine($"could not read {targetPath}");
    return 3;
}

var originalHash = Convert.ToHexStringLower(SHA256.HashData(original));
Console.WriteLine($"target  : {targetPath} ({original.Length} bytes)");
Console.WriteLine($"sha256  : {originalHash}");

if (!apply)
{
    Console.WriteLine("read-only check: OK");
    return 0;
}

var backup = ArchiveBackup.Create(archivePath, backupRoot!);
Console.WriteLine($"backup  : {backup.BackupPath}");

session.Write(targetPath, original);
session.Flush();

using (var reopened = ArchiveSessionFactory.Open(archivePath, new ArchiveSessionOptions(oodlePath)))
{
    if (!reopened.TryRead(targetPath, out var after))
    {
        Console.Error.WriteLine("reopen: target disappeared");
        return 4;
    }

    var afterHash = Convert.ToHexStringLower(SHA256.HashData(after));
    Console.WriteLine($"reopen  : {afterHash}");

    if (!string.Equals(afterHash, originalHash, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("FAIL: content changed after rewriting identical bytes");
        return 4;
    }
}

Console.WriteLine("write/reopen check: OK");
return 0;

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
