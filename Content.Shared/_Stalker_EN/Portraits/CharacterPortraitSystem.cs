using System.Linq;
using Content.Shared._Stalker.Bands;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker_EN.Portraits;

/// <summary>
/// Resolves character portraits for entities.
/// If PortraitTexturePath is not set, picks a random texture from available portraits for the entity's job/band.
/// Also resolves disguise portraits for factions that can disguise.
/// </summary>
public sealed class CharacterPortraitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("st.portrait.system");
        SubscribeLocalEvent<CharacterPortraitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BandsComponent, ComponentAdd>(OnBandsComponentAdd);
    }

    private void OnMapInit(EntityUid uid, CharacterPortraitComponent comp, MapInitEvent args)
    {
        // NPCs need portrait resolution on map initialization because they have BandsComponent
        // from their prototype and MapInit fires after entity spawn with full component state.
        // Players are handled via BandsComponent ComponentAdd after DoJobSpecials to ensure
        // band information is available before resolution.
        ResolvePortrait(uid, comp);
    }

    private void OnBandsComponentAdd(EntityUid uid, BandsComponent comp, ComponentAdd args)
    {
        // BandsComponent is set via AddComponentSpecial (DoJobSpecials) after SpawnPlayerMob,
        // so we need to resolve portrait here to ensure band information is available.
        // This prevents premature resolution with missing band context.
        if (!TryComp<CharacterPortraitComponent>(uid, out var portraitComp))
            return;

        // Skip main portrait re-resolution if player explicitly selected a portrait to preserve their choice.
        // However, always resolve disguise portrait since it depends on band information.
        if (string.IsNullOrEmpty(portraitComp.PortraitTexturePath))
        {
            ResolvePortrait(uid, portraitComp);
        }
        else
        {
            // Main portrait already set by player, but still need to resolve disguise portrait.
            ResolveDisguisePortrait(uid, portraitComp);
        }
    }

    /// <summary>
    /// Resolves character portrait into texture path.
    /// Validates existing paths and picks random textures for missing ones based on job/band context.
    /// Called automatically on MapInit/ComponentAdd and manually via VV for re-resolution.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public void ResolvePortrait(EntityUid uid, CharacterPortraitComponent comp)
    {
        // Validate existing portrait path to ensure it references a valid texture.
        // This catches stale paths from deleted portraits or prototype changes.
        if (!string.IsNullOrEmpty(comp.PortraitTexturePath))
        {
            var currentPath = new ResPath(comp.PortraitTexturePath);

            // Support both legacy full paths and new relative paths for compatibility
            var textureExists = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
                .Any(p => p.Textures.Any(t => t == currentPath || p.GetFullPath(t) == currentPath));

            if (!textureExists)
            {
                _sawmill.Warning($"Portrait texture path not found in any prototype: {comp.PortraitTexturePath}");
                comp.PortraitTexturePath = string.Empty;
                Dirty(uid, comp);
            }
            else
            {
                // Valid path - keep as-is (supports both relative and absolute paths)
                Dirty(uid, comp);
                ResolveDisguisePortrait(uid, comp);
                return;
            }
        }

        // Pick random portrait based on job/band context.
        // Priority: PortraitJobId (explicit override) > BandsComponent hierarchy > fallback.
        string? targetBandId = null;
        string? targetJobId = null;

        // PortraitJobId takes precedence over BandsComponent hierarchy for explicit overrides.
        if (!string.IsNullOrEmpty(comp.PortraitJobId))
        {
            targetJobId = comp.PortraitJobId;
        }
        else
        {
            // Fall back to BandsComponent hierarchy for NPCs and dynamic assignment.
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
        }

        // Filter portraits by band and job to find matching candidates.
        // Band filtering is strict; job filtering depends on whether we have a specific job ID.
        var matches = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
            .Where(p =>
            {
                // Strict band matching when band ID is specified.
                if (!string.IsNullOrEmpty(targetBandId) && p.BandId != targetBandId)
                    return false;

                // Strict job matching when job ID is specified; otherwise accept jobs without ID.
                if (!string.IsNullOrEmpty(targetJobId))
                {
                    return p.JobId == targetJobId;
                }
                else
                {
                    return string.IsNullOrEmpty(p.JobId);
                }
            })
            .ToList();

        if (matches.Count > 0)
        {
            // Randomly select from matching portraits to provide variety.
            var chosenProto = matches[_random.Next(matches.Count)];
            var texturePath = PickRandomTexture(chosenProto.Textures);
            comp.PortraitTexturePath = texturePath.ToString();
            Dirty(uid, comp);
        }
        else
        {
            _sawmill.Warning($"No matching portrait prototypes found for band: {targetBandId}, job: {targetJobId}");
        }

        // Resolve disguise portrait for factions that can disguise (e.g., Clear Sky).
        // NPCs need this because they don't have profiles with explicit portrait selection.
        ResolveDisguisePortrait(uid, comp);
    }

    /// <summary>
    /// Resolves disguise portrait for factions capable of disguise.
    /// Selects random portrait from target faction's portrait set based on DisguiseTargetJobId.
    /// </summary>
    private void ResolveDisguisePortrait(EntityUid uid, CharacterPortraitComponent comp)
    {
        // Determine if faction can disguise by checking both band-change capability
        // and explicit disguise target job ID. This supports both band-switching (Bandit->Freedom)
        // and job-based disguise (Clear Sky->Stalker).
        var canDisguise = false;
        var targetJobId = (string?)null;

        if (TryComp<BandsComponent>(uid, out var bands))
        {
            if (bands.BandProto.HasValue)
            {
                if (_protoManager.TryIndex<STBandPrototype>(bands.BandProto.Value, out var bandProto))
                {
                    targetJobId = bandProto.DisguiseTargetJobId?.ToString();
                    // Factions can disguise if they have CanChange capability AND either
                    // an AltBand for band-switching or a DisguiseTargetJobId for job-based disguise.
                    canDisguise = bands.CanChange && (bands.AltBand != null || targetJobId != null);
                }
            }
        }

        // Clear disguise state if faction cannot disguise or has no target job.
        if (!canDisguise || string.IsNullOrEmpty(targetJobId))
        {
            if (comp.IsDisguised)
            {
                comp.IsDisguised = false;
                Dirty(uid, comp);
            }
            return;
        }

        // Mark entity as disguised for factions with disguise capability.
        if (!comp.IsDisguised)
        {
            comp.IsDisguised = true;
            Dirty(uid, comp);
        }

        // Select random portrait from target faction's portrait set.
        var targetPortraits = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>()
            .Where(p => p.JobId == targetJobId)
            .ToList();

        if (targetPortraits.Count > 0)
        {
            // Pick random portrait for disguise unless explicitly set by profile.
            // NPCs always get random selection since they don't have profiles.
            if (string.IsNullOrEmpty(comp.DisguisedPortraitPath))
            {
                var chosenProto = targetPortraits[_random.Next(targetPortraits.Count)];
                comp.DisguisedPortraitPath = PickRandomTexture(chosenProto.Textures).ToString();
                Dirty(uid, comp);
            }
        }
    }

    /// <summary>
    /// Picks a random texture from a list and converts relative paths to full paths.
    /// Returns empty ResPath if list is empty and logs a warning.
    /// </summary>
    private ResPath PickRandomTexture(List<ResPath> texturePaths)
    {
        if (texturePaths.Count == 0)
        {
            _sawmill.Warning("Attempted to pick random texture from empty list");
            return ResPath.Empty;
        }

        var randomPath = texturePaths[_random.Next(texturePaths.Count)];

        // Convert relative paths to full paths using prototype's GetFullPath method.
        // This ensures consistent path resolution regardless of input format.
        var firstProto = _protoManager.EnumeratePrototypes<CharacterPortraitPrototype>().FirstOrDefault();
        if (firstProto != null)
        {
            return firstProto.GetFullPath(randomPath);
        }

        // Fallback to raw path if no prototype available (should not occur in normal operation).
        return randomPath;
    }
}
