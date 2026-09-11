using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LearnOpenTK.Common.Animation;

namespace LearnOpenTK.Common.Dota2;

/// <summary>
/// A hero's animation clips grouped by inferred motion state. A hero can carry several idle/attack
/// variants (e.g. per weapon slot or idle_rare), hence lists rather than a single clip name; each list
/// is ordered by descending acttable <see cref="HeroAnimationActivity.Weight"/> (clips with no weight
/// data, i.e. classified only via the clip-name keyword fallback, sort last), then alphabetically as a
/// deterministic tiebreak, so PrimaryXClip prefers the clip the game itself would pick most often over
/// a rare/alternate variant (e.g. "idle" over a low-weight "idle_rare") instead of an arbitrary
/// alphabetical guess. Dota 2 has no jump mechanic, so JumpClips/FallClips are empty for most heroes -
/// callers should fall back to Idle/Run clips while airborne (see 15-CharacterController's PlayClipForState).
/// </summary>
public sealed record HeroAnimationClassification(
    IReadOnlyList<string> IdleClips,
    IReadOnlyList<string> RunClips,
    IReadOnlyList<string> AttackClips,
    IReadOnlyList<string> JumpClips,
    IReadOnlyList<string> FallClips)
{
    public string? PrimaryIdleClip => IdleClips.Count > 0 ? IdleClips[0] : null;
    public string? PrimaryRunClip => RunClips.Count > 0 ? RunClips[0] : null;
    public string? PrimaryAttackClip => AttackClips.Count > 0 ? AttackClips[0] : null;
    public string? PrimaryJumpClip => JumpClips.Count > 0 ? JumpClips[0] : null;
    public string? PrimaryFallClip => FallClips.Count > 0 ? FallClips[0] : null;
}

/// <summary>
/// Infers idle/run/attack/jump/fall clips from a hero's full animation list instead of hardcoding clip
/// names per hero, and caches the result to disk (see <see cref="ClassifyOrLoadCached"/>) so the
/// classification is not redone every time a hero is selected.
/// </summary>
public static class HeroAnimationClassifier
{
    /// <summary>
    /// Classifies every clip by (1) its <see cref="ModelAsset.AnimationActivities"/> entries, whose
    /// names are stripped of the "ACT_DOTA_" prefix, then (2) a keyword fallback on the raw clip name
    /// when no activity name matched - real Dota 2 assets do not populate activity data for every clip,
    /// so the activity check is a strong hint, not a guaranteed complete taxonomy.
    /// </summary>
    public static HeroAnimationClassification Classify(
        IReadOnlyDictionary<string, AnimationClip> animations,
        IReadOnlyDictionary<string, IReadOnlyList<HeroAnimationActivity>> activities)
    {
        var idle = new List<(string Name, int? Weight)>();
        var run = new List<(string Name, int? Weight)>();
        var attack = new List<(string Name, int? Weight)>();
        var jump = new List<(string Name, int? Weight)>();
        var fall = new List<(string Name, int? Weight)>();

        foreach (var clipName in animations.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var clipActivities = activities.TryGetValue(clipName, out var found) ? found : [];
            var (category, weight) = ClassifyOne(clipName, clipActivities);
            switch (category)
            {
                case "Idle": idle.Add((clipName, weight)); break;
                case "Run": run.Add((clipName, weight)); break;
                case "Attack": attack.Add((clipName, weight)); break;
                case "Jump": jump.Add((clipName, weight)); break;
                case "Fall": fall.Add((clipName, weight)); break;
            }
        }

        return new HeroAnimationClassification(ByWeight(idle), ByWeight(run), ByWeight(attack), ByWeight(jump), ByWeight(fall));
    }

    /// <summary>Highest acttable weight first (clips with no weight data, e.g. keyword-only matches, sort last), then alphabetically for a deterministic tiebreak.</summary>
    private static List<string> ByWeight(List<(string Name, int? Weight)> clips) => clips
        .OrderByDescending(c => c.Weight ?? int.MinValue)
        .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .Select(c => c.Name)
        .ToList();

    /// <summary>
    /// Read-through disk cache at <c>cacheDirectory/&lt;npcName&gt;.json</c>. Recomputes and overwrites
    /// the cache if the hero's live clip-name set differs from what was cached (e.g. after a game update
    /// changes a hero's animation list), or if the cache file is missing or unreadable.
    /// </summary>
    public static HeroAnimationClassification ClassifyOrLoadCached(ModelAsset asset, string npcName, string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrEmpty(npcName);
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);

        var clipNames = asset.Animations.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var cachePath = Path.Combine(cacheDirectory, $"{npcName}.json");

        if (TryLoadCache(cachePath, clipNames) is { } cached) return cached;

