using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Stalker_EN.Portraits;
using Robust.Client.Graphics;
using Robust.Client.Localization;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Lobby.UI.Loadouts;

/// <summary>
/// Simple grid-based portrait selector for the LoadoutWindow Portrait tab.
/// </summary>
public sealed class PortraitSelector : BoxContainer
{
    private readonly IResourceCache _resCache;
    private readonly IRobustRandom _random;
    private readonly GridContainer _grid;
    private readonly TextureRect _previewRect;
    private readonly Label _previewName;
    private readonly Label _previewDesc;
    private readonly Dictionary<string, Button> _buttons = new();

    public event Action<string>? OnPortraitSelected;

    public PortraitSelector()
    {
        _resCache = IoCManager.Resolve<IResourceCache>();
        _random = IoCManager.Resolve<IRobustRandom>();

        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        _grid = new GridContainer { Columns = 4, HorizontalExpand = true };
        scroll.AddChild(_grid);
        AddChild(scroll);

        var preview = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinSize = new Vector2(150, 0),
        };
        preview.AddChild(new Label { Text = Loc.GetString("st-portrait-preview-label"), StyleClasses = { "labelHeading" }, HorizontalAlignment = HAlignment.Center });
        preview.AddChild(new Control { MinSize = new Vector2(0, 5) });

        var texPanel = new PanelContainer { HorizontalAlignment = HAlignment.Center };
        texPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1B1B1E") };
        _previewRect = new TextureRect { Stretch = TextureRect.StretchMode.KeepCentered, MinSize = new Vector2(128, 128) };
        texPanel.AddChild(_previewRect);
        preview.AddChild(texPanel);

        preview.AddChild(new Control { MinSize = new Vector2(0, 5) });
        _previewName = new Label { HorizontalAlignment = HAlignment.Center };
        preview.AddChild(_previewName);
        _previewDesc = new Label { HorizontalAlignment = HAlignment.Center, FontColorOverride = Color.FromHex("#888888") };
        preview.AddChild(_previewDesc);

        AddChild(preview);
    }

    public void Setup(List<CharacterPortraitPrototype> portraits, string? selectedId, IPrototypeManager protoMan)
    {
        _grid.RemoveAllChildren();
        _buttons.Clear();

        // Flatten portraits: each texture becomes a button
        var textureEntries = new List<(CharacterPortraitPrototype Proto, string TexturePath, int Index)>();
        foreach (var proto in portraits)
        {
            for (var i = 0; i < proto.Textures.Count; i++)
                textureEntries.Add((proto, proto.Textures[i], i));
        }

        // Fallback: auto-select random if none selected
        if ((string.IsNullOrEmpty(selectedId) || !textureEntries.Any(t => t.TexturePath == selectedId))
            && textureEntries.Count > 0)
        {
            selectedId = textureEntries[_random.Next(textureEntries.Count)].TexturePath;
            OnPortraitSelected?.Invoke(selectedId);
        }

        foreach (var entry in textureEntries)
        {
            var btn = CreateButton(entry.Proto, entry.TexturePath, entry.Index);
            btn.Pressed = entry.TexturePath == selectedId;
            if (entry.TexturePath == selectedId)
                UpdatePreview(entry.Proto, entry.TexturePath);

            _buttons[entry.TexturePath] = btn;
            _grid.AddChild(btn);
        }

        if (string.IsNullOrEmpty(selectedId))
        {
            _previewName.Text = Loc.GetString("st-portrait-no-selection-name");
            _previewDesc.Text = Loc.GetString("st-portrait-no-selection-desc");
            _previewRect.Texture = null;
        }
    }

    private Button CreateButton(CharacterPortraitPrototype proto, string texturePath, int index)
    {
        var btn = new Button { MinSize = new Vector2(64, 64), ToggleMode = true };

        var texRect = new TextureRect { Stretch = TextureRect.StretchMode.KeepCentered, HorizontalExpand = true, VerticalExpand = true };
        btn.AddChild(texRect);

        if (_resCache.TryGetResource<TextureResource>(texturePath, out var tex))
            texRect.Texture = tex;

        btn.OnToggled += _ =>
        {
            if (btn.Pressed)
            {
                foreach (var kvp in _buttons)
                    kvp.Value.Pressed = false;
                btn.Pressed = true;
                UpdatePreview(proto, texturePath);
                OnPortraitSelected?.Invoke(texturePath);
            }
        };

        return btn;
    }

    private void UpdatePreview(CharacterPortraitPrototype proto, string texturePath)
    {
        _previewName.Text = proto.Textures.Count > 1
            ? Loc.GetString("st-portrait-preview-multi", ("name", Loc.GetString(proto.Name)), ("current", proto.Textures.IndexOf(texturePath) + 1), ("total", proto.Textures.Count))
            : Loc.GetString(proto.Name);
        _previewDesc.Text = proto.Description != null ? Loc.GetString(proto.Description) : string.Empty;

        if (_resCache.TryGetResource<TextureResource>(texturePath, out var tex))
            _previewRect.Texture = tex;
        else
            _previewRect.Texture = null;
    }
}
