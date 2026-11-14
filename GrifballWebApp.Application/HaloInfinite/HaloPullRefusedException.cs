namespace GrifballWebApp.Application.HaloInfinite;

/// <summary>
/// A pull from Halo that can't be done now (the medals are downloaded already). The site answers it with a 409, and the
/// agents' tools with its message.
/// </summary>
public class HaloPullRefusedException : Exception
{
    public HaloPullRefusedException(string message) : base(message)
    {
    }
}
