using Content.Shared.Actions;
using Content.Shared.Armor;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Nutrition.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using System.Security.Principal;
using Robust.Shared.Prototypes;


namespace Content.Shared.Clothing.EntitySystems;

public sealed class HelmetVisorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedVerbSystem _verbs = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HelmetVisorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HelmetVisorComponent, ToggleHelmetVisorEvent>(OnToggle);
        SubscribeLocalEvent<HelmetVisorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HelmetVisorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<HelmetVisorComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnInit(EntityUid uid, HelmetVisorComponent comp, ComponentInit args)
    {
        if (TryComp<ArmorComponent>(uid, out var armor))
            comp.DefaultModifiers = armor.Modifiers;

        if (TryComp<ReflectComponent>(uid, out var reflect))
            comp.DefaultReflectProb = reflect.ReflectProb;

        UpdateBlockers(uid, comp);
    }

    private void OnGetActions(EntityUid uid, HelmetVisorComponent comp, GetItemActionsEvent args)
    {
        if (!comp.IsToggleable)
            return;

        if (args.SlotFlags == null || (args.SlotFlags.Value & SlotFlags.HEAD) == 0)
            return;

        args.AddAction(ref comp.ToggleActionEntity, comp.ToggleAction);
        Dirty(uid, comp);
    }

    private void OnToggle(EntityUid uid, HelmetVisorComponent comp, ToggleHelmetVisorEvent args)
    {
        if (args.Handled || !comp.IsToggleable)
            return;

        if (comp.IsUp && _inventorySystem.TryGetSlotEntity(Transform(uid).ParentUid, "mask", out _))
            return;

        if (_timing.CurTime.TotalSeconds - comp.LastToggleTime < comp.ToggleDelay)
            return;

        comp.LastToggleTime = (float)_timing.CurTime.TotalSeconds;

        _audio.PlayPredicted(comp.IsUp ? comp.SoundVisorDown : comp.SoundVisorUp, uid, args.Performer);
        SetUp(uid, comp, !comp.IsUp);
        args.Handled = true;
    }

    public void SetUp(EntityUid uid, HelmetVisorComponent comp, bool up, bool force = false)
    {
        if (_timing.ApplyingState)
            return;

        if (!force && !comp.IsToggleable)
            return;

        if (comp.IsUp == up)
            return;

        comp.IsUp = up;

        if (TryComp<SlotBlockOverrideComponent>(uid, out var over))
        {
            over.Overridden = comp.IsUp;
            Dirty(uid, over);
        }
        if (comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, comp.IsUp);

        UpdateVisuals(uid, comp);
        RaiseLocalEvent(uid, new VisorToggledEvent(uid, comp.IsUp));
        Dirty(uid, comp);
    }

    private void UpdateVisuals(EntityUid uid, HelmetVisorComponent comp)
    {
        if (comp.EquippedPrefixUp != null)
        {
            var prefix = comp.IsUp ? comp.EquippedPrefixUp : null;
            _clothing.SetEquippedPrefix(uid, prefix);
        }

        _appearance.SetData(uid, HelmetVisorVisuals.IsUp, comp.IsUp);
        RaiseLocalEvent(uid, new HelmetVisorVisualsChangedEvent());
    }

    private void UpdateBlockers(EntityUid uid, HelmetVisorComponent comp)
    {
        var block = !comp.IsUp;
        RaiseLocalEvent(uid, new VisorBlockersChangedEvent(block, block));
    }
    private void OnExamine(EntityUid uid, HelmetVisorComponent comp, ExaminedEvent args)
    {
        if (!comp.IsToggleable)
            return;

        if (args.IsInDetailsRange)
        {
            var key = comp.IsUp ? "helmet-visor-up" : "helmet-visor-down";
            args.PushMarkup(Loc.GetString(key));
        }
    }
    private void OnGetVerbs(EntityUid uid, HelmetVisorComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !comp.IsToggleable)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(comp.IsUp ? "helmet-visor-lower" : "helmet-visor-raise"),
            Act = () =>
            {
                if (_timing.CurTime.TotalSeconds - comp.LastToggleTime < comp.ToggleDelay)
                    return;

                comp.LastToggleTime = (float)_timing.CurTime.TotalSeconds;

                _audio.PlayPredicted(comp.IsUp ? comp.SoundVisorDown : comp.SoundVisorUp, uid, args.User);
                SetUp(uid, comp, !comp.IsUp);
            }
        });
    }
}
public enum HelmetVisorVisuals : byte
{
    IsUp
}

public readonly record struct VisorToggledEvent(EntityUid Visor, bool IsUp);
public readonly record struct VisorBlockersChangedEvent(bool BlockIngestion, bool BlockIdentity);
public readonly record struct HelmetVisorVisualsChangedEvent;

