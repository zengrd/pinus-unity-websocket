using System;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Pinus.Protobuf
{
    public class MsgEncoder
    {
        private JObject protos; //The message format(like .proto file)
        private Util util;

        public MsgEncoder(JObject protos)
        {
            if (protos == null)
                this.protos = new JObject();
            else
                this.protos = protos;
            util = new Util();
        }

        /// <summary>
        /// Encode the message from server.
        /// </summary>
        /// <param name='route'>
        /// Route.
        /// </param>
        /// <param name='msg'>
        /// Message.
        /// </param>
        public byte[] encode(string route, JObject msg)
        {
            byte[] returnByte = null;
            JObject proto = util.GetProtoMessage(protos, route);
            if (proto != null)
            {
                int length = Encoder.byteLength(msg.ToString()) * 2;
                int offset = 0;
                byte[] buff = new byte[length];
                offset = encodeMsg(buff, offset, proto, msg);
                returnByte = new byte[offset];
                for (int i = 0; i < offset; i++)
                {
                    returnByte[i] = buff[i];
                }
            }
            return returnByte;
        }

        /// <summary>
        /// Encode the message.
        /// </summary>
        private int encodeMsg(byte[] buffer, int offset, JObject proto, JObject msg)
        {
            var msgDict = msg.ToObject<Dictionary<string, object>>();
            foreach (KeyValuePair<string, object> pair in msgDict)
            {
                var key = pair.Key;
                JObject protoField = util.GetField(proto, key);
                if (protoField is null)
                    continue;

                var rule = protoField["rule"];
                var keyType = protoField["keyType"];
                if (keyType != null)
                {
                    // map
                    var mapObj = msg[key];
                    if (mapObj != null)
                    {
                        var dict = mapObj.ToObject<Dictionary<object, object>>();
                        offset = encodeMap(dict, protoField, offset, buffer);
                    }
                }
                else if (rule != null)
                {
                    if (rule.ToString() == "repeated")
                    {
                        // array
                        var arr = msg[key];
                        if (arr != null)
                        {
                            var ls = arr.ToObject<List<object>>();
                            if (ls.Count > 0)
                            {
                                offset = encodeArray(ls, protoField, offset, buffer);
                            }
                        }
                    }
                }
                else
                {
                    var valueType = protoField["type"];
                    var valueId = protoField["id"];
                    if (!(valueType is null) && !(valueId is null))
                    {
                        offset = writeBytes(buffer, offset, encodeTag(valueType.ToString(), Convert.ToInt32(valueId)));
                        offset = encodeProp(msg[key], valueType.ToString(), offset, buffer);
                    }
                }
            }
            return offset;
        }

        /// <summary>
        /// Encode the array type.
        /// </summary>
        private int encodeArray(List<object> msg, JObject protoField, int offset, byte[] buffer)
        {
            var valueType = protoField["type"];
            var valueId = protoField["id"];
            if (valueType != null && valueId != null)
            {
                if (util.isSimpleType(valueType.ToString()))
                {
                    // 简单类型，packed 编码
                    int length = Encoder.byteLength(msg.ToString()) * 2;
                    int subOffset = 0;
                    byte[] subBuff = new byte[length];
                    offset = writeBytes(buffer, offset, encodeTag("repeated", Convert.ToInt32(valueId)));
                    foreach (object item in msg)
                    {
                        subOffset = encodeProp(item, valueType.ToString(), subOffset, subBuff);
                    }
                    offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)subOffset));
                    for (var i = 0; i < subOffset; i++)
                    {
                        buffer[offset + i] = subBuff[i];
                    }
                    offset += subOffset;
                }
                else
                {
                    foreach (object item in msg)
                    {
                        int length = Encoder.byteLength(msg.ToString()) * 2;
                        int subOffset = 0;
                        byte[] subBuff = new byte[length];
                        offset = writeBytes(buffer, offset, encodeTag("repeated", Convert.ToInt32(valueId)));
                        subOffset = encodeProp(item, valueType.ToString(), subOffset, subBuff);
                        for (var i = 0; i < subOffset; i++)
                        {
                            buffer[offset + i] = subBuff[i];
                        }
                        offset += subOffset;
                    }
                }

            }
            return offset;
        }

        private int encodeMap(Dictionary<object, object> msg, JObject protoField, int offset, byte[] buffer)
        {
            string keyType = (string)protoField["keyType"];
            string valueType = (string)protoField["type"];
            int valueId = (int)protoField["id"];
            if (keyType != null && valueType != null)
            {
                foreach (var item in msg)
                {
                    int length = Encoder.byteLength(msg.ToString()) * 2;
                    int subOffset = 0;
                    byte[] subBuff = new byte[length];

                    // write tag
                    offset = writeBytes(buffer, offset, encodeTag("map", Convert.ToInt32(valueId)));

                    // encode value
                    subOffset = writeBytes(subBuff, subOffset, encodeTag(keyType, 1));
                    subOffset = encodeProp(item.Key, keyType.ToString(), subOffset, subBuff);
                    subOffset = writeBytes(subBuff, subOffset, encodeTag(valueType, 2));
                    subOffset = encodeProp(item.Value, valueType.ToString(), subOffset, subBuff);

                    // write length
                    offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)subOffset));

                    // write value
                    for (var i = 0; i < subOffset; i++)
                    {
                        buffer[offset + i] = subBuff[i];
                    }
                    offset += subOffset;
                }
            }
            return offset;
        }

        /// <summary>
        /// Encode each item in message.
        /// </summary>
        private int encodeProp(object value, string type, int offset, byte[] buffer)
        {
            switch (type)
            {
                case "uint32":
                    writeUInt32(buffer, ref offset, value);
                    break;
                case "int32":
                case "sint32":
                    writeInt32(buffer, ref offset, value);
                    break;
                case "uint64":
                    writeUInt64(buffer, ref offset, value);
                    break;
                case "int64":
                case "sint64":
                    writeInt64(buffer, ref offset, value);
                    break;
                case "float":
                    writeFloat(buffer, ref offset, value);
                    break;
                case "double":
                    writeDouble(buffer, ref offset, value);
                    break;
                case "string":
                    writeString(buffer, ref offset, value);
                    break;
                case "bool":
                    writeBool(buffer, ref offset, value);
                    break;
                default:
                    encodeObject(type, value, ref offset, buffer);
                    break;
            }
            return offset;
        }

        private void encodeObject(string type, object msg, ref int offset, byte[] buffer)
        {
            JObject subProto = util.GetProtoMessage(protos, type);
            if (subProto != null)
            {
                byte[] subBuff = new byte[Encoder.byteLength(msg.ToString())];
                int subOffset = 0;
                subOffset = encodeMsg(subBuff, subOffset, subProto, (JObject)msg);
                offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)subOffset));
                for (int i = 0; i < subOffset; i++)
                {
                    buffer[offset + i] = subBuff[i];
                }
                offset += subOffset;
            }
        }

        //Encode string.
        private void writeString(byte[] buffer, ref int offset, object value)
        {
            int le = Encoding.UTF8.GetByteCount(value.ToString());
            offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)le));
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToString());
            writeBytes(buffer, offset, bytes);
            offset += le;
        }

        //Encode double.
        private void writeDouble(byte[] buffer, ref int offset, object value)
        {
            WriteRawLittleEndian64(buffer, offset, (ulong)BitConverter.DoubleToInt64Bits(double.Parse(value.ToString())));
            offset += 8;
        }

        //Encode float.
        private void writeFloat(byte[] buffer, ref int offset, object value)
        {
            writeBytes(buffer, offset, Encoder.encodeFloat(float.Parse(value.ToString())));
            offset += 4;
        }

        private void writeBool(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeBool(value));
        }

        ////Encode UInt32.
        private void writeUInt32(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeUInt32(value.ToString()));
        }

        //Encode Int32
        private void writeInt32(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeSInt32(value.ToString()));
        }

        private void writeUInt64(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeUInt64(value.ToString()));
        }

        private void writeInt64(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeSInt64(value.ToString()));
        }

        //Write bytes to buffer.
        private int writeBytes(byte[] buffer, int offset, byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                buffer[offset] = bytes[i];
                offset++;
            }
            return offset;
        }

        //Encode tag.
        private byte[] encodeTag(string type, int tag)
        {
            int flag = util.containType(type);
            return Encoder.encodeUInt32((uint)(tag << 3 | flag));
        }


        private void WriteRawLittleEndian64(byte[] buffer, int offset, ulong value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 32);
            buffer[offset++] = (byte)(value >> 40);
            buffer[offset++] = (byte)(value >> 48);
            buffer[offset++] = (byte)(value >> 56);
        }
    }
}
