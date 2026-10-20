using System;
using System.Collections.Generic;
using EsportsManager.Players;
using UnityEngine;

namespace EsportsManager.Events
{
    /// <summary>
    /// Event texts by key (Assets/Data/event_text_zh.json, Story 006). Strictly checked like the configs: {"texts":[{"key","text"}]},
    /// keys unique and non-empty. Placeholders {player} / {partner} are filled by the UI.
    /// </summary>
    public sealed class EventTextTable
    {
        private readonly Dictionary<string, string> _texts;

        private EventTextTable(Dictionary<string, string> texts) { _texts = texts; }

        /// <summary>Number of entries.</summary>
        public int Count => _texts.Count;

        /// <summary>Text of a key, or the key itself in brackets when missing (visible, never throws).</summary>
        public string Get(string key) => key != null && _texts.TryGetValue(key, out var t) ? t : "[" + key + "]";

        /// <summary>True when the key exists.</summary>
        public bool Has(string key) => key != null && _texts.ContainsKey(key);

        /// <summary>Every key the config uses (titles, texts, option labels) that this table lacks, in config order.</summary>
        public List<string> MissingKeys(EventConfig config)
        {
            var missing = new List<string>();
            foreach (var e in config.events)
            {
                if (!Has(e.titleKey)) missing.Add(e.titleKey);
                if (!Has(e.textKey)) missing.Add(e.textKey);
                foreach (var o in e.options) if (!Has(o.labelKey)) missing.Add(o.labelKey);
            }
            return missing;
        }

        /// <summary>Parses the table; malformed shape, empty or duplicate keys throw <see cref="InvalidOperationException"/>.</summary>
        public static EventTextTable FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("EventTextTable: JSON text is empty.");
            JsonShapeChecker.Check(json, typeof(TableDto), "EventTextTable");
            var dto = JsonUtility.FromJson<TableDto>(json);
            var map = new Dictionary<string, string>();
            for (int i = 0; i < dto.texts.Length; i++)
            {
                var e = dto.texts[i];
                if (string.IsNullOrEmpty(e.key)) throw new InvalidOperationException("EventTextTable: texts[" + i + "].key is empty.");
                if (map.ContainsKey(e.key)) throw new InvalidOperationException("EventTextTable: texts[" + i + "].key '" + e.key + "' is duplicated.");
                map[e.key] = e.text ?? "";
            }
            return new EventTextTable(map);
        }

        [Serializable] private class EntryDto { public string key; public string text; }
        [Serializable] private class TableDto { public EntryDto[] texts; }
    }
}
