
namespace Pinus.DotNetClient
{

    public enum TransportState
    {
        start = 0,
        opened = 1,
        closed = 2			// connection closed, will ignore all the message and wait for clean up
    }
}