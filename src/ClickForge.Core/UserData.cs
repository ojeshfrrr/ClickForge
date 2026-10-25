using System.Collections.Concurrent;
using System.Text.Json;

namespace ClickForge.Core;

// Shared per-path lock across service instances. Unique temporary files and atomic
// replacement prevent partial readers and conflicting saves within the application.
public static class UserData
{
    private static readonly ConcurrentDictionary<string, object> locks = new(StringComparer.OrdinalIgnoreCase);
    public static object Gate(string path) => locks.GetOrAdd(Path.GetFullPath(path), _ => new());
    public static void Write<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        lock (Gate(path))
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { JsonSerializer.Serialize(file, value, options); file.Flush(true); }
                if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
