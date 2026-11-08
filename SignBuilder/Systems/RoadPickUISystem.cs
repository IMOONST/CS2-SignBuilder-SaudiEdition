using System;
using Colossal.UI.Binding;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignBuilderSA.Tools;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// The road a sign is about: picked in the world from the designer ("Pick a road"), or the road the placement tool
    /// last snapped to. Binding group "SignBuilderSA".
    /// Value: roadInfo (JSON { street, streetAr?, district, roadClass: highway|major|medium|local|other, speedLimit
    /// (km/h), lanes (driving lanes one way), source: "pick"|"placement", nonce }, "" until a road is known).
    /// Triggers: pickRoad (start the pick mode; the editor stays open), cancelPickRoad.
    /// </summary>
    public partial class RoadPickUISystem : UISystemBase
    {
        private ValueBinding<string> m_RoadInfo;
        private SignPlacementToolSystem m_Tool;
        private SignBuilderUISystem m_UI;
        private string m_LastKey;

        /// <summary>The road last published (picked or snapped to); null until one is known.</summary>
        public RoadProbe.RoadInfo Current { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Tool = World.GetOrCreateSystemManaged<SignPlacementToolSystem>();
            m_UI = World.GetOrCreateSystemManaged<SignBuilderUISystem>();
            AddBinding(m_RoadInfo = new ValueBinding<string>(SignBuilderUISystem.Group, "roadInfo", ""));
            AddBinding(new TriggerBinding(SignBuilderUISystem.Group, "pickRoad", () =>
            {
                try
                {
                    if (!m_Tool.BeginPickInfo()) m_UI.SetStatus("أنهِ وضع اللوحة الحالي أولاً، ثم اختر طريقاً.");
                }
                catch (Exception e)
                {
                    Mod.log.Warn(e, "Starting the road pick failed");
                    m_UI.SetStatus("حدث خطأ: " + e.Message);
                }
            }));
            AddBinding(new TriggerBinding(SignBuilderUISystem.Group, "cancelPickRoad", () => m_Tool.EndPickInfo()));
        }

        /// <summary>
        /// Publishes <paramref name="info"/> to the UI. Unless <paramref name="force"/> is set, the same road values
        /// (names, class, speed, lanes) are not sent again; a forced publish (a click) always gets a new nonce.
        /// </summary>
        public void Publish(RoadProbe.RoadInfo info, string source, bool force)
        {
            if (info == null) return;
            var key = info.Key;
            Current = info;
            if (!force && key == m_LastKey) return;
            m_LastKey = key;
            var json = new JObject
            {
                ["street"] = info.Street,
                ["district"] = info.District,
                ["roadClass"] = info.RoadClass,
                ["speedLimit"] = info.SpeedLimit,
                ["lanes"] = info.Lanes,
                ["source"] = source,
                ["nonce"] = Guid.NewGuid().ToString("N"),
            };
            if (HasArabic(info.Street)) json["streetAr"] = info.Street;
            m_RoadInfo.Update(json.ToString(Formatting.None));
        }

        private static bool HasArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var c in s)
            {
                if ((c >= '؀' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ') || (c >= 'ࢠ' && c <= 'ࣿ')
                    || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿')) return true;
            }
            return false;
        }
    }
}
