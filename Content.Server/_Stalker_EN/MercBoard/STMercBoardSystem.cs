using Content.Server._Stalker_EN.PdaMessenger;
using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Shared._Stalker.Bands;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared._Stalker_EN.MercBoard;
using Content.Shared._Stalker_EN.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker_EN.MercBoard;

/// <summary>
/// Server system for the mercenary offers board PDA cartridge.
/// Manages service offers (posted by mercs) and job requests (posted by anyone),
/// handling posting, withdrawal, contact integration, and UI state broadcasting.
/// </summary>
public sealed class STMercBoardSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSTFactionResolutionSystem _factionResolution = default!;
    [Dependency] private readonly STMessengerSystem _messenger = default!;

    /// <summary>
    /// Band prototype ID for mercenaries. Used to check if a player is a mercenary.
    /// </summary>
    private static readonly ProtoId<STBandPrototype> MercenaryBandId = "STMercenariesBand";

    private const int MaxOffersPerPlayer = 1;
    private const int MaxTotalOffers = 100;
    private const int MaxDescriptionLength = 300;
    private const int MaxPriceLength = 50;
    private const int MaxDurationLength = 50;
    private static readonly TimeSpan PostCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ContactCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Active service offers (merc-posted). Key = offer ID.
    /// </summary>
    private readonly Dictionary<uint, STMercBoardOffer> _services = new();

    /// <summary>
    /// Active job requests (player-posted). Key = offer ID.
    /// </summary>
    private readonly Dictionary<uint, STMercBoardOffer> _jobs = new();

    /// <summary>
    /// Auto-increment offer ID counter.
    /// </summary>
    private uint _nextOfferId;

    /// <summary>
    /// PDAs with merc board cartridge currently active (UI open). Receive broadcast updates.
    /// </summary>
    private readonly HashSet<EntityUid> _activeLoaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STMercBoardComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<STMercBoardComponent, CartridgeActivatedEvent>(OnCartridgeActivated);
        SubscribeLocalEvent<STMercBoardComponent, CartridgeDeactivatedEvent>(OnCartridgeDeactivated);
        SubscribeLocalEvent<STMercBoardComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<STMercBoardComponent, STOpenMercBoardOfferEvent>(OnOpenOffer);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    #region Cartridge Events

    private void OnUiReady(Entity<STMercBoardComponent> ent, ref CartridgeUiReadyEvent args)
    {
        if (!TryComp<STMercBoardServerComponent>(ent, out var server))
            return;

        // Lazy init: if the board wasn't initialized at spawn (e.g. PDA re-equipped from stash),
        // resolve owner from the PDA holder now.
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            TryLazyInit(args.Loader, ent, server);

        UpdateUiState(ent, args.Loader, server);
    }

    private void OnCartridgeActivated(Entity<STMercBoardComponent> ent, ref CartridgeActivatedEvent args)
    {
        _activeLoaders.Add(args.Loader);
    }

    private void OnCartridgeDeactivated(Entity<STMercBoardComponent> ent, ref CartridgeDeactivatedEvent args)
    {
        _activeLoaders.Remove(args.Loader);
    }

    private void OnOpenOffer(Entity<STMercBoardComponent> ent, ref STOpenMercBoardOfferEvent args)
    {
        if (!TryComp<STMercBoardServerComponent>(ent, out var server))
            return;

        server.PendingSearchQuery = $"#{args.OfferId}";
        _cartridgeLoader.ActivateProgram(args.LoaderUid, ent);
    }

    private void OnMessage(Entity<STMercBoardComponent> ent, ref CartridgeMessageEvent args)
    {
        if (!TryComp<STMercBoardServerComponent>(ent, out var server))
            return;

        switch (args)
        {
            case STMercBoardPostOfferEvent post:
                OnPostOffer(ent, server, post, args);
                break;
            case STMercBoardWithdrawOfferEvent withdraw:
                OnWithdrawOffer(server, withdraw, args);
                break;
            case STMercBoardContactPosterEvent contact:
                OnContactPoster(ent, server, contact, args);
                break;
        }
    }

    #endregion

    #region Post / Withdraw / Contact

    private void OnPostOffer(
        Entity<STMercBoardComponent> ent,
        STMercBoardServerComponent server,
        STMercBoardPostOfferEvent post,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        if (server.NextPostTime > _timing.CurTime)
            return;

        // Only mercs can post services
        if (post.OfferType == STMercBoardOfferType.Service && !server.IsMercenary)
            return;

        var description = post.Description.Trim();
        var price = post.Price.Trim();
        var duration = post.Duration.Trim();

        if (string.IsNullOrEmpty(description) || string.IsNullOrEmpty(price) || string.IsNullOrEmpty(duration))
            return;

        if (description.Length > MaxDescriptionLength)
            description = description[..MaxDescriptionLength];
        if (price.Length > MaxPriceLength)
            price = price[..MaxPriceLength];
        if (duration.Length > MaxDurationLength)
            duration = duration[..MaxDurationLength];

        var storage = post.OfferType == STMercBoardOfferType.Service ? _services : _jobs;
        var playerCount = CountPlayerOffers(storage, server.OwnerCharacterName);
        if (playerCount >= MaxOffersPerPlayer)
            return;

        if (_services.Count + _jobs.Count >= MaxTotalOffers)
            return;

        server.NextPostTime = _timing.CurTime + PostCooldown;

        string? posterFaction = ResolveFaction(args.Actor);
        var posterMessengerId = _messenger.GetMessengerId(server.OwnerUserId, server.OwnerCharacterName);

        var offer = new STMercBoardOffer(
            ++_nextOfferId,
            post.OfferType,
            server.OwnerCharacterName,
            posterMessengerId,
            posterFaction,
            description,
            price,
            duration,
            _timing.CurTime);

        storage[offer.Id] = offer;

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} posted merc board {post.OfferType}: " +
            $"price=\"{price}\", duration=\"{duration}\", desc=\"{description}\"");

        BroadcastUiUpdate();
    }

    private void OnWithdrawOffer(
        STMercBoardServerComponent server,
        STMercBoardWithdrawOfferEvent withdraw,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        // Try both storage dictionaries
        if (_services.TryGetValue(withdraw.OfferId, out var serviceOffer)
            && serviceOffer.PosterName == server.OwnerCharacterName)
        {
            _services.Remove(withdraw.OfferId);

            _adminLogger.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} withdrew merc board service #{withdraw.OfferId}");

            BroadcastUiUpdate();
            return;
        }

        if (_jobs.TryGetValue(withdraw.OfferId, out var jobOffer)
            && jobOffer.PosterName == server.OwnerCharacterName)
        {
            _jobs.Remove(withdraw.OfferId);

            _adminLogger.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} withdrew merc board job #{withdraw.OfferId}");

            BroadcastUiUpdate();
        }
    }

    private void OnContactPoster(
        Entity<STMercBoardComponent> ent,
        STMercBoardServerComponent server,
        STMercBoardContactPosterEvent contact,
        CartridgeMessageEvent args)
    {
        if (string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        if (string.IsNullOrEmpty(contact.PosterMessengerId))
            return;

        if (server.NextContactTime > _timing.CurTime)
            return;
        server.NextContactTime = _timing.CurTime + ContactCooldown;

        var loaderUid = GetEntity(args.LoaderUid);
        if (!_cartridgeLoader.TryGetProgram<STMessengerComponent>(loaderUid, out var messengerUid, out _))
            return;

        // Format draft message with offer reference
        var draftMessage = STMercBoardOffer.FormatRef(contact.OfferId);
        _messenger.OpenDm(loaderUid, messengerUid.Value, contact.PosterMessengerId, draftMessage);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} opened DM from merc board with: {contact.PosterMessengerId} (offer #{contact.OfferId})");
    }

    #endregion

    #region UI State

    private void UpdateUiState(
        Entity<STMercBoardComponent> ent,
        EntityUid loaderUid,
        STMercBoardServerComponent server)
    {
        // Consume one-shot search pre-fill from external systems (e.g. offer link navigation)
        var searchQuery = server.PendingSearchQuery;
        server.PendingSearchQuery = null;

        var state = BuildUiState(server, searchQuery);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private STMercBoardUiState BuildUiState(STMercBoardServerComponent server, string? searchQuery = null)
    {
        var services = new List<STMercBoardOffer>(_services.Values);

        // Jobs visibility: mercs see all, non-mercs see only their own
        List<STMercBoardOffer> jobs;
        if (server.IsMercenary)
        {
            jobs = new List<STMercBoardOffer>(_jobs.Values);
        }
        else
        {
            jobs = new List<STMercBoardOffer>();
            foreach (var job in _jobs.Values)
            {
                if (job.PosterName == server.OwnerCharacterName)
                    jobs.Add(job);
            }
        }

        var myServiceCount = CountPlayerOffers(_services, server.OwnerCharacterName);
        var myJobCount = CountPlayerOffers(_jobs, server.OwnerCharacterName);

        return new STMercBoardUiState(
            services,
            jobs,
            server.IsMercenary,
            server.OwnerCharacterName,
            myServiceCount,
            myJobCount,
            searchQuery);
    }

    private void BroadcastUiUpdate()
    {
        var sharedServices = new List<STMercBoardOffer>(_services.Values);

        foreach (var loaderUid in _activeLoaders)
        {
            if (!TryComp<CartridgeLoaderComponent>(loaderUid, out _))
                continue;

            if (!_cartridgeLoader.TryGetProgram<STMercBoardComponent>(
                    loaderUid, out var progUid, out _))
                continue;

            if (!TryComp<STMercBoardServerComponent>(progUid.Value, out var server))
                continue;

            List<STMercBoardOffer> jobs;
            if (server.IsMercenary)
            {
                jobs = new List<STMercBoardOffer>(_jobs.Values);
            }
            else
            {
                jobs = new List<STMercBoardOffer>();
                foreach (var job in _jobs.Values)
                {
                    if (job.PosterName == server.OwnerCharacterName)
                        jobs.Add(job);
                }
            }

            var myServiceCount = CountPlayerOffers(_services, server.OwnerCharacterName);
            var myJobCount = CountPlayerOffers(_jobs, server.OwnerCharacterName);

            var state = new STMercBoardUiState(
                sharedServices,
                jobs,
                server.IsMercenary,
                server.OwnerCharacterName,
                myServiceCount,
                myJobCount);

            _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
        }
    }

    #endregion

    #region Player Spawn & Init

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (!_inventory.TryGetSlotEntity(args.Mob, "id", out var idEntity))
            return;

        if (!TryComp<PdaComponent>(idEntity, out _))
            return;

        if (!_cartridgeLoader.TryGetProgram<STMercBoardComponent>(idEntity.Value, out var progUid, out _))
            return;

        if (!TryComp<STMercBoardServerComponent>(progUid.Value, out var server))
            return;

        if (!string.IsNullOrEmpty(server.OwnerCharacterName))
            return;

        var userId = args.Player.UserId.UserId;
        InitializeBoardForPda(progUid.Value, server, userId, args.Profile.Name, args.Mob);
    }

    /// <summary>
    /// Initializes the merc board server component for a character.
    /// Sets owner identity and caches mercenary status from BandsComponent.
    /// </summary>
    private void InitializeBoardForPda(
        EntityUid cartridgeUid,
        STMercBoardServerComponent server,
        Guid userId,
        string charName,
        EntityUid mobUid)
    {
        server.OwnerUserId = userId;
        server.OwnerCharacterName = charName;
        server.IsMercenary = IsMercenary(mobUid);
    }

    /// <summary>
    /// Lazy initialization when the board UI is opened but the component wasn't set up at spawn.
    /// Resolves the PDA holder from the loader (PDA) entity's transform parent.
    /// Only works for player-controlled entities.
    /// </summary>
    private void TryLazyInit(
        EntityUid loaderUid,
        EntityUid cartridgeUid,
        STMercBoardServerComponent server)
    {
        if (!TryComp<TransformComponent>(loaderUid, out var xform))
            return;

        // PDA in inventory: ParentUid is the mob
        var holder = xform.ParentUid;
        if (!holder.IsValid())
            return;

        // Only initialize for player-controlled entities
        if (!TryComp<ActorComponent>(holder, out var actor))
            return;

        var userId = actor.PlayerSession.UserId.UserId;
        var charName = MetaData(holder).EntityName;
        InitializeBoardForPda(cartridgeUid, server, userId, charName, holder);
    }

    #endregion

    #region Round Lifecycle

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _services.Clear();
        _jobs.Clear();
        _nextOfferId = 0;
        _activeLoaders.Clear();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Checks if an entity is a member of the mercenary faction.
    /// </summary>
    private bool IsMercenary(EntityUid uid)
    {
        if (!TryComp<BandsComponent>(uid, out var bands))
            return false;

        return bands.BandProto == MercenaryBandId;
    }

    /// <summary>
    /// Resolves the faction name for an entity via BandsComponent.
    /// </summary>
    private string? ResolveFaction(EntityUid uid)
    {
        if (!TryComp<BandsComponent>(uid, out var bands))
            return null;

        if (bands.BandProto is not { } bandProtoId)
            return null;

        if (!_protoManager.TryIndex(bandProtoId, out var bandProto))
            return null;

        return _factionResolution.GetBandFactionName(bandProto.Name);
    }

    /// <summary>
    /// Counts how many offers a player has in a given storage dictionary.
    /// </summary>
    private static int CountPlayerOffers(Dictionary<uint, STMercBoardOffer> storage, string playerName)
    {
        var count = 0;
        foreach (var offer in storage.Values)
        {
            if (offer.PosterName == playerName)
                count++;
        }

        return count;
    }

    #endregion
}
