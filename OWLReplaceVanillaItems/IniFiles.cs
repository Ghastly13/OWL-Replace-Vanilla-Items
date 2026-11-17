using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OWLReplaceVanillaItems
{
    // The SkyPatcher INIs: one per plugin and record type, named after the plugin so it loads only with it, in a folder of
    // the patcher's own under Open World Loot's: SKSE\Plugins\SkyPatcher\<type>\OWL\Patches\OWL Replace Vanilla Items\.
    // They are rewritten on every run, and those of plugins no longer patched are removed. Nothing else is touched: the
    // leveledList INIs of the xEdit script OWL_Add_Items_To_Lists.pas, in OWL\Patches\<plugin>\, stay as they are.
    public sealed class IniFiles
    {
        public const string OwnFolder = "OWL Replace Vanilla Items";
        public const string Marker = "Written by the Synthesis patcher OWL Replace Vanilla Items";
        private const string XEditReplace = "OWL_Replace_Vanilla_Items.pas";
        public static readonly string[] TypeFolders = { "leveledList", "container", "outfit", "npc" };

        private readonly string _skyPatcher;

        public string SkyPatcherFolder => _skyPatcher;
        // The INIs in Open World Loot's folders, from Open World Loot or patches for other mods (ExistingPatches reads what
        // they do): every one but this patcher's own and those of the xEdit script OWL_Replace_Vanilla_Items.pas
        public List<(string Folder, string Path)> OtherInis { get; } = new();
        // Plugin file name -> INIs the xEdit script OWL_Replace_Vanilla_Items.pas wrote for it, which this patcher replaces
        public Dictionary<string, List<string>> XEditReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IniFiles(string dataPath)
        {
            _skyPatcher = Path.Combine(dataPath, "SKSE", "Plugins", "SkyPatcher");
            foreach (var folder in TypeFolders)
            {
                string owl = Path.Combine(_skyPatcher, folder, "OWL");
                if (!Directory.Exists(owl)) continue;
                string own = OwnDirectory(folder) + Path.DirectorySeparatorChar;
                foreach (var file in Directory.EnumerateFiles(owl, "*.ini", SearchOption.AllDirectories))
                {
                    if (file.StartsWith(own, StringComparison.OrdinalIgnoreCase)) continue;
                    string plugin = Path.GetFileNameWithoutExtension(file);
                    if (IsPluginName(plugin) && ReadOrEmpty(file).Contains(XEditReplace, StringComparison.Ordinal)) Add(XEditReplacements, plugin, file);
                    else OtherInis.Add((folder, file));
                }
            }
        }

        public static bool IsPluginName(string name)
        {
            string ext = Path.GetExtension(name);
            return ext.Equals(".esp", StringComparison.OrdinalIgnoreCase) || ext.Equals(".esm", StringComparison.OrdinalIgnoreCase) || ext.Equals(".esl", StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(Dictionary<string, List<string>> map, string plugin, string file)
        {
            if (!map.TryGetValue(plugin, out var files)) map[plugin] = files = new List<string>();
            files.Add(file);
        }

        private static string ReadOrEmpty(string file)
        {
            try { return File.ReadAllText(file); }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        private string OwnDirectory(string folder) => Path.Combine(_skyPatcher, folder, "OWL", "Patches", OwnFolder);

        public string PathFor(string folder, string plugin) => Path.Combine(OwnDirectory(folder), plugin + ".ini");

        // Writes the INI when its text changed (changed says whether it did) and returns its path
        public string Write(string folder, string plugin, IEnumerable<string> body, out bool changed)
        {
            string path = PathFor(folder, plugin);
            var lines = new List<string>
            {
                "; Open World Loot: vanilla items in " + plugin + "'s records replaced by the OWL lists that hand them out.",
                "; " + Marker + ", rewritten on every run. Loads only with " + plugin + " active.",
            };
            lines.AddRange(body);
            string text = string.Join("\r\n", lines) + "\r\n";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            changed = !File.Exists(path) || ReadOrEmpty(path) != text;
            if (changed) File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        // Removes this patcher's INIs that were not written this run (plugins no longer patched), and returns them
        public List<string> RemoveStale(ICollection<string> written)
        {
            var keep = new HashSet<string>(written.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            var removed = new List<string>();
            foreach (var folder in TypeFolders)
            {
                string dir = OwnDirectory(folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.ini", SearchOption.TopDirectoryOnly).ToList())
                {
                    if (keep.Contains(Path.GetFullPath(file)) || !ReadOrEmpty(file).Contains(Marker, StringComparison.Ordinal)) continue;
                    File.Delete(file);
                    removed.Add(file);
                }
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            return removed;
        }
    }
}
