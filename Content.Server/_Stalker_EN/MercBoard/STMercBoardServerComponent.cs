using Content.Shared._Stalker_EN.MercBoard;
using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server._Stalker_EN.MercBoard;

/// <summary>
/// Server-side state for a mercenary board PDA cartridge instance.
/// Not networked — client receives data via <see cref="STMercBoardUiState"/> through the BUI system.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(STMercBoardSystem))]
public sealed partial class STMercBoardServerComponent : Component
{
    /// <summary>
    /// The player's account user ID (from NetUserId). Used with character name as composite identity.
    /// </summary>
    [ViewVariables]
    public Guid OwnerUserId;

    /// <summary>
    /// Character name of the PDA's owner.
    /// </summary>
    [ViewVariables]
    public string OwnerCharacterName = string.Empty;

    /// <summary>
    /// Whether the PDA owner is a mercenary (cached at spawn time).
    /// </summary>
    [ViewVariables]
    public bool IsMercenary;

    /// <summary>
    /// Next allowed post time (absolute simulation time). Rate-limits offer creation.
    /// </summary>
    [AutoPausedField]
    [ViewVariables]
    public TimeSpan NextPostTime;

    /// <summary>
    /// Next allowed contact time (absolute simulation time). Rate-limits contact button usage.
    /// </summary>
    [AutoPausedField]
    [ViewVariables]
    public TimeSpan NextContactTime;

    /// <summary>
    /// One-shot search pre-fill. Consumed and cleared by the next UI state update.
    /// Set by external systems (e.g. messenger offer link navigation).
    /// </summary>
    [ViewVariables]
    public string? PendingSearchQuery;
}
