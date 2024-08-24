using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sisk.MusicBee.OBS.Tuna {

    public class TunaDataSender {
        private const string TMP_COVER_FILE = "mb_obs_tuna_cover.jpg";
        private readonly HttpClient _client;
        private readonly string _settingsPath;
        private readonly string _tmpCoverPath = Path.Combine(Path.GetTempPath(), TMP_COVER_FILE);
        private byte[] _lastHash;
        private bool _litemode = false;
        private Settings _settings;

        public TunaDataSender(string settingsPath) {
            _settingsPath = settingsPath;
            _settings = Settings.Load("settings.xml");
            _client = new HttpClient();
        }

        public string Host => _settings.Host;
        public int Port => _settings.Port;

        public void Close() {
            try {
                if (File.Exists(Path.Combine(Path.GetTempPath(), TMP_COVER_FILE))) {
                    File.Delete(Path.Combine(Path.GetTempPath(), TMP_COVER_FILE));
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"An error occurred while deleting temp cover file: {ex.Message}");
            }
        }

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
                if (!string.IsNullOrWhiteSpace(songData.CoverBase64)) {
                    WriteTempCover(songData.CoverBase64);
                    songData.Cover = new Uri(_tmpCoverPath).AbsoluteUri;
                }

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
                Console.Error.WriteLine($"An error occurred while sending song data: {ex.Message}");
                throw;
            }
        }

        private void WriteTempCover(string coverBase64) {
            try {
                if (string.IsNullOrWhiteSpace(coverBase64)) {
                    return;
                }

                if (coverBase64.StartsWith("data:image/")) {
                    coverBase64 = coverBase64.Substring(coverBase64.IndexOf(',') + 1);
                }

                var coverBytes = Convert.FromBase64String(coverBase64);
                byte[] currentHash;

                using (var sha256 = SHA256.Create()) {
                    currentHash = sha256.ComputeHash(coverBytes);
                }

                if (_lastHash != null && StructuralComparisons.StructuralEqualityComparer.Equals(_lastHash, currentHash)) {
                    return;
                }

                File.WriteAllBytes(_tmpCoverPath, coverBytes);
                _lastHash = currentHash;
            } catch (Exception ex) {
                Console.Error.WriteLine($"An error occurred while writing cover to temp file: {ex.Message}");
                throw;
            }
        }
    }
}