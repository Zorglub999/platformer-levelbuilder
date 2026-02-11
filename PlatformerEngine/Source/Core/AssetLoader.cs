using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using System;

namespace PlatformerEngine.Source.Core;

/// <summary>
/// Handles dynamic loading of assets (textures) from the file system.
/// This allows adding new decorations without recompiling the Content Pipeline.
/// </summary>
public class AssetLoader
{
    private GraphicsDevice graphicsDevice;
    private Dictionary<string, Texture2D> decorations;

    public IReadOnlyDictionary<string, Texture2D> Decorations => decorations;

    public AssetLoader(GraphicsDevice graphicsDevice)
    {
        this.graphicsDevice = graphicsDevice;
        decorations = new Dictionary<string, Texture2D>();
    }

    /// <summary>
    /// Scans the specified directory for .png files and loads them.
    /// </summary>
    /// <param name="relativePath">Path relative to the game's executable/root.</param>
    public void LoadDecorations(string relativePath)
    {
        decorations.Clear();

        if (!Directory.Exists(relativePath))
        {
            // Try to create it if it doesn't exist
            try
            {
                Directory.CreateDirectory(relativePath);
                Console.WriteLine($"Created directory: {relativePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create directory {relativePath}: {ex.Message}");
                return;
            }
        }

        string[] files = Directory.GetFiles(relativePath, "*.png");
        Console.WriteLine($"Found {files.Length} decoration files in {relativePath}");

        foreach (string file in files)
        {
            try
            {
                using (var stream = File.OpenRead(file))
                {
                    Texture2D texture = Texture2D.FromStream(graphicsDevice, stream);
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    // Basic validation - optional, but user mentioned 64x64. 
                    // We won't enforce it strictly to allow flexibility, effectively "accepting" any size,
                    // but we store it by filename.

                    decorations[fileName] = texture;
                    Console.WriteLine($"Loaded decoration: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load texture {file}: {ex.Message}");
            }
        }
    }
}
