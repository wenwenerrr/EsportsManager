using System;
using UnityEngine;

namespace EsportsManager.Players
{
    /// <summary>Parses and validates <see cref="PlayerConfig"/> JSON (e.g. Assets/Data/PlayerConfig.json).</summary>
    public static class PlayerConfigLoader
    {
        /// <summary>
        /// Parses JSON text into a validated config. Every "bad config" case (null/empty text,
        /// malformed JSON, failed validation) surfaces as <see cref="InvalidOperationException"/>;
        /// a parser exception is kept as InnerException.
        /// </summary>
        /// <example><code>var cfg = PlayerConfigLoader.FromJson(textAsset.text);</code></example>
        public static PlayerConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("PlayerConfig: JSON text is empty.");

            PlayerConfig config;
            try
            {
                config = JsonUtility.FromJson<PlayerConfig>(json);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("PlayerConfig: JSON could not be parsed: " + e.Message, e);
            }
            if (config == null) throw new InvalidOperationException("PlayerConfig: JSON could not be parsed.");
            config.Validate();
            return config;
        }
    }
}
