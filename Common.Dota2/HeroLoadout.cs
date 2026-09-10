using System;
using System.Collections.Generic;
using System.IO;
using ValveKeyValue;

namespace LearnOpenTK.Common.Dota2;

/// <summary>One item from a hero's default loadout: enough to load and place its compiled model.</summary>
public sealed record LoadoutItem(string Name, string? Slot, string ModelPlayerPath);

/// <summary>
/// Resolves which wearables (weapon, head, etc.) a hero equips by default, by reading
/// scripts/items/items_game.txt the same way the game does: a name-based lookup, not an asset
/// reference stored on the hero model itself.
/// </summary>
public static class HeroLoadout
{
    private const string ItemsGamePath = "scripts/items/items_game.txt";

    /// <summary>Maps "models/heroes/antimage/antimage.vmdl" to "npc_dota_hero_antimage", or null if the path has no "heroes" segment.</summary>
    public static string? HeroNpcNameFromModelPath(string modelPath)
    {
        var segments = modelPath.Replace('\\', '/').ToLowerInvariant().Split('/');
        var heroesIndex = Array.IndexOf(segments, "heroes");
        if (heroesIndex < 0 || heroesIndex + 1 >= segments.Length) return null;
        return $"npc_dota_hero_{segments[heroesIndex + 1]}";
    }

    /// <summary>The hero's default, non-persona items that carry a player model (i.e. are worn on the body).</summary>
    public static IReadOnlyList<LoadoutItem> LoadDefaultLoadout(GameArchive archive, string heroNpcName)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var bytes = archive.ReadRawFile(ItemsGamePath);
        using var stream = new MemoryStream(bytes);
        KVObject root = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);

        if (!root.TryGetValue("items", out var itemsNode)) return [];

        var results = new List<LoadoutItem>();
        foreach (var (itemId, item) in itemsNode.Children)
        {
            if (!item.TryGetValue("prefab", out var prefabValue) || prefabValue.ToString() != "default_item") continue;
            if (!UsesHero(item, heroNpcName)) continue;
            if (!item.TryGetValue("model_player", out var modelPlayerValue)) continue;

            var slot = ResolveSlot(root, item);
            if (IsPersonaVariant(slot)) continue;

            var name = item.TryGetValue("name", out var nameValue) ? nameValue.ToString() ?? itemId : itemId;
            results.Add(new LoadoutItem(name, slot, modelPlayerValue.ToString() ?? string.Empty));
        }

        return results;
    }

    private static bool UsesHero(KVObject item, string heroNpcName)
    {
        if (!item.TryGetValue("used_by_heroes", out var usedByHeroes)) return false;
        foreach (var (heroKey, _) in usedByHeroes.Children)
        {
            if (string.Equals(heroKey, heroNpcName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>An item's own item_slot if present, else the item_slot declared by the prefab it references.</summary>
    private static string? ResolveSlot(KVObject root, KVObject item)
    {
        if (item.TryGetValue("item_slot", out var slotValue)) return slotValue.ToString();
        if (!item.TryGetValue("prefab", out var prefabRef)) return null;
        if (!root.TryGetValue("prefabs", out var prefabsNode)) return null;
        if (prefabsNode.TryGetValue(prefabRef.ToString() ?? string.Empty, out var prefab) && prefab.TryGetValue("item_slot", out var prefabSlot))
        {
            return prefabSlot.ToString();
        }

        return null;
    }

    private static bool IsPersonaVariant(string? slot) => slot is not null && slot.Contains("_persona_", StringComparison.Ordinal);
}
