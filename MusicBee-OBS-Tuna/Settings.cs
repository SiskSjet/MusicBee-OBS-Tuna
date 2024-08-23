using System;
using System.IO;
using System.Xml.Serialization;

namespace Sisk.MusicBee.OBS.Tuna {

    [Serializable]
    internal class Settings {
        public const int DEFAULT_PORT = 1608;
        public int Port { get; set; } = DEFAULT_PORT;

        public static Settings Load(string file) {
            if (File.Exists(file)) {
                using (var reader = new StreamReader(file)) {
                    var serializer = new XmlSerializer(typeof(Settings));
                    return (Settings)serializer.Deserialize(reader);
                }
            }

            return new Settings();
        }

        public void Save(string file) {
            using (var writer = new StreamWriter(file)) {
                var serializer = new XmlSerializer(typeof(Settings));
                serializer.Serialize(writer, this);
            }
        }
    }
}