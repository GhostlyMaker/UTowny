using OpenMod.API.Commands;
namespace UTowny.Utilities;

public static class UTownyChat
{
    public static readonly System.Drawing.Color Color = System.Drawing.Color.FromArgb(0, 174, 98);
    public static Task SendAsync(ICommandActor actor, string message) => actor.PrintMessageAsync(message, Color);
}
