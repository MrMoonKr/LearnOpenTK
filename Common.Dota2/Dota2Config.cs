using System;
using System.Collections.Generic;
using System.IO;

namespace LearnOpenTK.Common.Dota2;

/// <summary>The handful of settings each example project reads from its own config.ini (see config.sample.ini).</summary>
public sealed class Dota2Config
{
    public required string GameRoot { get; init; }
    public required string ModelPath { get; init; }
    public string? AnimationClip { get; init; }

    /// <summary>Only used by the GPU-instancing example: the instance grid's row/column count and world-unit spacing.</summary>
    public int? InstanceRows { get; init; }
    public int? InstanceColumns { get; init; }
    public float? InstanceSpacing { get; init; }

    /// <summary>Only used by 14-GpuPbr: how many different heroes to place, how far apart, and (optionally) a fixed random seed so the same selection can be reproduced.</summary>
    public int? CharacterCount { get; init; }
    public float? CharacterSpacing { get; init; }
    public int? CharacterSeed { get; init; }

    /// <summary>Parses a minimal "[section]" / "key = value" INI file. ";" starts a comment.</summary>
    public static Dota2Config Load(string path)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[line[1..^1].Trim()] = currentSection;
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0) continue;
            currentSection[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        var gameRoot = sections["paths"]["game_root"];
        var modelPath = sections["model"]["path"];
        var animationClip = sections.TryGetValue("animation", out var animationSection) && animationSection.TryGetValue("clip", out var clip) ? clip : null;

        sections.TryGetValue("instancing", out var instancingSection);
        var rows = instancingSection is not null && instancingSection.TryGetValue("rows", out var rowsText) && int.TryParse(rowsText, out var rowsValue) ? rowsValue : (int?)null;
        var columns = instancingSection is not null && instancingSection.TryGetValue("columns", out var columnsText) && int.TryParse(columnsText, out var columnsValue) ? columnsValue : (int?)null;
        var spacing = instancingSection is not null && instancingSection.TryGetValue("spacing", out var spacingText) && float.TryParse(spacingText, out var spacingValue) ? spacingValue : (float?)null;

        sections.TryGetValue("characters", out var charactersSection);
        var characterCount = charactersSection is not null && charactersSection.TryGetValue("count", out var countText) && int.TryParse(countText, out var countValue) ? countValue : (int?)null;
        var characterSpacing = charactersSection is not null && charactersSection.TryGetValue("spacing", out var characterSpacingText) && float.TryParse(characterSpacingText, out var characterSpacingValue) ? characterSpacingValue : (float?)null;
        var characterSeed = charactersSection is not null && charactersSection.TryGetValue("seed", out var seedText) && int.TryParse(seedText, out var seedValue) ? seedValue : (int?)null;

        return new Dota2Config
        {
            GameRoot = gameRoot,
            ModelPath = modelPath,
            AnimationClip = animationClip,
            InstanceRows = rows,
            InstanceColumns = columns,
            InstanceSpacing = spacing,
            CharacterCount = characterCount,
            CharacterSpacing = characterSpacing,
            CharacterSeed = characterSeed,
        };
    }
}
