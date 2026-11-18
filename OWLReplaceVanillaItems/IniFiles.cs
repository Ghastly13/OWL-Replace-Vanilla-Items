using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OWLReplaceVanillaItems
{
    // The SkyPatcher INIs: in a folder of the patcher's own under Open World Loot's,
    // SKSE\Plugins\SkyPatcher\<type>\OWL\Patches\OWL Replace Vanilla Items\:
    //   <plugin>.ini      the replacements in a plugin's records, loading only with the plugin
    //   <source INI name> the replacements of items another mod's SkyPatcher INI adds, named as that INI so it loads exactly
    //                     when the source does, and nested ("zz" folders) deeper than it so SkyPatcher applies it after
    // They are rewritten on every run, and those no longer written are removed. Nothing else is touched: the leveledList
    // INIs of the xEdit script OWL_Add_Items_To_Lists.pas, in OWL\Patches\<plugin>\, stay as they are.
    public sealed class IniFiles
    {
        public const string OwnFolder = "OWL Replace Vanilla Items";
        public const string Marker = "Written by the Synthesis patcher OWL Replace Vanilla Items";
        private const string XEditReplace = "OWL_Replace_Vanilla_Items.pas";
        public static readonly string[] TypeFolders = { "leveledList", "container", "outfit", "npc" };
        // How deep this patcher's own INIs are in their type folder: OWL\Patches\OWL Replace Vanilla Items\<file>
        public const int OwnDepth = 3;

        public sealed record Ini(string Folder, string Path, string Relative, bool InOwlFolder)
        {
            // Folders between the type folder and the file: SkyPatcher applies shallower INIs first
            public int Depth => Relative.Count(c => c == System.IO.Path.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar);
        }

        private readonly string _skyPatcher;

        public string SkyPatcherFolder => _skyPatcher;
        // Every SkyPatcher INI of the four record types but this patcher's own and those of the xEdit script
        // OWL_Replace_Vanilla_Items.pas: Open World Loot's (in its OWL folders, its patches for other mods included) and
        // other mods'. ExistingPatches reads what they do, and what other mods' add
        public List<Ini> Inis { get; } = new();
        // Plugin file name -> INIs the xEdit script OWL_Replace_Vanilla_Items.pas wrote for it, which this patcher replaces
        public Dictionary<string, List<string>> XEditReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IniFiles(string dataPath)
        {
            _skyPatcher = Path.Combine(dataPath, "SKSE", "Plugins", "SkyPatcher");
            foreach (var folder in TypeFolders)
            {
                string typeFolder = Path.Combine(_skyPatcher, folder);
                if (!Directory.Exists(typeFolder)) continue;
                string own = OwnDirectory(folder) + Path.DirectorySeparatorChar;
                string owl = Path.Combine(typeFolder, "OWL") + Path.DirectorySeparatorChar;
                foreach (var file in Directory.EnumerateFiles(typeFolder, "*.ini", SearchOption.AllDirectories))
                {
                    if (file.StartsWith(own, StringComparison.OrdinalIgnoreCase)) continue;
                    string plugin = Path.GetFileNameWithoutExtension(file);
                    if (IsPluginName(plugin) && ReadOrEmpty(file).Contains(XEditReplace, StringComparison.Ordinal)) { Add(XEditReplacements, plugin, file); continue; }
                    Inis.Add(new Ini(folder, file, Path.GetRelativePath(typeFolder, file), file.StartsWith(owl, StringComparison.OrdinalIgnoreCase)));
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

        // Whether SkyPatcher applies INI a after INI b, both given relative to the same folder: shallower INIs first, then
        // by path
        public static bool AppliesAfter(string a, string b)
        {
            static int Depth(string p) => p.Count(c => c == System.IO.Path.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar);
            int depth = Depth(a).CompareTo(Depth(b));
            return depth != 0 ? depth > 0 : string.Compare(a, b, StringComparison.OrdinalIgnoreCase) > 0;
        }

        // The file, inside this patcher's folder, for the replacements of what a source INI adds: named as the source and
        // nested in "zz" folders one level deeper than it, so SkyPatcher applies it after the source
        public static string FileFor(Ini source)
        {
            string name = Path.GetFileName(source.Path);
            int extra = Math.Max(0, source.Depth + 1 - OwnDepth);
            return Path.Combine(Enumerable.Repeat("zz", extra).Append(name).ToArray());
        }

        // Writes the INI when its text changed (changed says whether it did) and returns its path. file is the path
        // inside this patcher's folder; what says whose items these are ("in X.esp's records", "<INI> adds to records")
        public string Write(string folder, string file, string what, IEnumerable<string> body, out bool changed)
        {
            string path = Path.Combine(OwnDirectory(folder), file);
            string gate = Path.GetFileNameWithoutExtension(file);
            var lines = new List<string>
            {
                "; Open World Loot: the vanilla items " + what + ", replaced by the OWL lists that hand them out.",
                "; " + Marker + ", rewritten on every run. " + (IsPluginName(gate) ? "Loads only with " + gate + " active." : "Loads whenever SkyPatcher does."),
            };
            lines.AddRange(body);
            string text = string.Join("\r\n", lines) + "\r\n";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            changed = !File.Exists(path) || ReadOrEmpty(path) != text;
            if (changed) File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        // Removes this patcher's INIs that were not written this run, and the "zz" folders left empty, and returns them
        public List<string> RemoveStale(ICollection<string> written)
        {
            var keep = new HashSet<string>(written.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            var removed = new List<string>();
            foreach (var folder in TypeFolders)
            {
                string dir = OwnDirectory(folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.ini", SearchOption.AllDirectories).ToList())
                {
                    if (keep.Contains(Path.GetFullPath(file)) || !ReadOrEmpty(file).Contains(Marker, StringComparison.Ordinal)) continue;
                    File.Delete(file);
                    removed.Add(file);
                }
                foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList())
                    if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub);
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            return removed;
        }
    }
}
