using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WindowsIsland
{
    // The wire format of Discord's local RPC (the pipe "discord-ipc-N"), apart from any connection so it can be tested.
    // A frame is a 32-bit opcode, a 32-bit length (both little-endian) and that many bytes of JSON.
    static class DiscordRpc
    {
        public const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;
        public const int MaxPayload = 64 * 1024;

        public static byte[] Encode(int opcode, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(opcode).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            return frame;
        }

        // Takes one whole frame from the front of the buffer. False when more bytes are needed, or when the
        // length is absurd (consumed = -1, so the caller drops the connection).
        public static bool TryDecode(byte[] buffer, int count, out int opcode, out string json, out int consumed)
        {
            opcode = 0;
            json = null;
            consumed = 0;
            if (count < 8) return false;
            opcode = BitConverter.ToInt32(buffer, 0);
            int length = BitConverter.ToInt32(buffer, 4);
            if (length < 0 || length > MaxPayload) { consumed = -1; return false; }
            if (count < 8 + length) return false;
            json = Encoding.UTF8.GetString(buffer, 8, length);
            consumed = 8 + length;
            return true;
        }

        public static string HandshakeJson(string clientId)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                using (Utf8JsonWriter json = new Utf8JsonWriter(buffer))
                {
                    json.WriteStartObject();
                    json.WriteNumber("v", 1);
                    json.WriteString("client_id", clientId);
                    json.WriteEndObject();
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        // {"cmd":..., "args":{...}, "evt":..., "nonce":...}. The args are written by the caller.
        public static string Command(string cmd, Action<Utf8JsonWriter> args, string evt, string nonce)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                using (Utf8JsonWriter json = new Utf8JsonWriter(buffer))
                {
                    json.WriteStartObject();
                    json.WriteString("cmd", cmd);
                    json.WritePropertyName("args");
                    json.WriteStartObject();
                    if (args != null) args(json);
                    json.WriteEndObject();
                    if (evt != null) json.WriteString("evt", evt);
                    json.WriteString("nonce", nonce);
                    json.WriteEndObject();
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        // What a reply or an event says about the voice settings: self mute and self deafen.
        public static bool TryVoice(string json, out bool mute, out bool deaf)
        {
            mute = deaf = false;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement data, value;
                    if (!document.RootElement.TryGetProperty("data", out data) || data.ValueKind != JsonValueKind.Object) return false;
                    bool found = false;
                    if (data.TryGetProperty("mute", out value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)) { mute = value.GetBoolean(); found = true; }
                    if (data.TryGetProperty("deaf", out value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)) { deaf = value.GetBoolean(); found = true; }
                    if (deaf) mute = true; // Deafened always means muted too
                    return found;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // Discord ends a refused connection with a CLOSE frame: {"code":4000,"message":"Invalid Client ID"}.
        public static string CloseText(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement code, message;
                    string text = document.RootElement.TryGetProperty("message", out message) && message.ValueKind == JsonValueKind.String ? message.GetString() : "";
                    int number;
                    if (document.RootElement.TryGetProperty("code", out code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out number)) text = (text + " (" + number + ")").Trim();
                    return text;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        // The command, event, nonce and error (code and message) of one message.
        public sealed class Message
        {
            public string Cmd, Evt, Nonce, Text, RawJson;
            public int ErrorCode;
            public bool IsError;
            public string Code;      // The authorization code of an AUTHORIZE reply
        }

        public static Message Parse(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement, value, data;
                    if (root.ValueKind != JsonValueKind.Object) return null;
                    Message message = new Message { RawJson = json };
                    if (root.TryGetProperty("cmd", out value) && value.ValueKind == JsonValueKind.String) message.Cmd = value.GetString();
                    if (root.TryGetProperty("evt", out value) && value.ValueKind == JsonValueKind.String) message.Evt = value.GetString();
                    if (root.TryGetProperty("nonce", out value) && value.ValueKind == JsonValueKind.String) message.Nonce = value.GetString();
                    message.IsError = message.Evt == "ERROR";
                    if (root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object)
                    {
                        if (data.TryGetProperty("code", out value))
                        {
                            int number;
                            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number)) message.ErrorCode = number;
                            else if (value.ValueKind == JsonValueKind.String) message.Code = value.GetString();
                        }
                        if (data.TryGetProperty("message", out value) && value.ValueKind == JsonValueKind.String) message.Text = value.GetString();
                    }
                    return message;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
