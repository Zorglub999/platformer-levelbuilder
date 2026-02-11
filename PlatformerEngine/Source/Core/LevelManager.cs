using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PlatformerEngine.Source.Core
{
    public static class LevelManager
    {
        public static List<string> LevelFiles { get; private set; } = new List<string>();
        private const string LevelsDirectory = "Levels";
        private const string OrderFileName = "level_order.json";

        public static void Initialize()
        {
            if (!Directory.Exists(LevelsDirectory))
            {
                Directory.CreateDirectory(LevelsDirectory);
            }
            RefreshLevels();
        }

        public static void RefreshLevels()
        {
            if (!Directory.Exists(LevelsDirectory)) return;

            // Get all actual level files
            var allFiles = Directory.GetFiles(LevelsDirectory, "*.json")
                                   .Select(Path.GetFileName)
                                   .Where(f => f != OrderFileName)
                                   .ToList();

            // Try to load order
            List<string> orderedList = new List<string>();
            string orderPath = Path.Combine(LevelsDirectory, OrderFileName);

            if (File.Exists(orderPath))
            {
                try
                {
                    string json = File.ReadAllText(orderPath);
                    orderedList = JsonSerializer.Deserialize<List<string>>(json);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load level order: {e.Message}");
                }
            }

            // Sync: Add new files, remove missing ones, preserve order
            LevelFiles.Clear();

            // 1. Add known ordered files if they still exist
            if (orderedList != null)
            {
                foreach (var file in orderedList)
                {
                    if (allFiles.Contains(file))
                    {
                        LevelFiles.Add(file);
                        allFiles.Remove(file); // Mark as handled
                    }
                }
            }

            // 2. Add remaining (new) files at the end
            LevelFiles.AddRange(allFiles);

            // Save updated order
            SaveOrder();
        }

        private static void SaveOrder()
        {
            try
            {
                string orderPath = Path.Combine(LevelsDirectory, OrderFileName);
                string json = JsonSerializer.Serialize(LevelFiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(orderPath, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to save level order: {e.Message}");
            }
        }

        public static void MoveLevelUp(int index)
        {
            if (index > 0 && index < LevelFiles.Count)
            {
                string temp = LevelFiles[index];
                LevelFiles[index] = LevelFiles[index - 1];
                LevelFiles[index - 1] = temp;
                SaveOrder();
            }
        }

        public static void MoveLevelDown(int index)
        {
            if (index >= 0 && index < LevelFiles.Count - 1)
            {
                string temp = LevelFiles[index];
                LevelFiles[index] = LevelFiles[index + 1];
                LevelFiles[index + 1] = temp;
                SaveOrder();
            }
        }

        public static void DeleteLevel(string filename)
        {
            string path = Path.Combine(LevelsDirectory, filename);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    LevelFiles.Remove(filename);
                    SaveOrder();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to delete level {filename}: {e.Message}");
                }
            }
        }

        public static bool RenameLevel(string oldName, string newName)
        {
            // Ensure .json extension
            if (!newName.EndsWith(".json")) newName += ".json";

            string oldPath = Path.Combine(LevelsDirectory, oldName);
            string newPath = Path.Combine(LevelsDirectory, newName);

            if (File.Exists(newPath))
            {
                Console.WriteLine($"Cannot rename: {newName} already exists.");
                return false;
            }

            if (File.Exists(oldPath))
            {
                try
                {
                    File.Move(oldPath, newPath);

                    // Update list in place
                    int index = LevelFiles.IndexOf(oldName);
                    if (index != -1)
                    {
                        LevelFiles[index] = newName;
                    }
                    else
                    {
                        // Should not happen if confirmed before calling, but safe fallback
                        LevelFiles.Add(newName);
                    }
                    SaveOrder();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to rename level {oldName} to {newName}: {e.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a unique level name (e.g. level1.json, level2.json)
        /// </summary>
        public static string GetUniqueLevelName(string baseName = "new_level")
        {
            int counter = 1;
            string name = $"{baseName}_{counter}.json";
            while (File.Exists(Path.Combine(LevelsDirectory, name)))
            {
                counter++;
                name = $"{baseName}_{counter}.json";
            }
            return name;
        }
    }
}
