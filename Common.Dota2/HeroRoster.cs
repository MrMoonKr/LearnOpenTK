using System;
using System.Collections.Generic;
using System.IO;
using ValveKeyValue;

namespace LearnOpenTK.Common.Dota2;

/// <summary>One playable hero as declared in scripts/npc/npc_heroes.txt.</summary>
public sealed record HeroRosterEntry(string NpcName, string ModelPath);

/// <summary>
/// The real hero roster, read from the game's own data instead of a hardcoded name list: several
/// internal model folder names do not match the hero's public name (e.g. "bloodseeker" is actually
/// "blood_seeker", "drow_ranger" is "drow", "nevermore" is "shadow_fiend"), so guessing paths from
/// hero names is unreliable.
/// </summary>
public static class HeroRoster
{
    private const string NpcHeroesPath = "scripts/npc/npc_heroes.txt";

    /// <summary>Every real, selectable hero (excludes the "npc_dota_hero_base" template entry).</summary>
    public static IReadOnlyList<HeroRosterEntry> LoadAll(GameArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var bytes = archive.ReadRawFile(NpcHeroesPath);
        using var stream = new MemoryStream(bytes);
        KVObject root = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);

        var heroes = new List<HeroRosterEntry>();
        foreach (var (npcName, npc) in root.Children)
        {
            if (!npcName.StartsWith("npc_dota_hero_", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(npcName, "npc_dota_hero_base", StringComparison.OrdinalIgnoreCase)) continue;
            if (!npc.TryGetValue("Model", out var modelValue)) continue;
            var modelPath = modelValue.ToString();
            if (string.IsNullOrEmpty(modelPath) || modelPath.StartsWith("models/dev/", StringComparison.OrdinalIgnoreCase)) continue;
            heroes.Add(new HeroRosterEntry(npcName, modelPath));
        }

        return heroes;
    }
}
