using System.Xml;
using System.Collections.Generic;

namespace Utilities.XmlUtilities
{
    public static class StatsXMLreader
    {
        public static XmlDocument doc;

        public static string stats_fileName = System.IO.Directory.GetCurrentDirectory() + "/assets/xml_defs/stats.xml";

        public static string getParameterFromXML(string caller, string field = null)
        {
            if (doc == null)
            { // load the doc if its null
                doc = new XmlDocument();
                doc.Load(stats_fileName);
            }
            XmlNode node;
            if (field == null)
            {
                node = doc.DocumentElement.SelectSingleNode("/stats/" + caller + "[1]");
            }
            else
            {
                node = doc.DocumentElement.SelectSingleNode("/stats/" + caller + "/" + field + "[1]");
            }
            if (node != null)
            {
                return node.InnerText;
            }
            else
                return null;
        }

        public static string[] getParametersFromXML(string caller, string field = null)
        {
            List<string> strings;
            if (doc == null)
            { // load the doc if its null
                doc = new XmlDocument();
                doc.Load(stats_fileName);
            }
            XmlNodeList nodes;
            if (field == null)
            {
                nodes = doc.DocumentElement.SelectNodes("/stats/" + caller);
            }
            else
            {
                nodes = doc.DocumentElement.SelectNodes("/stats/" + caller + "/" + field);
            }
            if (nodes != null)
            {
                strings = new List<string>();
                foreach (XmlNode node in nodes)
                    strings.Add(node.InnerText);
            }
            else
                return null;
            return strings.ToArray();
        }
    }
}

