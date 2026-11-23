using Xunit;

namespace AdbAux.Tests
{
    /// <summary>
    /// Parsing of "adb devices -l" output (Windows prints no "usb:" field). Serial numbers are made up;
    /// 192.0.2.x is an address range reserved for documentation.
    /// </summary>
    public class AdbParseTests
    {
        [Fact]
        public void FindsUsbPhoneAndIgnoresDeviceOnWiFi()
        {
            string output = "List of devices attached\r\n"
                + "192.0.2.10:5555        device product:tv_box model:TV_BOX device:x transport_id:2\r\n"
                + "AB12CD34EF5            device product:a36xnsxx model:SM_A366E device:a36x transport_id:3\r\n";

            PhoneInfo phone = AdbClient.Parse(output);

            Assert.NotNull(phone);
            Assert.Equal("AB12CD34EF5", phone.Serial);
            Assert.Equal("SM_A366E", phone.Model);
            Assert.Equal("SM-A366E", phone.DisplayModel);
            Assert.True(phone.IsReady);
        }

        [Fact]
        public void ReportsUnauthorisedPhone()
        {
            PhoneInfo phone = AdbClient.Parse("List of devices attached\nR58N12ABCDE\tunauthorized transport_id:1\n");

            Assert.NotNull(phone);
            Assert.Equal("unauthorized", phone.State);
            Assert.False(phone.IsReady);
        }

        [Fact]
        public void IgnoresEmulatorAndWirelessDebugging()
        {
            string output = "List of devices attached\n"
                + "emulator-5554 device product:sdk model:sdk_phone device:emu\n"
                + "adb-R58N12ABCDE-AbCdEf._adb-tls-connect._tcp device product:x model:SM_S938B device:x\n";

            Assert.Null(AdbClient.Parse(output));
        }

        [Fact]
        public void MediaPlayingFromDumpsys()
        {
            string playing = "Sessions Stack - have 2 sessions:\n"
                + "    com.amazon.mp3/MediaSession (userId=0)\n"
                + "      state=PlaybackState {state=PLAYING(3), position=1234, buffered position=0, speed=1.0}\n"
                + "    com.google.android.youtube/youtube (userId=0)\n"
                + "      state=PlaybackState {state=PAUSED(2), position=10, buffered position=0, speed=0.0}\n";
            string paused = "      state=PlaybackState {state=PAUSED(2), position=10, buffered position=0, speed=0.0}\n";
            string olderAndroid = "      state=PlaybackState {state=3, position=5000, buffered position=0, speed=1.0}\n";

            Assert.True(AdbClient.IsMediaPlaying(playing));
            Assert.False(AdbClient.IsMediaPlaying(paused));
            Assert.True(AdbClient.IsMediaPlaying(olderAndroid));
            Assert.False(AdbClient.IsMediaPlaying("Sessions Stack - have 0 sessions:"));
            Assert.False(AdbClient.IsMediaPlaying(null));
        }

        [Fact]
        public void NoDevices()
        {
            Assert.Null(AdbClient.Parse("List of devices attached\r\n\r\n"));
        }
    }
}
