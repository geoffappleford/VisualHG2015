﻿using System;
using System.IO;

namespace VisualHg
{
    public class VisualHgOptions
    {
        public bool AutoActivatePlugin { get; set; }

        public bool AddFilesOnLoad { get; set; }

        public bool AutoAddNewFiles { get; set; }

        public bool AutoSaveProjectFiles { get; set; }

        public bool ProjectStatusIncludesChildren { get; set; }

        public string DiffToolPath { get; set; }

        public string DiffToolArguments { get; set; }

        public string StatusImageFileName { get; set; }

        public bool TrackChangesNotInSolution { get; set; }

        public VisualHgOptions()
        {
            AutoActivatePlugin = true;
            AutoAddNewFiles = true;
            AutoSaveProjectFiles = true;
            ProjectStatusIncludesChildren = true;
            TrackChangesNotInSolution = false;
        }

        
        private static VisualHgOptions _global;

        public static VisualHgOptions Global
        {
            get { return _global ?? (_global = Load()); }
            set
            {
                _global = value;
                Save(_global);
            }
        }


        private static string optionsPath = Path.Combine
               (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"VisualHg\Options.xml");

        private static VisualHgOptions Load()
        {
            return Serializer.Deserialize<VisualHgOptions>(optionsPath) ?? new VisualHgOptions();
        }

        private static void Save(VisualHgOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(optionsPath));

            Serializer.Serialize(optionsPath, options);
        }
    }
}