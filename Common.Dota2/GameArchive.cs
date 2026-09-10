using System;
using System.IO;
using ValveResourceFormat;
using ValveResourceFormat.IO;

namespace LearnOpenTK.Common.Dota2;

/// <summary>Opens a Dota 2 installation's VPK archives so compiled resources can be loaded by virtual path.</summary>
public sealed class GameArchive : IDisposable
{
    private readonly GameFileLoader _fileLoader;

    private GameArchive(GameFileLoader fileLoader)
    {
        _fileLoader = fileLoader;
    }

    /// <summary>Opens the Dota 2 installation at <paramref name="gameRootDirectory"/> (the folder containing "game\dota").</summary>
    public static GameArchive Open(string gameRootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(gameRootDirectory);
        var gameInfoPath = Path.Combine(gameRootDirectory, "game", "dota", "gameinfo.gi");
        if (!File.Exists(gameInfoPath))
        {
            throw new FileNotFoundException(
                $"Could not find gameinfo.gi under '{gameRootDirectory}'. Point [paths] game_root in config.ini at the Dota 2 installation folder (the one containing 'game\\dota').",
                gameInfoPath);
        }

        var fileLoader = new GameFileLoader(currentPackage: null, currentFileName: null);
        fileLoader.FindAndLoadSearchPaths(gameInfoPath);
        return new GameArchive(fileLoader);
    }

    public GameFileLoader FileLoader => _fileLoader;

    /// <summary>Loads a compiled resource by its source path (without the trailing "_c"), e.g. "models/heroes/antimage/antimage.vmdl".</summary>
    public Resource LoadCompiled(string sourcePath)
    {
        return _fileLoader.LoadFileCompiled(sourcePath) ?? throw new FileNotFoundException($"Resource not found: {sourcePath}{GameFileLoader.CompiledFileSuffix}");
    }

    /// <summary>Reads a plain-text game script (e.g. scripts/items/items_game.txt) that is not itself a compiled resource.</summary>
    public byte[] ReadRawFile(string virtualPath)
    {
        var (pathOnDisk, package, entry) = _fileLoader.FindFile(virtualPath, logNotFound: false);
        if (entry is not null && package is not null)
        {
            package.ReadEntry(entry, out var bytes, validateCrc: false);
            return bytes;
        }

        if (pathOnDisk is not null)
        {
            return File.ReadAllBytes(pathOnDisk);
        }

        throw new FileNotFoundException($"File not found in any VPK or on disk: {virtualPath}");
    }

    public void Dispose() => _fileLoader.Dispose();
}
