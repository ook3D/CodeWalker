using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CodeWalker.World
{
    public class Timecycle
    {
        public volatile bool Inited = false;
        public bool UseModdedData { get; set; } = true;

        public float sun_roll { get; set; }
        public float sun_yaw { get; set; }
        public float moon_roll { get; set; }
        public float moon_wobble_freq { get; set; }
        public float moon_wobble_amp { get; set; }
        public float moon_wobble_offset { get; set; }
        public List<TimecycleSample> Samples { get; set; } = new List<TimecycleSample>();
        public List<string> Regions { get; set; } = new List<string>();

        public float CurrentHour { get; set; } = 0;
        public int CurrentSampleIndex { get; set; } = 0;
        public float CurrentSampleBlend { get; set; } = 1.0f;
        public float NextSampleBlend { get; set; } = 0.0f;
        public Vector3 CurrentSunDirection { get; set; } = new Vector3(0, 0, 1);
        public Vector3 CurrentMoonDirection { get; set; } = new Vector3(0, 0, -1);

        public void Init(GameFileCache gameFileCache, Action<string> updateStatus)
        {
            var rpfman = gameFileCache.RpfMan
                ?? throw new InvalidOperationException("The game file cache must have an RPF manager before loading timecycle data.");

            string filename = "common.rpf\\data\\levels\\gta5\\time.xml";

            XmlDocument timexml = rpfman.GetFileXml(filename, UseModdedData);

            var time = timexml?.DocumentElement
                ?? throw new XmlException("The timecycle document must have a root element.");
            XmlNode? suninfo = time.SelectSingleNode("suninfo");
            XmlNode? mooninfo = time.SelectSingleNode("mooninfo");
            XmlNodeList? samples = time.SelectNodes("sample");
            XmlNodeList? regions = time.SelectNodes("region");

            sun_roll = Xml.GetFloatAttribute(suninfo, "sun_roll");
            sun_yaw = Xml.GetFloatAttribute(suninfo, "sun_yaw");
            moon_roll = Xml.GetFloatAttribute(mooninfo, "moon_roll");
            moon_wobble_freq = Xml.GetFloatAttribute(mooninfo, "moon_wobble_freq");
            moon_wobble_amp = Xml.GetFloatAttribute(mooninfo, "moon_wobble_amp");
            moon_wobble_offset = Xml.GetFloatAttribute(mooninfo, "moon_wobble_offset");

            Samples.Clear();
            if (samples != null)
            {
                foreach (XmlNode sample in samples)
                {
                    TimecycleSample tcs = new();
                    tcs.Init(sample);
                    Samples.Add(tcs);
                }
            }

            Regions.Clear();
            if (regions != null)
            {
                foreach (XmlNode region in regions)
                {
                    Regions.Add(Xml.GetStringAttribute(region, "name") ?? string.Empty);
                }
            }

            Inited = true;
        }


        public void SetTime(float hour)
        {
            float day = Math.Max(hour / 24.0f, 0.0f);
            float h = hour - ((float)Math.Floor(day) * 24.0f);
            CurrentHour = h;

            for (int i = 0; i < Samples.Count; i++)
            {
                bool lasti = (i >= Samples.Count - 1);
                var cur = Samples[i];
                var nxt = Samples[lasti ? 0 : i+1];
                var nxth = lasti ? nxt.hour + 24.0f : nxt.hour;
                if (((h >= cur.hour) && (h < nxth)) || lasti)
                {
                    float blendrange = (nxth - cur.hour) - cur.duration;
                    float blendstart = cur.hour + cur.duration;
                    float blendrel = h - blendstart;
                    float blendval = blendrel / blendrange;
                    float blend = Math.Min(Math.Max(blendval, 0.0f), 1.0f);
                    NextSampleBlend = blend;
                    CurrentSampleBlend = 1.0f - blend;
                    CurrentSampleIndex = i;
                    break;
                }
            }
        }

        public bool IsNightTime
        {
            get
            {
                return (CurrentHour < 6.0f) || (CurrentHour > 20.0f);
            }
        }
    }

    public class TimecycleSample
    {
        public string name { get; set; } = string.Empty;
        public float hour { get; set; }
        public float duration { get; set; }
        public string uw_tc_mod { get; set; } = string.Empty;

        public void Init(XmlNode node)
        {
            name = Xml.GetStringAttribute(node, "name") ?? string.Empty;
            hour = Xml.GetFloatAttribute(node, "hour");
            duration = Xml.GetFloatAttribute(node, "duration");
            uw_tc_mod = Xml.GetStringAttribute(node, "uw_tc_mod") ?? string.Empty;
        }
    }


}
