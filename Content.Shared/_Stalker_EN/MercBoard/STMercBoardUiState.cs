using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.MercBoard;

/// <summary>
/// Full UI state for the mercenary board cartridge, sent via the CartridgeLoader BUI system.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMercBoardUiState : BoundUserInterfaceState
{
    /// <summary>
    /// All currently active service offers (merc-posted). Visible to everyone.
    /// </summary>
    public readonly List<STMercBoardOffer> Services;

    /// <summary>
    /// Job request offers visible to this player.
    /// Mercenaries see all job requests; non-mercenaries see only their own.
    /// </summary>
    public readonly List<STMercBoardOffer> Jobs;

    /// <summary>
    /// Whether the PDA owner is a mercenary (determines posting abilities and job visibility).
    /// </summary>
    public readonly bool IsMercenary;

    /// <summary>
    /// Character name of the PDA owner (used to identify own offers for Withdraw button).
    /// </summary>
    public readonly string OwnerCharacterName;

    /// <summary>
    /// Number of active service offers posted by this player.
    /// </summary>
    public readonly int MyServiceCount;

    /// <summary>
    /// Number of active job requests posted by this player.
    /// </summary>
    public readonly int MyJobCount;

    /// <summary>
    /// One-shot search pre-fill from the server (e.g. when navigating from an offer link).
    /// Null means no search pre-fill requested.
    /// </summary>
    public readonly string? SearchQuery;

    public STMercBoardUiState(
        List<STMercBoardOffer> services,
        List<STMercBoardOffer> jobs,
        bool isMercenary,
        string ownerCharacterName,
        int myServiceCount,
        int myJobCount,
        string? searchQuery = null)
    {
        Services = services;
        Jobs = jobs;
        IsMercenary = isMercenary;
        OwnerCharacterName = ownerCharacterName;
        MyServiceCount = myServiceCount;
        MyJobCount = myJobCount;
        SearchQuery = searchQuery;
    }
}
