using System.Linq;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.Portraits;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Stalker.Portraits;

/// <summary>
/// Resolves CharacterPortraitComponent.PortraitId into PortraitTexturePath.
/// Works at MapInit, ComponentAdd (runtime spawn), and manual calls.
/// If no PortraitId is set, picks a random fallback portrait matching the entity's band + rank.
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
    /// If PortraitId is explicitly set — pick a random texture from it.
    /// If empty — pick a random fallback matching the entity's band + rank.
    /// </summary>
    public void ResolvePortrait(EntityUid uid, CharacterPortraitComponent comp)
    {
        // If portrait ID explicitly set — pick random texture from it
        if (!string.IsNullOrEmpty(comp.PortraitId))
        {
            if (_protoManager.TryIndex<CharacterPortraitPrototype>(comp.PortraitId, out var proto))
            {
                comp.PortraitTexturePath = PickRandomTexture(proto.Textures);
                Dirty(uid, comp);
                return;
            }
        }

        // No ID set — find matching fallback by band + rank
        if (TryComp<BandsComponent>(uid, out var bands))
        {
            // Try 1: Full band + rank hierarchy lookup
            if (bands.BandProto.HasValue)
            {
                var band = bands.BandProto.Value;
                var rankId = bands.BandRankId;

                if (_protoManager.TryIndex<STBandPrototype>(band, out var bandProto) &&
                    bandProto.Hierarchy.TryGetValue(rankId, out var jobProtoId))
                {
                    var jobId = jobProtoId.Id;
                    var matches = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
                        .Where(p => p.BandId == band.Id && p.JobId == jobId && p.IsFallback)
                        .ToList();

                    if (matches.Count > 0)
                    {
                        var chosen = PickRandomTexture(matches);
                        comp.PortraitTexturePath = chosen;
                        Dirty(uid, comp);
                        return;
                    }
                }
            }

            // Try 2: No BandProto — match by BandName (e.g. "Zombie")
            if (!string.IsNullOrEmpty(bands.BandName))
            {
                var matches = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
                    .Where(p => p.BandId == bands.BandName && p.IsFallback)
                    .ToList();

                if (matches.Count > 0)
                {
                    var chosen = PickRandomTexture(matches);
                    comp.PortraitTexturePath = chosen;
                    Dirty(uid, comp);
                    return;
                }
            }
        }

        // Final fallback: any random portrait (but skip admin-only ones without bandId)
        var any = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
            .Where(p => !string.IsNullOrEmpty(p.BandId)) // Skip admin-only portraits
            .ToList();
        if (any.Count > 0)
        {
            comp.PortraitTexturePath = PickRandomTexture(any);
            Dirty(uid, comp);
        }
    }

    private string PickRandomTexture(List<string> texturePaths)
    {
        if (texturePaths.Count == 0)
            return string.Empty;

        return texturePaths[_random.Next(texturePaths.Count)];
    }

    private string PickRandomTexture(List<CharacterPortraitPrototype> portraits)
    {
        var proto = portraits[_random.Next(portraits.Count)];
        return PickRandomTexture(proto.Textures);
    }
}