        var classification = Classify(asset.Animations, asset.AnimationActivities);
        TrySaveCache(cachePath, cacheDirectory, clipNames, classification);
        return classification;
    }

    /// <summary>Returns the inferred category plus the acttable weight that earned it (null if the category came from the clip-name keyword fallback, which carries no weight data).</summary>
    private static (string? Category, int? Weight) ClassifyOne(string clipName, IReadOnlyList<HeroAnimationActivity> activities)
    {
        // A leading "@" (or "@@") is Source's convention for an additive/partial-body layer clip (e.g.
        // an aim or turn blend meant to be mixed onto a base pose), not a standalone full-body
        // animation. Picking one as the character's whole pose looks broken (only part of the body
        // moves), so these are left unclassified rather than becoming a candidate Idle/Run/Attack pick.
        if (clipName.StartsWith('@')) return (null, null);

        foreach (var activity in activities)
        {
            var stripped = activity.Name.StartsWith("ACT_DOTA_", StringComparison.OrdinalIgnoreCase) ? activity.Name["ACT_DOTA_".Length..] : activity.Name;
            if (stripped.Contains("IDLE", StringComparison.OrdinalIgnoreCase)) return ("Idle", activity.Weight);
            if (stripped.Contains("RUN", StringComparison.OrdinalIgnoreCase)) return ("Run", activity.Weight);
            if (stripped.Contains("ATTACK", StringComparison.OrdinalIgnoreCase)) return ("Attack", activity.Weight);
            // ACT_DOTA_FLAIL is a real Dota activity (confirmed on Axe's "flail"/"flail_anim" clips) for
            // the generic "airborne, arms/legs flailing out of control" reaction used by knockup/knockback
            // effects (e.g. Eul's Scepter's Cyclone) - closer to what an actual jump/fall looks like than
            // the Force Staff keyword fallback below, so it is checked here among the authoritative
            // activity-tag matches rather than down in the keyword fallback.
            if (stripped.Contains("JUMP", StringComparison.OrdinalIgnoreCase) || stripped.Contains("LEAP", StringComparison.OrdinalIgnoreCase) || stripped.Contains("FLAIL", StringComparison.OrdinalIgnoreCase)) return ("Jump", activity.Weight);
            if (stripped.Contains("FALL", StringComparison.OrdinalIgnoreCase) || stripped.Contains("LAND", StringComparison.OrdinalIgnoreCase)) return ("Fall", activity.Weight);
        }

        if (clipName.Contains("idle", StringComparison.OrdinalIgnoreCase)) return ("Idle", null);
        if (clipName.Contains("run", StringComparison.OrdinalIgnoreCase) || clipName.Contains("sprint", StringComparison.OrdinalIgnoreCase)) return ("Run", null);
        if (clipName.Contains("attack", StringComparison.OrdinalIgnoreCase) || clipName.Contains("swing", StringComparison.OrdinalIgnoreCase)) return ("Attack", null);
        if (clipName.Contains("jump", StringComparison.OrdinalIgnoreCase) || clipName.Contains("leap", StringComparison.OrdinalIgnoreCase) || clipName.Contains("hop", StringComparison.OrdinalIgnoreCase)) return ("Jump", null);
        if (clipName.Contains("fall", StringComparison.OrdinalIgnoreCase) || clipName.Contains("land", StringComparison.OrdinalIgnoreCase)) return ("Fall", null);

        // Dota 2 has no jump ability, but "Force Staff" (a universal item nearly every hero has a
        // reaction clip for) knocks its target airborne, which is a reasonable stand-in pose for
        // Jump/Fall despite the name containing neither word. Most forcestaff/flail reaction clips
        // (including both the "_friendly"- and "_enemy"-cast variants - confirmed empirically, an
        // earlier version of this heuristic wrongly assumed "_enemy" meant a grounded caster gesture
        // and excluded it) already carry a real ACT_DOTA_FLAIL activity and are caught by the activity
        // loop above; this keyword fallback only ever fires for the remaining variants that don't (e.g.
        // Axe's base "forcestaff" clip, tagged ACT_DOTA_FORCESTAFF_STATUE). An "end" variant (the
        // landing/recovery pose, e.g. ACT_DOTA_FORCESTAFF_END) maps to Fall instead of Jump.
        if (clipName.Contains("forcestaff", StringComparison.OrdinalIgnoreCase))
        {
            return clipName.Contains("end", StringComparison.OrdinalIgnoreCase) ? ("Fall", null) : ("Jump", null);
        }

        return (null, null);
    }

    private static HeroAnimationClassification? TryLoadCache(string cachePath, List<string> liveClipNames)
    {
        if (!File.Exists(cachePath)) return null;
        try
        {
            var json = File.ReadAllText(cachePath);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json);
            if (entry is null) return null;
            if (entry.ClipNames is null || !entry.ClipNames.SequenceEqual(liveClipNames, StringComparer.OrdinalIgnoreCase)) return null;
            // Older cache files (written before Jump/Fall existed) simply lack those JSON properties;
            // System.Text.Json leaves the corresponding record parameters null rather than throwing.
            return new HeroAnimationClassification(
                entry.IdleClips ?? [],
                entry.RunClips ?? [],
                entry.AttackClips ?? [],
                entry.JumpClips ?? [],
                entry.FallClips ?? []);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or partially-written cache file falls through to a fresh classification below.
            return null;
        }
    }

    private static void TrySaveCache(string cachePath, string cacheDirectory, List<string> clipNames, HeroAnimationClassification classification)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            var entry = new CacheEntry(
                clipNames,
                [.. classification.IdleClips],
                [.. classification.RunClips],
                [.. classification.AttackClips],
                [.. classification.JumpClips],
                [.. classification.FallClips]);
            File.WriteAllText(cachePath, JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Caching is a reuse optimization, not a correctness requirement - a failed write just means
            // the next hero selection classifies again instead of reading a cache.
        }
    }

    private sealed record CacheEntry(
        List<string>? ClipNames,
        List<string>? IdleClips,
        List<string>? RunClips,
        List<string>? AttackClips,
        List<string>? JumpClips,
        List<string>? FallClips);
}
