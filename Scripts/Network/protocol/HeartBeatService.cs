using System;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Pinus.DotNetClient
{
    public class HeartBeatService
    {
        int interval;
        public int timeout;
        DateTime lastTime;
        private CancellationTokenSource cts;
        Protocol protocol;

        public HeartBeatService(int interval, Protocol protocol)
        {
            cts = new CancellationTokenSource();

            this.interval = interval;
            this.protocol = protocol;
        }

        internal void resetTimeout()
        {
            this.timeout = 0;
            lastTime = DateTime.Now;
        }

        public void sendHeartBeat()
        {
            TimeSpan span = DateTime.Now - lastTime;
            timeout = (int)span.TotalSeconds;

            //check timeout
            if (timeout > interval * 2)
            {
                protocol.getPinusClient().disconnect();
                stop();

            }

            //Send heart beat
            protocol.send(PackageType.PKG_HEARTBEAT);
        }

        async public UniTaskVoid start()
        {
            if (interval < 1000) return;

            while (!cts.Token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(this.interval));
                sendHeartBeat();
            }

            //Set timeout
            timeout = 0;
            lastTime = DateTime.Now;
        }

        public void stop()
        {
            cts?.Cancel();
        }
    }
}