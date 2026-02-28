using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.MercBoard;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Stalker_EN.MercBoard;

/// <summary>
/// UIFragment implementation for the mercenary offers board cartridge.
/// Manages two pages: main (with Services/Jobs tabs) and post form.
/// </summary>
public sealed partial class STMercBoardUi : UIFragment
{
    private BoxContainer? _root;
    private STMercBoardMainPage? _mainPage;
    private STMercBoardPostPage? _postPage;
    private BoundUserInterface? _userInterface;

    public override Control GetUIFragmentRoot()
    {
        return _root!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _userInterface = userInterface;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _mainPage = new STMercBoardMainPage();
        _postPage = new STMercBoardPostPage();

        _root.AddChild(_mainPage);
        _root.AddChild(_postPage);

        _postPage.Visible = false;

        _mainPage.OnPostPressed += offerType =>
        {
            _mainPage.Visible = false;
            _postPage.Visible = true;
            _postPage.SetOfferType(offerType);
        };

        _mainPage.OnWithdrawPressed += offerId =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new STMercBoardWithdrawOfferEvent(offerId)));
        };

        _mainPage.OnContactPressed += (posterMessengerId, offerId) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new STMercBoardContactPosterEvent(posterMessengerId, offerId)));
        };

        _postPage.OnBack += () =>
        {
            _postPage.Visible = false;
            _mainPage.Visible = true;
        };

        _postPage.OnSubmit += (offerType, description, price, duration) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new STMercBoardPostOfferEvent(offerType, description, price, duration)));

            _postPage.Visible = false;
            _mainPage.Visible = true;
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STMercBoardUiState boardState)
            return;

        if (boardState.SearchQuery is not null)
            _mainPage?.SetSearchQuery(boardState.SearchQuery);

        _mainPage?.UpdateState(boardState);
    }
}
