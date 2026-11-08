using System.Text;
using Xunit;

namespace WindowsIsland.Tests
{
    public class DiscordRpcTests
    {
        [Fact]
        public void AFrameIsAnOpcodeALengthAndTheJson()
        {
            byte[] frame = DiscordRpc.Encode(DiscordRpc.OpHandshake, "{\"v\":1}");
            Assert.Equal(8 + 7, frame.Length);
            Assert.Equal(0, System.BitConverter.ToInt32(frame, 0));
            Assert.Equal(7, System.BitConverter.ToInt32(frame, 4));
            Assert.Equal("{\"v\":1}", Encoding.UTF8.GetString(frame, 8, 7));
        }

        [Fact]
        public void ADecodedFrameGivesBackWhatWasEncoded()
        {
            byte[] frame = DiscordRpc.Encode(DiscordRpc.OpFrame, "{\"cmd\":\"PING\"}");
            int opcode, consumed;
            string json;
            Assert.True(DiscordRpc.TryDecode(frame, frame.Length, out opcode, out json, out consumed));
            Assert.Equal(DiscordRpc.OpFrame, opcode);
            Assert.Equal("{\"cmd\":\"PING\"}", json);
            Assert.Equal(frame.Length, consumed);
        }

        [Fact]
        public void AnIncompleteFrameWaitsForMoreBytes()
        {
            byte[] frame = DiscordRpc.Encode(DiscordRpc.OpFrame, "{\"cmd\":\"PING\"}");
            int opcode, consumed;
            string json;
            Assert.False(DiscordRpc.TryDecode(frame, frame.Length - 1, out opcode, out json, out consumed));
            Assert.Equal(0, consumed);
            Assert.False(DiscordRpc.TryDecode(frame, 5, out opcode, out json, out consumed));
        }

        [Fact]
        public void OnlyTheFirstOfTwoFramesIsTaken()
        {
            byte[] one = DiscordRpc.Encode(DiscordRpc.OpFrame, "{\"a\":1}");
            byte[] two = DiscordRpc.Encode(DiscordRpc.OpFrame, "{\"b\":2}");
            byte[] both = new byte[one.Length + two.Length];
            one.CopyTo(both, 0);
            two.CopyTo(both, one.Length);
            int opcode, consumed;
            string json;
            Assert.True(DiscordRpc.TryDecode(both, both.Length, out opcode, out json, out consumed));
            Assert.Equal("{\"a\":1}", json);
            Assert.Equal(one.Length, consumed);
        }

        [Fact]
        public void AnAbsurdLengthIsRefused()
        {
            byte[] frame = new byte[8];
            System.BitConverter.GetBytes(1).CopyTo(frame, 0);
            System.BitConverter.GetBytes(int.MaxValue).CopyTo(frame, 4);
            int opcode, consumed;
            string json;
            Assert.False(DiscordRpc.TryDecode(frame, frame.Length, out opcode, out json, out consumed));
            Assert.Equal(-1, consumed);
        }

        [Fact]
        public void TheHandshakeNamesTheClient()
        {
            Assert.Equal("{\"v\":1,\"client_id\":\"123\"}", DiscordRpc.HandshakeJson("123"));
        }

        [Fact]
        public void ACommandCarriesItsArgsAndNonce()
        {
            string json = DiscordRpc.Command("AUTHENTICATE", w => w.WriteString("access_token", "abc"), null, "n1");
            DiscordRpc.Message message = DiscordRpc.Parse(json);
            Assert.Equal("AUTHENTICATE", message.Cmd);
            Assert.Equal("n1", message.Nonce);
            Assert.Contains("\"access_token\":\"abc\"", json);
        }

        [Fact]
        public void ASubscriptionNamesItsEvent()
        {
            string json = DiscordRpc.Command("SUBSCRIBE", null, "VOICE_SETTINGS_UPDATE", "n2");
            Assert.Equal("VOICE_SETTINGS_UPDATE", DiscordRpc.Parse(json).Evt);
        }

        [Theory]
        [InlineData("{\"cmd\":\"GET_VOICE_SETTINGS\",\"data\":{\"mute\":true,\"deaf\":false}}", true, false)]
        [InlineData("{\"evt\":\"VOICE_SETTINGS_UPDATE\",\"data\":{\"mute\":false,\"deaf\":false}}", false, false)]
        [InlineData("{\"evt\":\"VOICE_SETTINGS_UPDATE\",\"data\":{\"mute\":false,\"deaf\":true}}", true, true)]
        public void TheVoiceStateIsReadFromAReplyOrAnEvent(string json, bool mute, bool deaf)
        {
            bool m, d;
            Assert.True(DiscordRpc.TryVoice(json, out m, out d));
            Assert.Equal(mute, m);
            Assert.Equal(deaf, d);
        }

        [Fact]
        public void AMessageWithoutTheVoiceFieldsIsNotAVoiceState()
        {
            bool m, d;
            Assert.False(DiscordRpc.TryVoice("{\"cmd\":\"AUTHENTICATE\",\"data\":{\"user\":{}}}", out m, out d));
            Assert.False(DiscordRpc.TryVoice("not json", out m, out d));
        }

        [Fact]
        public void AnErrorReplyIsRecognized()
        {
            DiscordRpc.Message message = DiscordRpc.Parse("{\"cmd\":\"AUTHORIZE\",\"evt\":\"ERROR\",\"data\":{\"code\":4000,\"message\":\"Invalid scope\"},\"nonce\":\"x\"}");
            Assert.True(message.IsError);
            Assert.Equal(4000, message.ErrorCode);
            Assert.Equal("Invalid scope", message.Text);
        }

        [Fact]
        public void TheAuthorizationCodeIsReadFromTheReply()
        {
            DiscordRpc.Message message = DiscordRpc.Parse("{\"cmd\":\"AUTHORIZE\",\"data\":{\"code\":\"abc123\"},\"nonce\":\"x\"}");
            Assert.False(message.IsError);
            Assert.Equal("abc123", message.Code);
        }

        [Fact]
        public void ARefusedConnectionExplainsItself()
        {
            Assert.Equal("Invalid Client ID (4000)", DiscordRpc.CloseText("{\"code\":4000,\"message\":\"Invalid Client ID\"}"));
            Assert.Equal("", DiscordRpc.CloseText("not json"));
        }
    }
}
