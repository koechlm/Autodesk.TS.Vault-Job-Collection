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

namespace adsk.ts.image.create.inventor
{

    [XmlRoot("settings")]
    public class Settings
    {
        [XmlElement("LogFileLocation")]
        public string LogFileLocation;

        [XmlElement("AcceptLocalIpj")]
        public string AcceptLocalIpj;

        [XmlElement("ExportFormats")]
        public string ExportFormats;

        [XmlElement("ImgFileType")]
        public string ImgFileType;

        [XmlElement("OutputPath")]
        public string OutPutPath;

        #region for future use
        //[XmlElement("OutputPath")]
        //public string mOutPutPath;
        #endregion for future use

        private Settings()
        {

        }

        public void Save()
        {
            try
            {
                string codeFolder = Util.GetAssemblyPath();
                string xmlPath = Path.Combine(codeFolder, "image.inventor.settings.xml");

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
            string xmlPath = Path.Combine(codeFolder, "image.inventor.settings.xml");

            using (System.IO.StreamReader reader = new System.IO.StreamReader(xmlPath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                retVal = (Settings)serializer.Deserialize(reader);
            }


            return retVal;
        }
    }

}
