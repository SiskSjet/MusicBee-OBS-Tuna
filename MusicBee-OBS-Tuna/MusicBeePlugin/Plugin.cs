using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using Sisk.MusicBee.OBS.Tuna;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Threading.Tasks;
using System.Text.Json;

namespace MusicBeePlugin {

    public partial class Plugin {
        private PluginInfo _about = new PluginInfo();
        private MusicBeeApiInterface _api;
        private string _settingsFile = "settings.xml";
        private string _tmpHost;
        private int _tmpPort;
        private TunaDataSender _tuna;

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason) {
            _tuna = null;
        }

        public bool Configure(IntPtr panelHandle) {
            // save any persistent settings in a sub-folder of this path
            var dataPath = _api.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
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
            _about.TargetApplication = "";   //  the name of a Plugin Storage device or panel header for a dockable panel
            _about.Type = PluginType.General;
            _about.VersionMajor = (short)version.Major;  // your plugin version
            _about.VersionMinor = (short)version.Minor;
            _about.Revision = (short)version.Revision;
            _about.MinInterfaceVersion = MIN_INTERFACE_VERSION;
            _about.MinApiRevision = MIN_API_REVISION;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents);
            _about.ConfigurationPanelHeight = 60;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            _settingsFile = Path.Combine(_api.Setting_GetPersistentStoragePath(), $"{assemblyName.Name}.xml");
            _tuna = new TunaDataSender(_settingsFile);
            _tuna.LoadSettings();

            return _about;
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type) {
            // perform some action depending on the notification type
            switch (type) {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    switch (_api.Player_GetPlayState()) {
                        case PlayState.Playing:
                        case PlayState.Paused:
                        case PlayState.Stopped:
                            // ...
                            // send song data to OBS-Tuna
                            SendSongDataToTuna();

                            break;
                    }
                    break;

                case NotificationType.PlayStateChanged:
                case NotificationType.TrackChanged:
                    var artist = _api.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    // send song data to OBS-Tuna
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
            var position = SecondsToMilliseconds(_api.Player_GetPosition());
            var duration = SecondsToMilliseconds(_api.NowPlaying_GetDuration());
            var cover = _api.NowPlaying_GetArtwork();
            var tags = new MetaDataType[] { MetaDataType.TrackTitle, MetaDataType.Album, MetaDataType.AlbumArtist, MetaDataType.Artists, MetaDataType.Custom1, MetaDataType.Custom2, MetaDataType.Custom4 };
            string[] trackData;
            _api.NowPlaying_GetFileTags(tags, out trackData);

            var songdata = new SongData() {
                Title = trackData[0],
                Album = trackData[1],
                AlbumArtist = trackData[2],
                Artists = trackData[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                Copyright = trackData[4],
                Url = trackData[5],
                Status = state.ToString(),
                Progress = position,
                Cover = cover,
            };

            var unused = Task.Run(() => _tuna.SendSongDataAsync(songdata));
        }
    }
}