/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
=====================================================================*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace adsk.ts.nwd.create.navisworks
{

    [XmlRoot("settings")]
    public class Settings
    {
        [XmlElement("LogFileLocation")]
        public string LogFileLocation;

        [XmlElement("ExportFormats")]
        public string ExportFormats;

        [XmlElement("NwdTemplate")]
        public string NwdTemplate;

        [XmlElement("OutputPath")]
        public string OutPutPath;

        // added for future enhancement using Navisworks Plugins to export, instead of Automation
        //[XmlElement("NWDExcludeHiddenItems")]
        //public string NWDExcludeHiddenItems;

        //[XmlElement("NWDEmbedXrefs")]
        //public string NWDEmbedXrefs;

        //[XmlElement("NWDPreventObjectPropertyExport")]
        //public string NWDPreventObjectPropertyExport;

        //[XmlElement("NWDFileVersion")]
        //public string NWDFileVersion;

        private Settings()
        {

        }

        public void Save()
        {
            try
            {
                string codeFolder = Util.GetAssemblyPath();
                string xmlPath = Path.Combine(codeFolder, "adsk.ts.nwd.create.navisworks.settings.xml");

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(xmlPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(writer, this);
                }
            }
            catch
            { }
        }

        public static Settings Load()
        {
            Settings retVal = new Settings();


            string codeFolder = Util.GetAssemblyPath();
            string xmlPath = Path.Combine(codeFolder, "adsk.ts.nwd.create.navisworks.settings.xml");

            using (System.IO.StreamReader reader = new System.IO.StreamReader(xmlPath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                retVal = (Settings)serializer.Deserialize(reader);
            }


            return retVal;
        }
    }

}
