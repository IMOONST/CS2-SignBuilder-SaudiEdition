using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SignBuilderSA.Assemblies
{
    /// <summary>
    /// Speed feedback signs ("Your Speed"): a design marked with
    /// <c>"feedback": { "limit": 60, "state": "ok" }</c> (optionally also <c>"ok"</c> / <c>"slow"</c>: the LED message of
    /// each state) is built twice, once per state, and <see cref="Systems.SpeedFeedbackSystem"/> swaps a placed sign
    /// between the two as traffic passes it. The player designs one state; the other is derived here.
    /// </summary>
    public static class SpeedFeedback
    {
        public const string Key = "feedback";
        public const string Ok = "ok";
        public const string Slow = "slow";
        public const float kDefaultLimit = 60f;
        public const string kOkMessage = "THANK YOU";
        public const string kSlowMessage = "SLOW DOWN";
        private const string kHappy = "face-happy";
        private const string kSad = "face-sad";

        /// <summary>Whether <paramref name="design"/> is a speed feedback sign.</summary>
        public static bool IsFeedback(JObject design) => design?[Key] is JObject;

        /// <summary>Whether any sign of a structure is a speed feedback sign.</summary>
        public static bool IsFeedback(IEnumerable<JObject> designs) => designs != null && designs.Any(IsFeedback);

        /// <summary>The state a design shows: "ok" or "slow" (anything else counts as "ok").</summary>
        public static string StateOf(JObject design)
        {
            var state = (string)(design?[Key] as JObject)?["state"];
            return state == Slow ? Slow : Ok;
        }

        /// <summary>The state a structure shows: its first feedback sign's.</summary>
        public static string StateOf(IEnumerable<JObject> designs)
        {
            var first = designs?.FirstOrDefault(IsFeedback);
            return first == null ? Ok : StateOf(first);
        }

        /// <summary>
        /// The speed limit in km/h a design reacts to: the number on its speed shield (network "SPD"), which is what the
        /// player sees and edits; otherwise the marker's "limit".
        /// </summary>
        public static float LimitOf(JObject design)
        {
            if (design == null) return kDefaultLimit;
            foreach (var item in Items(design))
            {
                if ((string)item["type"] != "shield" || (string)item["network"] != "SPD") continue;
                var number = ToWesternDigits((string)item["number"] ?? "").Trim();
                if (float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var shown) && shown > 0f && shown < 400f) return shown;
            }
            var token = (design[Key] as JObject)?["limit"];
            if (token == null) return kDefaultLimit;
            float value;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) value = token.Value<float>();
            else if (!float.TryParse((string)token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return kDefaultLimit;
            return value > 0f ? value : kDefaultLimit;
        }

        public static float LimitOf(IEnumerable<JObject> designs)
        {
            var first = designs?.FirstOrDefault(IsFeedback);
            return first == null ? kDefaultLimit : LimitOf(first);
        }

        public static string Other(string state) => state == Slow ? Ok : Slow;

        /// <summary>Arabic-Indic and Eastern Arabic-Indic digits (Saudi shields use them) as 0-9.</summary>
        private static string ToWesternDigits(string text)
        {
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
                else if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            }
            return new string(chars);
        }

        /// <summary>
        /// The design of the other state: the face symbol swapped (…face-happy ↔ …face-sad), every LED line set to that
        /// state's message, the speed shields (network "SPD") showing the limit, and the marker's state flipped.
        /// <paramref name="design"/> itself is left unchanged. Designs without a marker are returned as copies.
        /// </summary>
        public static JObject Alternate(JObject design)
        {
            var copy = (JObject)design.DeepClone();
            if (!IsFeedback(copy)) return copy;
            var marker = (JObject)copy[Key];
            var target = Other(StateOf(copy));
            marker["state"] = target;
            var message = Message(marker, target);
            var limitText = LimitOf(copy).ToString("0", CultureInfo.InvariantCulture);
            foreach (var item in Items(copy))
            {
                switch ((string)item["type"])
                {
                    case "symbol":
                        var symbol = (string)item["symbol"];
                        if (symbol == null) break;
                        if (target == Slow && symbol.Contains(kHappy)) item["symbol"] = symbol.Replace(kHappy, kSad);
                        else if (target == Ok && symbol.Contains(kSad)) item["symbol"] = symbol.Replace(kSad, kHappy);
                        break;
                    case "led":
                        if (item["lines"] is JArray lines && lines.Count > 0)
                        {
                            item["lines"] = new JArray(message);
                        }
                        else if (item["text"] != null)
                        {
                            item["text"] = message;
                        }
                        break;
                    case "shield":
                        // The limit is read from this shield; only an empty one gets the marker's number.
                        if ((string)item["network"] == "SPD" && string.IsNullOrWhiteSpace((string)item["number"])) item["number"] = limitText;
                        break;
                }
            }
            // A message board (board: "led") keeps its lines at the top level.
            if ((string)copy["board"] == "led" && copy["lines"] is JArray boardLines && boardLines.Count > 0)
            {
                copy["lines"] = new JArray(message);
            }
            return copy;
        }

        /// <summary>The designs of a structure in the other state (signs without a marker unchanged).</summary>
        public static List<JObject> Alternate(IEnumerable<JObject> designs) => designs.Select(Alternate).ToList();

        private static string Message(JObject marker, string state)
        {
            var custom = (string)marker[state];
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            return state == Slow ? kSlowMessage : kOkMessage;
        }

        /// <summary>Every item object of a design, nested ones included.</summary>
        private static List<JObject> Items(JObject design) =>
            design.Descendants().OfType<JObject>().Where(o => o["type"] is JValue v && v.Type == JTokenType.String).ToList();
    }
}
