using System;
using System.Drawing;
using System.Windows.Forms;
using Sisk.MusicBee.OBS.Tuna;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MusicBeePlugin {

    public partial class Plugin {
        private PluginInfo _about = new PluginInfo();
        private MusicBeeApiInterface _api;
        private string _tmpHost;
        private int _tmpPort;
        private TunaDataSender _tuna;
        private Timer _updateTimer;

        public void Close(PluginCloseReason reason) {
            _updateTimer?.Stop();
            _tuna = null;
        }

        public bool Configure(IntPtr panelHandle) {
            if (panelHandle != IntPtr.Zero) {
                var configPanel = (Panel)Control.FromHandle(panelHandle);
                configPanel.Controls.Clear();
                var hostLabel = new Label {
                    Text = "Host",
                    AutoSize = true,
                    Location = new Point(0, 0)
                };

                var hostTextBox = new TextBox {
                    Text = _tuna.Host,
                    Bounds = new Rectangle(2, 20, 150, 20)
                };

                hostTextBox.TextChanged += (sender, e) => {
                    _tmpHost = hostTextBox.Text;
                    if (string.IsNullOrWhiteSpace(_tmpHost)) {
                        _tmpHost = Settings.DEFAULT_HOST;
                        hostTextBox.Text = _tmpHost;
                    }
                };

                var portLabel = new Label {
                    Text = "Port",
                    AutoSize = true,
                    Location = new Point(160, 0)
                };

                var portTextBox = new TextBox {
                    Text = _tuna.Port.ToString(),
                    Bounds = new Rectangle(162, 20, 100, 20)
                };

                portTextBox.TextChanged += (sender, e) => {
                    var port = 0;
                    if (int.TryParse(portTextBox.Text, out port)) {
                        _tmpPort = port;
                    }

                    if (_tmpPort < 1) {
                        _tmpPort = Settings.DEFAULT_PORT;
                        portTextBox.Text = _tmpPort.ToString();
                    }
                };

                configPanel.Controls.AddRange(new Control[] { hostLabel, hostTextBox, portLabel, portTextBox });
            }

            return false;
        }

        public PluginInfo Initialise(IntPtr apiInterfacePtr) {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            var name = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            var version = assemblyName.Version;

            _api = new MusicBeeApiInterface();
            _api.Initialise(apiInterfacePtr);
            _about.PluginInfoVersion = PLUGIN_INFO_VERSION;
            _about.Name = name;
            _about.Description = description;
            _about.Author = company;
            _about.TargetApplication = "";
            _about.Type = PluginType.General;
            _about.VersionMajor = (short)version.Major;
            _about.VersionMinor = (short)version.Minor;
            _about.Revision = (short)version.Revision;
            _about.MinInterfaceVersion = MIN_INTERFACE_VERSION;
            _about.MinApiRevision = MIN_API_REVISION;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents);
            _about.ConfigurationPanelHeight = 60;

            _tuna = new TunaDataSender(Path.Combine(_api.Setting_GetPersistentStoragePath(), name, $"settings.xml"));
            _tuna.LoadSettings();

            return _about;
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type) {
            switch (type) {
                case NotificationType.PluginStartup:
                    SetupTimer();
                    break;

                case NotificationType.PlayStateChanged:
                case NotificationType.TrackChanged:
                    SendSongDataToTuna();

                    break;
            }
        }

        public void SaveSettings() {
            _tuna?.SaveSettings(_tmpHost, _tmpPort);
        }

        public void Uninstall() {
            _tuna = null;
        }

        private int SecondsToMilliseconds(int seconds) {
            return seconds * 1000;
        }

        private void SendSongDataToTuna() {
            var state = _api.Player_GetPlayState();
            var position = _api.Player_GetPosition();
            var duration = _api.NowPlaying_GetDuration();
            var cover = _api.NowPlaying_GetArtwork();
            var tags = new MetaDataType[] { MetaDataType.TrackTitle, MetaDataType.Album, MetaDataType.AlbumArtist, MetaDataType.Artists, MetaDataType.Custom1, MetaDataType.Custom2, MetaDataType.Custom4 };
            string[] trackData;
            _api.NowPlaying_GetFileTags(tags, out trackData);

            var playState = "";
            switch (state) {
                case PlayState.Playing:
                    playState = "playing";
                    _updateTimer?.Start();
                    break;

                case PlayState.Paused:
                    playState = "paused";
                    _updateTimer?.Stop();
                    break;

                case PlayState.Stopped:
                    playState = "stopped";
                    _updateTimer?.Stop();
                    break;

                default:
                    playState = "unknown";
                    break;
            }

            var songdata = new SongData() {
                Title = trackData[0],
                Album = trackData[1],
                AlbumArtist = trackData[2],
                Artists = trackData[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                Copyright = trackData[4],
                Url = trackData[5],
                Status = playState,
                Progress = position,
                Duration = duration,
                //Cover = cover,
            };

            var unused = Task.Run(() => _tuna.SendSongDataAsync(songdata));
        }

        private void SetupTimer() {
            _updateTimer = new Timer(500);
            _updateTimer.Elapsed += UpdateTimerElapsedCallback;
            _updateTimer.AutoReset = true;
            _updateTimer.Stop();
        }

        private void UpdateTimerElapsedCallback(object sender, ElapsedEventArgs e) {
            SendSongDataToTuna();
        }
    }
}