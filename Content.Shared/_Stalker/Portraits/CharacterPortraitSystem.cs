using System.Linq;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.Portraits;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Stalker.Portraits;

/// <summary>
/// Resolves CharacterPortraitComponent.PortraitId into PortraitTexturePath.
/// Works at MapInit, ComponentAdd (runtime spawn), and manual calls.
/// If no PortraitId is set, picks a random texture from available portraits for the entity's job/band.
/// </summary>
public sealed class CharacterPortraitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterPortraitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CharacterPortraitComponent, ComponentAdd>(OnComponentAdd);
    }

    private void OnMapInit(EntityUid uid, CharacterPortraitComponent comp, MapInitEvent args)
    {
        ResolvePortrait(uid, comp);
    }

    private void OnComponentAdd(EntityUid uid, CharacterPortraitComponent comp, ComponentAdd args)
    {
        ResolvePortrait(uid, comp);
    }

    /// <summary>
    /// Resolve portrait into texture path.
    /// If PortraitId is explicitly set — pick random texture from that prototype.
    /// If empty — pick random texture from portraits matching the entity's job/band.
    /// </summary>
    public void ResolvePortrait(EntityUid uid, CharacterPortraitComponent comp)
    {
        // If portrait ID explicitly set — pick random texture from that prototype
        if (!string.IsNullOrEmpty(comp.PortraitId))
        {
            if (_protoManager.TryIndex<CharacterPortraitPrototype>(comp.PortraitId, out var proto))
            {
                comp.PortraitTexturePath = PickRandomTexture(proto.Textures);
                Dirty(uid, comp);
            }
        }
        else
        {
            // No ID set — pick random from matching portraits for MAIN portrait
            string? targetBandId = null;
            string? targetJobId = null;

            // Get band and job from BandsComponent (for NPCs)
            if (TryComp<BandsComponent>(uid, out var bands))
            {
                targetBandId = bands.BandProto?.Id ?? bands.BandName;

                if (bands.BandProto.HasValue)
                {
                    if (_protoManager.TryIndex<STBandPrototype>(bands.BandProto.Value, out var bandProto) &&
                        bandProto.Hierarchy.TryGetValue(bands.BandRankId, out var jobProtoId))
                    {
                        targetJobId = jobProtoId.Id;
                    }
                }
            }

            // Find all matching portraits
            var matches = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
                .Where(p =>
                {
                    // Must match band if we have one
                    if (!string.IsNullOrEmpty(targetBandId) && p.BandId != targetBandId)
                        return false;

                    // Match by job if specified (or include portraits with no job restriction)
                    if (!string.IsNullOrEmpty(targetJobId) && !string.IsNullOrEmpty(p.JobId) && p.JobId != targetJobId)
                        return false;

                    return true;
                })
                .ToList();

            if (matches.Count > 0)
            {
                // Pick random portrait, then random texture from it
                var chosenProto = matches[_random.Next(matches.Count)];
                comp.PortraitTexturePath = PickRandomTexture(chosenProto.Textures);
                Dirty(uid, comp);
            }
        }

        // Resolve Disguise Portrait Path (for Clear Sky / Monolith disguise)
        // Always resolve this for NPCs (who don't have profiles)
        ResolveDisguisePortrait(uid, comp);
    }

    /// <summary>
    /// Resolves the disguise portrait path randomly from Stalker portraits
    /// if the entity belongs to a faction capable of disguise.
    /// </summary>
    private void ResolveDisguisePortrait(EntityUid uid, CharacterPortraitComponent comp)
    {
        // Factions that can disguise as Stalkers
        var canDisguise = false;

        if (TryComp<BandsComponent>(uid, out var bands) && bands.BandProto.HasValue)
        {
            var bandId = bands.BandProto.Value.Id;
            canDisguise = (bandId == "STClearSkyBand" || bandId == "STMonolithBand");
        }

        if (!canDisguise)
            return;

        // Find Stalker portraits
        var stalkerPortraits = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
            .Where(p => p.JobId == "Stalker")
            .ToList();

        if (stalkerPortraits.Count > 0)
        {
            // If manually set, use it. If empty, pick random.
            // (For NPCs this will always be random)
            if (string.IsNullOrEmpty(comp.DisguisedPortraitPath))
            {
                var chosenProto = stalkerPortraits[_random.Next(stalkerPortraits.Count)];
                comp.DisguisedPortraitPath = PickRandomTexture(chosenProto.Textures);
                Dirty(uid, comp);
            }
        }
    }

    private string PickRandomTexture(List<string> texturePaths)
    {
        if (texturePaths.Count == 0)
            return string.Empty;

        return texturePaths[_random.Next(texturePaths.Count)];
    }
}
