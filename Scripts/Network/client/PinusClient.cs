using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using UnityWebSocket;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Pinus.DotNetClient
{
    /// <summary>
    /// network state enum
    /// </summary>
    public enum NetWorkState
    {
        [Description("initial state")]
        CLOSED,

        [Description("connecting server")]
        CONNECTING,

        [Description("server connected")]
        CONNECTED,

        [Description("disconnected with server")]
        DISCONNECTED,

        [Description("connect timeout")]
        TIMEOUT,

        [Description("netwrok error")]
        ERROR
    }

    public class PinusClient : IDisposable
    {
        public event Action<NetWorkState> NetWorkStateChangedEvent;


        private NetWorkState netWorkState = NetWorkState.CLOSED;   //current network state

        private EventManager eventManager;
        private WebSocket socket;
        private Protocol protocol;
        private bool disposed = false;          
        private uint reqId = 1;
       
        private int timeoutSec = 8;    //connect timeout count in second

        public PinusClient()
        {

        }

        // 针对init异步调用，全部完成后返回
        public UniTask<JObject> initAsync(string host, int port)
        {
            var tcs = new UniTaskCompletionSource<JObject>();
            init(host, port, (jobject) =>
            {
                // 执行initCallback时，标记异步操作已完成
                tcs.TrySetResult(jobject);
            });
            return tcs.Task;
        }


        public void init(string host, int port, Action<JObject> initCallback = null)
        {
            init(host, port, initCallback, null, null);
        }


        public void init(string host, int port, Action<JObject> initCallback = null, JObject user = null, Action<JObject> handshakeCallback = null)
        {
            eventManager = new EventManager();
            NetWorkChanged(NetWorkState.CONNECTING);

            string address = "ws://" + host + ":" + port.ToString();
            this.socket = new WebSocket(address);
            Debug.Log("websocket address is:" + address);
            this.protocol = new Protocol(this, this.socket, initCallback);
            this.socket.OnOpen += OnOpen;
            this.socket.OnClose += OnClose;
            socket.OnError += OnError;

            // 启动协议
            this.protocol.start(user, handshakeCallback);

            // 超时处理
            this.handleTimeout();
        }

        // Unity 使用异步方法对超时进行处理
        async private void handleTimeout()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(this.timeoutSec));
            if (netWorkState != NetWorkState.CONNECTED && netWorkState != NetWorkState.ERROR)
            {
                NetWorkChanged(NetWorkState.TIMEOUT);
                Dispose();
            }
        }

        // 连接websocket 成功 
        private void OnOpen(object sender, OpenEventArgs e)
        {
            NetWorkChanged(NetWorkState.CONNECTED);
        }

        // websocket 连接关闭
        private void OnClose(object sender, CloseEventArgs e)
        {
            NetWorkChanged(NetWorkState.CLOSED);
        }

        // 连接错误
        private void OnError(object sender, ErrorEventArgs e)
        {
            NetWorkChanged(NetWorkState.ERROR);
        }

        /// <summary>
        /// 网络状态变化
        /// </summary>
        /// <param name="state"></param>
        private void NetWorkChanged(NetWorkState state)
        {
            netWorkState = state;

            if (NetWorkStateChangedEvent != null)
            {
                NetWorkStateChangedEvent(state);
            }
        }

        private JObject emptyMsg = new JObject();
        public void request(string route, Action<JObject> action)
        {
            this.request(route, emptyMsg, action);
        }



        public void request(string route, JObject msg, Action<JObject> action)
        {
            this.eventManager.AddCallBack(reqId, action);
            protocol.send(route, reqId, msg);

            reqId++;
        }

        // 异步request
        // return 返回JObject对象
        public UniTask<JObject> requestAsync(string route, JObject msg)
        {
            var tcs = new UniTaskCompletionSource<JObject>();
            request(route, msg, (jobject) =>
            {
                // 执行initCallback时，标记异步操作已完成
                tcs.TrySetResult(jobject);
            });
            return tcs.Task;
        }

        public void notify(string route, JObject msg)
        {
            protocol.send(route, msg);
        }


        public void on(string eventName, Action<JObject> action)
        {
            eventManager.AddOnEvent(eventName, action);
        }

        internal void processMessage(Message msg)
        {
            if (msg.type == MessageType.MSG_RESPONSE)
            {
                eventManager.InvokeCallBack(msg.id, msg.data);
            }
            else if (msg.type == MessageType.MSG_PUSH)
            {
                eventManager.InvokeOnEvent(msg.route, msg.data);
            }
        }

        public void disconnect()
        {
            Dispose();
            NetWorkChanged(NetWorkState.DISCONNECTED);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                // free managed resources
                if (this.protocol != null)
                {
                    this.protocol.close();
                }

                if (this.eventManager != null)
                {
                    this.eventManager.Dispose();
                }

                try
                {
                    if(this.socket != null)
                    {
                        this.socket.CloseAsync();
                    }
                    this.socket = null;
                }
                catch (Exception)
                {
                    //todo : 有待确定这里是否会出现异常，这里是参考之前官方github上pull request。emptyMsg
                }

                this.disposed = true;
            }
        }
    }
}