using System.ComponentModel;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.XboxAccounts;

namespace GrifballWebApp.Application.Tools;

/// <summary>The pictures the site keeps of a player, as addresses a chat shows as pictures: the ones the site stored, and no others.</summary>
public class ImageTools
{
    private readonly ToolCaller _caller;
    private readonly UrlService _urls;
    private readonly XboxImageService _images;

    public ImageTools(ToolCaller caller, UrlService urls, XboxImageService images)
    {
        _caller = caller;
        _urls = urls;
        _images = images;
    }

    [Description("Show a player's picture in your reply: their Spartan headshot, their Halo emblem or their gamer picture, from their user ID (search and get_player give it). "
        + "Give the address in your reply exactly as this returns it, on its own: the chat turns it into the picture. Never write a picture address of your own.")]
    public async Task<string> GetPlayerImage([Description("The player's user ID.")] int userID,
        [Description("Which picture.")] PlayerImageKind image,
        CancellationToken ct = default)
    {
        if (!await ChatImages.StoredAsync(_images, new ChatImage(userID, image), ct))
            throw new ToolException($"The site has no {image} stored for user {userID}: they may have none, or the site hasn't fetched it yet.");
        // The site's own chat gets a path it shows on the page; anywhere else (Discord, MCP) a whole address
        return (_caller.RelativeLinks ? UrlService.Relative : _urls).PlayerImage(userID, ChatImages.Slug(image));
    }
}
