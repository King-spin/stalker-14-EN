using Content.Shared.CartridgeLoader;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker_EN.MercBoard;

/// <summary>
/// Client requests posting a new offer (service or job request) to the merc board.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMercBoardPostOfferEvent : CartridgeMessageEvent
{
    public readonly STMercBoardOfferType OfferType;
    public readonly string Description;
    public readonly string Price;
    public readonly string Duration;

    public STMercBoardPostOfferEvent(
        STMercBoardOfferType offerType,
        string description,
        string price,
        string duration)
    {
        OfferType = offerType;
        Description = description;
        Price = price;
        Duration = duration;
    }
}

/// <summary>
/// Client requests withdrawing one of their own offers from the board.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMercBoardWithdrawOfferEvent : CartridgeMessageEvent
{
    public readonly uint OfferId;

    public STMercBoardWithdrawOfferEvent(uint offerId)
    {
        OfferId = offerId;
    }
}

/// <summary>
/// Client requests adding the poster of an offer as a messenger contact.
/// </summary>
[Serializable, NetSerializable]
public sealed class STMercBoardContactPosterEvent : CartridgeMessageEvent
{
    public readonly string PosterMessengerId;
    public readonly uint OfferId;

    public STMercBoardContactPosterEvent(string posterMessengerId, uint offerId)
    {
        PosterMessengerId = posterMessengerId;
        OfferId = offerId;
    }
}

/// <summary>
/// Local by-ref entity event raised on a merc board cartridge entity to request
/// opening a specific offer. Decouples messenger → merc board dependency.
/// </summary>
[ByRefEvent]
public readonly record struct STOpenMercBoardOfferEvent(EntityUid LoaderUid, uint OfferId);
