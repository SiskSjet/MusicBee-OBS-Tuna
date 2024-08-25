/**
 * @type {import('semantic-release').GlobalConfig}
 */
export default {
    branches: ["main", "next"],
    plugins: [
        'semantic-release-gitmoji',
        [
            "@semantic-release/github",
            {
                "assets": [
                    { "path": "MusicBee-OBS-Tuna/bin/x86/Release/net48/mb_MusicBee-OBS-Tuna.dll", "label": "mb_MusicBee-OBS-Tuna.dll" },
                ]
            }
        ]
    ]
};