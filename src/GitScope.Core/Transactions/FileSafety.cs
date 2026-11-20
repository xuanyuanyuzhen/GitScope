using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace GitScope.Core;

internal static partial class FileSafety
{
    private const uint BackupSemantics = 0x02000000, OpenReparsePoint = 0x00200000;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleInfo info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleInfo
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || TryAttributes(path, out _);
    public static bool TryAttributes(string path, out FileAttributes attrs)
    {
        try { attrs = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { attrs = default; return false; }
        catch (DirectoryNotFoundException) { attrs = default; return false; }
    }
    public static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ScopeException(ErrorCode.UnsafeTarget, "请选择专用输出目录。");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || !Regex.IsMatch(full, @"^[A-Za-z]:\\"))
            throw new ScopeException(ErrorCode.UnsafeTarget, "首版仅支持本机磁盘目录，不支持网络共享或设备路径。");
        if (Directory.Exists(full))
        {
            using var handle = Open(full);
            var text = new StringBuilder(32768);
            uint n = GetFinalPathNameByHandleW(handle, text, (uint)text.Capacity, 0);
            if (n == 0 || n >= text.Capacity) throw new IOException("无法解析输出目录的最终路径。", new Win32Exception(Marshal.GetLastWin32Error()));
            var final = text.ToString();
            if (final.StartsWith(@"\\?\", StringComparison.Ordinal)) final = final[4..];
            // Check all original components first: a resolved path must not hide a junction.
            NoReparseAncestors(full);
            full = Path.TrimEndingDirectorySeparator(final);
        }
        return full;
    }
    public static SafeFileHandle Open(string path, bool blockRename = false)
    {
        var h = CreateFileW(path, blockRename ? 1u : 0u, blockRename ? 3u : 7u,
            IntPtr.Zero, 3, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); throw new IOException($"无法读取文件身份：{path}", new Win32Exception(Marshal.GetLastWin32Error())); }
        return h;
    }
    public static string Identity(string path)
    {
        using var h = Open(path);
        if (!GetFileInformationByHandle(h, out var i)) throw new IOException("无法读取目录身份。", new Win32Exception(Marshal.GetLastWin32Error()));
        return $"{i.Volume:x8}:{i.IndexHigh:x8}{i.IndexLow:x8}:{i.CreationHigh:x8}{i.CreationLow:x8}";
    }
    public static string? IdentityOrNull(string path) => Exists(path) ? Identity(path) : null;
    public static bool Related(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        || a.StartsWith(b.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || b.StartsWith(a.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static void NoReparseAncestors(string path)
    {
        string? part = Path.GetFullPath(path);
        while (part is not null)
        {
            if (TryAttributes(part, out var attrs) && attrs.HasFlag(FileAttributes.ReparsePoint))
                throw new ScopeException(ErrorCode.UnsafeTarget, $"目录或父目录是链接，无法安全替换：{part}");
            part = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(part));
        }
    }
    public static IDisposable HoldParents(string path)
    {
        var handles = new List<SafeFileHandle>();
        try
        {
            string? parent = Path.GetDirectoryName(path);
            while (parent is not null)
            {
                NoReparseAncestors(parent);
                handles.Add(Open(parent, true));
                parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(parent));
            }
            return new HandleSet(handles);
        }
        catch { foreach (var h in handles) h.Dispose(); throw; }
    }
    private sealed class HandleSet(List<SafeFileHandle> handles) : IDisposable
    {
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); }
    }
    public static string Relative(string input, ErrorCode code = ErrorCode.UnsafeTarget)
    {
        var path = input.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || Path.IsPathRooted(path))
            throw new ScopeException(code, $"只接受精确的相对路径：{input}");
        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.')
                || part.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || part.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || ReservedName().IsMatch(part.Split('.')[0]))
                throw new ScopeException(code, $"Windows 无法安全映射此路径：{input}");
        }
        return path;
    }
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedName();
    public static bool RelativeRelated(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase)
        || a.StartsWith(b + "/", StringComparison.OrdinalIgnoreCase) || b.StartsWith(a + "/", StringComparison.OrdinalIgnoreCase);
    public static string Child(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, Relative(relative).Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ScopeException(ErrorCode.UnsafeTarget, "路径超出输出目录。");
        return path;
    }
    public static void Move(string source, string destination, string identity)
    {
        NoReparseAncestors(source);
        NoReparseAncestors(Path.GetDirectoryName(destination)!);
        if (IdentityOrNull(source) != identity || Exists(destination))
            throw new ScopeException(ErrorCode.TargetChanged, "移动前发现内容身份变化或目标位置已被占用，保留现场等待恢复。");
        if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination);
        if (Identity(destination) != identity) throw new ScopeException(ErrorCode.TargetChanged, "移动后的内容身份不符。");
    }
    public static string Fingerprint(string root, CancellationToken token = default)
    {
        NoReparseAncestors(root);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string text) => hash.AppendData(Encoding.UTF8.GetBytes(text));
        void Walk(string path, string relative)
        {
            token.ThrowIfCancellationRequested();
            var attrs = File.GetAttributes(path);
            if (attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                FileSystemInfo info = attrs.HasFlag(FileAttributes.Directory) ? new DirectoryInfo(path) : new FileInfo(path);
                Add($"{relative}\0L\0{(int)attrs}\0{info.LinkTarget}\0");
            }
            else if (attrs.HasFlag(FileAttributes.Directory))
            {
                using var stable = Open(path, true);
                NoReparseAncestors(path);
                Add($"{relative}\0D\0");
                foreach (var child in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal))
                    Walk(child, relative.Length == 0 ? Path.GetFileName(child) : relative + "/" + Path.GetFileName(child));
            }
            else
            {
                using var stable = Open(path, true);
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new ScopeException(ErrorCode.TargetChanged, "文件在读取前被替换为链接。");
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Add($"{relative}\0F\0{stream.Length}\0");
                var buffer = new byte[65536]; int count;
                while ((count = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
                Add("\0");
            }
        }
        Walk(root, "");
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static void DeleteOwnedTree(string root, string identity, string expectedParent)
    {
        if (!Exists(root)) return;
        if (!Path.GetDirectoryName(root)!.Equals(expectedParent, StringComparison.OrdinalIgnoreCase))
            throw new ScopeException(ErrorCode.UnsafeTarget, "拒绝清理事务父目录之外的路径。");
        NoReparseAncestors(root);
        if (Identity(root) != identity) throw new ScopeException(ErrorCode.TargetChanged, "待清理目录身份变化，保留现场。");
        void Remove(string path)
        {
            var attrs = File.GetAttributes(path);
            if (attrs.HasFlag(FileAttributes.ReparsePoint))
            {
                if (attrs.HasFlag(FileAttributes.Directory)) Directory.Delete(path); else File.Delete(path);
                return;
            }
            if (attrs.HasFlag(FileAttributes.Directory))
            {
                var expected = Identity(path);
                using (var stable = Open(path, true))
                {
                    NoReparseAncestors(path);
                    if (Identity(path) != expected) throw new ScopeException(ErrorCode.TargetChanged, "清理中目录身份发生变化。");
                    foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                    {
                        NoReparseAncestors(path);
                        if (Identity(path) != expected) throw new ScopeException(ErrorCode.TargetChanged, "清理中目录身份发生变化。");
                        Remove(entry);
                    }
                }
                if (Identity(path) != expected) throw new ScopeException(ErrorCode.TargetChanged, "清理中目录身份发生变化。");
                Directory.Delete(path);
            }
            else
            {
                using (var stable = Open(path, true))
                {
                    var currentAttributes = File.GetAttributes(path);
                    if (currentAttributes.HasFlag(FileAttributes.ReparsePoint))
                        throw new ScopeException(ErrorCode.TargetChanged, "清理中的文件被替换为链接，保留现场。");
                    if (currentAttributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, currentAttributes & ~FileAttributes.ReadOnly);
                }
                File.Delete(path);
            }
        }
        Remove(root);
    }
    public static string Key(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant()))).ToLowerInvariant();
}
