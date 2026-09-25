using System;
using System.IO;

namespace NovaLauncher.Core
{
    public static class AppPaths
    {
        public static string RootFolder
        {
            get
            {
                string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string root = Path.Combine(baseFolder, "NovaLauncher");
                Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string IconsFolder
        {
            get
            {
                string folder = Path.Combine(RootFolder, "Icons");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string LibraryFile => Path.Combine(RootFolder, "library.json");

        public static string LogFile => Path.Combine(RootFolder, "launcher.log");
    }
}
