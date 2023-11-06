using AdvancedMapTool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdvancedMapParser
{
    class FileWatcher
    {

        private readonly string locPattern = "Your Location is (.*), (.*), (.*)";

        private readonly string zonePattern = "There are \\d+ players in (.*)\\.";
        private readonly string enterZonePattern = "You have entered (.*)\\.";
        private readonly int maxLoc = 20;
        private FileSystemWatcher watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
        private ParseInfo parseInfo = new ParseInfo();

        public delegate void OnParseInfo(ParseInfo parseInfo);

        public OnParseInfo OnParseInfoCallback { get; set; } = null;
        public FileWatcher()
        {
            var cwd = Directory.GetCurrentDirectory();
            Console.WriteLine($"Looking for file changes in {cwd}");

            var files = Directory.GetFiles(cwd, "eqlog_*", SearchOption.AllDirectories);

            Console.WriteLine($"Found files to watch:");
            foreach (var f in files)
            {
                Console.WriteLine(f);
            }
            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Filter = "eqlog_*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            watcher.Changed += OnChanged;
            watcher.Error += OnError;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            var name = e.Name.Split('_')[1];
            var lineReader = new ReverseLineReader(e.FullPath);
            var serialized = JsonSerializer.Serialize(parseInfo);

            parseInfo.locations.Clear();
            parseInfo.displayedName = name;
            bool zoneFound = false;
            bool enterZoneFound = false;
            foreach (var line in lineReader)
            {

                if (parseInfo.locations.Count >= maxLoc && zoneFound)
                {
                    break;
                }
                var locMatches = Regex.Matches(line, locPattern, RegexOptions.IgnoreCase);
                if (!enterZoneFound && locMatches.Count > 0 && parseInfo.locations.Count < maxLoc)
                {
                    Loc loc = new Loc
                    {
                        y = float.Parse(locMatches[0].Groups[1].Value),
                        x = float.Parse(locMatches[0].Groups[2].Value),
                        z = float.Parse(locMatches[0].Groups[3].Value)
                    };
                    if (parseInfo.locations.Any(l =>
                        l.x == loc.x && l.y == loc.y && l.z == loc.z
                    ))
                    {
                        continue;
                    }
                    parseInfo.locations.Add(loc);
                }

                string infoPattern = $"\\[(\\d+) (\\w+)\\] {name} \\((\\w+)\\)";

                var infoMatch = Regex.Match(line, infoPattern);
                if (infoMatch.Success)
                {
                    parseInfo.level = int.Parse(infoMatch.Groups[1].Value);
                    parseInfo.className = infoMatch.Groups[2].Value;
                    parseInfo.race = infoMatch.Groups[3].Value;
                }


                if (line.Contains(name) && (line.Contains("ANONYMOUS")))
                {
                    parseInfo.displayedName = line.Substring(27).Trim();
                }


                var enterZoneMatch = Regex.Match(line, enterZonePattern);
                if (!zoneFound && enterZoneMatch.Success)
                {
                    parseInfo.zoneName = enterZoneMatch.Groups[1].Value;
                    zoneFound = true;
                    enterZoneFound = true;
                }
                var zoneMatch = Regex.Match(line, zonePattern);
                if (!zoneFound && zoneMatch.Success)
                {
                    parseInfo.zoneName = zoneMatch.Groups[1].Value;
                    zoneFound = true;
                }
            }
            if (OnParseInfoCallback != null && JsonSerializer.Serialize(parseInfo) != serialized)
            {
                OnParseInfoCallback(parseInfo);
            }
        }

        private void OnError(object sender, System.IO.ErrorEventArgs e) =>
            PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }
    }
}
