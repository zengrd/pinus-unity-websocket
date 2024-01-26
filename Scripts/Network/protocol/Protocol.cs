using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityWebSocket;

namespace Pinus.DotNetClient
{
    public class Protocol
    {
        private MessageProtocol messageProtocol;
        private ProtocolState state;
        private Transporter transporter;
        private WebSocket socket;
        private HandShakeService handshake;
        private JObject user;
        private Action<JObject> handshakeCallback;
        private HeartBeatService heartBeatService = null;
        private PinusClient pc;
        private Action<JObject> initCallback;

        public PinusClient getPinusClient()
        {
            return this.pc;
        }

        public Protocol(PinusClient pc, WebSocket socket, Action<JObject> initCallback = null)
        {
            this.pc = pc;
            this.socket = socket;
            this.handshake = new HandShakeService(this);
            this.transporter = new Transporter(this, socket);
            this.transporter.onDisconnect = onDisconnect;

            
            this.state = ProtocolState.start;
            this.initCallback = initCallback;
        }

        // websocket 连接成功，就使用request请求
        private void OnOpen(object sender, OpenEventArgs e)
        {
            this.handshake.request(this.user, this.handshakeCallback);
            this.state = ProtocolState.handshaking;
        }

        internal void start()
        {
            start(null, null);
        }

        internal void start(JObject user, Action<JObject> handshakeCallback)
        {
            this.user = user;
            this.handshakeCallback = handshakeCallback;
            this.transporter.start();
            // 先开启transporter，然后再进行OnOpen 的回调
            this.socket.OnOpen += OnOpen;
            
        }

        //Send notify, do not need id
        internal void send(string route, JObject msg)
        {
            send(route, 0, msg);
        }

        //Send request, user request id 
        internal void send(string route, uint id, JObject msg)
        {
            if (this.state != ProtocolState.working) return;

            byte[] body = messageProtocol.encode(route, id, msg);

            send(PackageType.PKG_DATA, body);
        }

        internal void send(PackageType type)
        {
            if (this.state == ProtocolState.closed) return;
            transporter.send(PackageProtocol.encode(type));
        }

        //Send system message, these message do not use messageProtocol
        internal void send(PackageType type, JObject msg)
        {
            //This method only used to send system package
            if (type == PackageType.PKG_DATA) return;

            byte[] body = Encoding.UTF8.GetBytes(msg.ToString());

            send(type, body);
        }

        //Send message use the transporter
        internal void send(PackageType type, byte[] body)
        {
            if (this.state == ProtocolState.closed) return;

            byte[] pkg = PackageProtocol.encode(type, body);

            transporter.send(pkg);
        }

        //Invoke by Transporter, process the message
        internal void processMessage(byte[] bytes)
        {
            Package pkg = PackageProtocol.decode(bytes);

            //Ignore all the message except handshading at handshake stage
            if (pkg.type == PackageType.PKG_HANDSHAKE && this.state == ProtocolState.handshaking)
            {

                //Ignore all the message except handshading
                //var data = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(pkg.body));
                JObject data = JObject.Parse(Encoding.UTF8.GetString(pkg.body));

                processHandshakeData(data);

                this.state = ProtocolState.working;

            }
            else if (pkg.type == PackageType.PKG_HEARTBEAT && this.state == ProtocolState.working)
            {
                this.heartBeatService.resetTimeout();
            }
            else if (pkg.type == PackageType.PKG_DATA && this.state == ProtocolState.working)
            {
                this.heartBeatService.resetTimeout();
                pc.processMessage(messageProtocol.decode(pkg.body));
            }
            else if (pkg.type == PackageType.PKG_KICK)
            {
                this.getPinusClient().disconnect();
                this.close();
            }
        }

        private void processHandshakeData(JObject msg)
        {
            //Handshake error
            if (!msg.ContainsKey("code") || !msg.ContainsKey("sys") || Convert.ToInt32(msg["code"]) != 200)
            {
                throw new Exception("Handshake error! Please check your handshake config.");
            }

            //Set compress data
            JObject sys = (JObject)msg["sys"];

            JObject dict = new JObject();
            if (sys.ContainsKey("dict")) dict = (JObject)sys["dict"];

            JObject protos = new JObject();
            JObject serverProtos = new JObject();
            JObject clientProtos = new JObject();

            if (sys.ContainsKey("protos"))
            {
                protos = (JObject)sys["protos"];
                serverProtos = (JObject)protos["server"];
                clientProtos = (JObject)protos["client"];
            }

            messageProtocol = new MessageProtocol(dict, serverProtos, clientProtos);

            //Init heartbeat service
            int interval = 0;
            if (sys.ContainsKey("heartbeat")) interval = Convert.ToInt32(sys["heartbeat"]);
            heartBeatService = new HeartBeatService(interval, this);

            if (interval > 0)
            {
                heartBeatService.start();
            }

            //send ack and change protocol state
            handshake.ack();
            this.state = ProtocolState.working;

            //Invoke handshake callback
            JObject user = new JObject();
            if (msg.ContainsKey("user")) user = (JObject)msg["user"];
            handshake.invokeCallback(user);

            // Invoke initClient callback
            if(this.initCallback != null)
            {
                this.initCallback(msg);
            }
        }

        //The socket disconnect
        private void onDisconnect()
        {
            this.pc.disconnect();
        }

        internal void close()
        {
            transporter.close();

            if (heartBeatService != null) heartBeatService.stop();

            this.state = ProtocolState.closed;
        }
    }
}