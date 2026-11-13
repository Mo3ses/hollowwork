using System.Security.Cryptography;
using System.Text;
using LibGGPK3.Records;

namespace Hollowwork.Core.Tests;

/// <summary>
/// Builds a minimal, valid version-3 GGPK (header + directory tree + file data)
/// so the real LibGGPK3 read/write path can be exercised without a game install.
/// </summary>
internal static class GgpkFixture
{
    private const uint HeaderTag = 0x4B504747;    // GGPK
    private const uint DirectoryTag = 0x52494450; // PDIR
    private const uint FileTag = 0x454C4946;      // FILE

    public static void Create(string path, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = new DirectoryNode(string.Empty);
        foreach (var (filePath, content) in files)
        {
            var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                current = current.GetOrAddDirectory(parts[i]);
            }

            current.AddFile(parts[^1], content);
        }

        root.ComputeLength();
        long offset = 28;
        root.AssignOffset(ref offset);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        writer.Write(28u);
        writer.Write(HeaderTag);
        writer.Write(3u);
        writer.Write(root.Offset);
        writer.Write(0L);

        WriteDirectory(writer, root);
    }

    private static void WriteDirectory(BinaryWriter writer, DirectoryNode directory)
    {
        var entries = directory.Entries();
        writer.Write(directory.Length);
        writer.Write(DirectoryTag);
        writer.Write(directory.Name.Length + 1);
        writer.Write(entries.Count);
        writer.Write(directory.Hash);

        WriteUtf16(writer, directory.Name);
        writer.Write((ushort)0);

        foreach (var entry in entries)
        {
            writer.Write(TreeNode.GetNameHash(entry.Name));
            writer.Write(entry.Offset);
        }

        foreach (var entry in entries)
        {
            if (entry is DirectoryNode child)
            {
                WriteDirectory(writer, child);
            }
            else
            {
                WriteFile(writer, (FileNode)entry);
            }
        }
    }

    private static void WriteFile(BinaryWriter writer, FileNode file)
    {
        writer.Write(file.Length);
        writer.Write(FileTag);
        writer.Write(file.Name.Length + 1);
        writer.Write(file.Hash);
        WriteUtf16(writer, file.Name);
        writer.Write((ushort)0);
        writer.Write(file.Content);
    }

    private static void WriteUtf16(BinaryWriter writer, string value)
    {
        foreach (var c in value)
        {
            writer.Write((ushort)c);
        }
    }

    private abstract class Node(string name)
    {
        public string Name { get; } = name;

        public long Offset { get; set; }

        public uint Length { get; protected set; }

        public byte[] Hash { get; protected set; } = new byte[32];

        public abstract void ComputeLength();
    }

    private sealed class DirectoryNode(string name) : Node(name)
    {
        public Dictionary<string, DirectoryNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, FileNode> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public DirectoryNode GetOrAddDirectory(string childName)
        {
            if (!Directories.TryGetValue(childName, out var child))
            {
                child = new DirectoryNode(childName);
                Directories.Add(childName, child);
            }

            return child;
        }

        public void AddFile(string fileName, byte[] content) => Files[fileName] = new FileNode(fileName, content);

        public List<Node> Entries()
        {
            var entries = new List<Node>(Directories.Count + Files.Count);
            entries.AddRange(Directories.Values);
            entries.AddRange(Files.Values);
            return entries.OrderBy(node => TreeNode.GetNameHash(node.Name)).ToList();
        }

        public override void ComputeLength()
        {
            foreach (var directory in Directories.Values)
            {
                directory.ComputeLength();
            }

            foreach (var file in Files.Values)
            {
                file.ComputeLength();
            }

            var entries = Entries();
            Length = (uint)(16 + 32 + 2 * (Name.Length + 1) + 12 * entries.Count);

            var combination = new byte[32 * entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i].Hash.CopyTo(combination, i * 32);
            }

            Hash = SHA256.HashData(combination);
        }

        public void AssignOffset(ref long offset)
        {
            Offset = offset;
            offset += Length;

            foreach (var entry in Entries())
            {
                switch (entry)
                {
                    case DirectoryNode directory:
                        directory.AssignOffset(ref offset);
                        break;
                    case FileNode file:
                        file.Offset = offset;
                        offset += file.Length;
                        break;
                }
            }
        }
    }

    private sealed class FileNode(string name, byte[] content) : Node(name)
    {
        public byte[] Content { get; } = content;

        public override void ComputeLength()
        {
            Length = (uint)(12 + 32 + 2 * (Name.Length + 1) + Content.Length);
            Hash = SHA256.HashData(Content);
        }
    }
}
