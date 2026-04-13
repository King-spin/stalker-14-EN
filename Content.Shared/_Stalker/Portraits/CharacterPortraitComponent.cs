using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared._Stalker.Portraits;

/// <summary>
/// Stores the selected character portrait for a mob entity.
/// Set during character spawning from the player's profile,
/// or manually in YAML for NPC dolls.
/// Used for PDA notification sender icons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CharacterPortraitComponent : Component
{
    /// <summary>
    /// Portrait prototype ID. Used in YAML to assign a portrait to an NPC.
    /// Resolved into <see cref="PortraitTexturePath"/> at MapInit.
    /// Example: Portrait_StalkerRookie
    /// </summary>
    [DataField("portrait")]
    public string PortraitId = string.Empty;

    /// <summary>
    /// The resolved texture path of the selected portrait.
    /// Empty string means no portrait selected.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string PortraitTexturePath = string.Empty;

    /// <summary>
    /// Resolved texture path for the disguise portrait (e.g., Stalker portrait for Clear Sky).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string DisguisedPortraitPath = string.Empty;

    /// <summary>
    /// If true, Clear Sky members use DisguisedPortraitPath for PDA icons.
    /// Defaults to true (masked).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsDisguised = true;
}
