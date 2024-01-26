using UnityEngine;
using Pinus.DotNetClient;
using Newtonsoft.Json.Linq;

public class main : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        // 在建立WebSocket连接之前设置回调
        string host = "127.0.0.1";//(www.xxx.com/127.0.0.1/::1/localhost etc.)
        int port = 3010;
        PinusClient pclient = new PinusClient();

        //listen on network state changed event
        pclient.NetWorkStateChangedEvent += (state) =>
        {
            Debug.Log("state changed " + state);
        };

        await pclient.initAsync(host, port);
        var msg = JObject.Parse(@"{
                'uid': 12345
            }");

        JObject resp = await pclient.requestAsync("gate.gateHandler.queryEntry", msg);
        Debug.Log("收到服务端返回" + resp.ToString());

        /*
        pclient.init(host, port, (data) =>
        {
            //The user data is the handshake user params
            JObject user = JObject.Parse("{}");
            Debug.Log("发送消息");
            var msg = JObject.Parse(@"{
                'uid': 12345
            }");
            pclient.request("gate.gateHandler.queryEntry", msg, (resp) =>
            {
                //process the data
                Debug.Log("收到服务端返回" + resp.ToString());
            });

        });
        */
    }

    // Update is called once per frame
    void Update()
    {

    }
}