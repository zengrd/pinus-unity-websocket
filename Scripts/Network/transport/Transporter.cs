using System;
using UnityWebSocket;

namespace Pinus.DotNetClient
{

    public class Transporter
    {
        private WebSocket socket;
        private Action<byte[]> messageProcesser;
        internal Action onDisconnect = null;

        //Used for get message
        private TransportState transportState = TransportState.start;


        public Transporter(Protocol protocol, WebSocket socket)
        {
            this.socket = socket;
            this.messageProcesser = protocol.processMessage;
        }


        public void start()
        {
            socket.OnOpen += OnOpen;
            socket.OnMessage += OnMessage;
            socket.OnClose += OnClose;
            socket.OnError += OnError;
            socket.ConnectAsync();
        }


        private void OnOpen(object sender, OpenEventArgs e)
        {
            UnityEngine.Debug.Log("OnOpen");
            transportState = TransportState.opened;
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsBinary)
            {
                UnityEngine.Debug.Log("OnMessage:" + e.Data);
                this.messageProcesser.Invoke(e.RawData);
            }
            else if (e.IsText)
            {
                // 当获取的内容是文本的内容，需要报错，都是使用probuf进行传输的
                UnityEngine.Debug.Log("Error Message Type:" + e.Data);
            }
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            UnityEngine.Debug.Log("OnClose");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            // 打印socket 返回的错误
            UnityEngine.Debug.Log(e.Message);
        }


        public void send(byte[] buffer)
        {
            if (this.transportState == TransportState.opened)
            {
                socket.SendAsync(buffer);
            }
        }

        internal void close()
        {
            this.transportState = TransportState.closed;
            socket.CloseAsync();
        }


    }
}