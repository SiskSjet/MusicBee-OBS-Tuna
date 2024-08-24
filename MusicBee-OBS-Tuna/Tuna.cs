using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sisk.MusicBee.OBS.Tuna {

    public class TunaDataSender {
        private readonly HttpClient _client;
        private readonly string _settingsPath;
        private bool _litemode = false;
        private Settings _settings;

        public TunaDataSender(string settingsPath) {
            _settingsPath = settingsPath;
            _settings = Settings.Load("settings.xml");
            _client = new HttpClient();
        }

        public string Host => _settings.Host;
        public int Port => _settings.Port;

        public void LoadSettings() {
            _settings = Settings.Load(_settingsPath);
        }

        public void SaveSettings(string _tmpHost, int _tmpPort) {
            if (!string.IsNullOrWhiteSpace(_tmpHost)) {
                _settings.Host = _tmpHost;
            }

            if (_tmpPort > 0) {
                _settings.Port = _tmpPort;
            }

            _settings.Save(_settingsPath);
        }

        public async Task SendSongDataAsync(SongData songData) {
            try {
                var request = new HttpRequestMessage {
                    Method = _litemode ? HttpMethod.Options : HttpMethod.Post,
                    RequestUri = new Uri($"http://{_settings.Host}:{_settings.Port}/"),
                    Headers = { { "Accept", "application/json" }, { "Access-Control-Allow-Headers", "*" }, { "Access-Control-Allow-Origin", "*" }, },
                    Content = _litemode ? null : new StringContent(JsonSerializer.Serialize(new SongDataContainer { Data = songData }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, IncludeFields = true }), Encoding.UTF8, "application/json")
                };

                using (var response = await _client.SendAsync(request)) {
                    if (response.IsSuccessStatusCode) {
                        if (_litemode) {
                            _litemode = false;
                            await SendSongDataAsync(songData);
                        }
                    } else {
                        if (!_litemode) {
                            _litemode = true;
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred while sending song data: {ex.Message}");
            }
        }
    }
}