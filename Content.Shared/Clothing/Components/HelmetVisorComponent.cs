using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HelmetVisorSystem))]
public sealed partial class HelmetVisorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ToggleHelmetVisorEvent";

    [DataField]
    public string? IconStateUp;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [DataField, AutoNetworkedField]
    public bool IsUp;

    [DataField, AutoNetworkedField]
    public string? EquippedPrefixUp;

    [DataField, AutoNetworkedField]
    public bool IsToggleable = true;

    [DataField, AutoNetworkedField]
    public float ToggleDelay = 1.5f;

    [DataField, AutoNetworkedField]
    public float LastToggleTime;

    [DataField]
    public DamageModifierSet? VisorUpModifiers;

    public DamageModifierSet? DefaultModifiers;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public SoundSpecifier SoundVisorUp = new SoundPathSpecifier("/Audio/_Stalker_EN/Clothing/Hats/vityaz_up.ogg");

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public SoundSpecifier SoundVisorDown = new SoundPathSpecifier("/Audio/_Stalker_EN/Clothing/Hats/vityaz_down.ogg");

    [DataField]
    public float? VisorUpReflectProb;

    public float DefaultReflectProb;
}
