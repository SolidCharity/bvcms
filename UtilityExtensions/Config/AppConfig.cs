﻿using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UtilityExtensions.Config
{
    public abstract class AppConfig : IDisposable
    {
        public static AppConfig Change(string path)
        {
            return new ChangeAppConfig(path);
        }

        public static string GetWebConfig(params string[] directoriesToSearch)
        {
            if (directoriesToSearch == null || directoriesToSearch.Length == 0)
            {
                var curDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                directoriesToSearch = new[] {
                    Path.GetFullPath(Path.Combine(curDir, @"..")),
                    Path.GetFullPath(Path.Combine(curDir, @"..\..\..\CmsWeb"))
                };
            }

            foreach(var dir in directoriesToSearch)
            { 
                var webconfig = Path.Combine(dir, "web.config");
                if (File.Exists(webconfig))
                {
                    return webconfig;
                }
            }

            return null;
        }

        public abstract void Dispose();

        private class ChangeAppConfig : AppConfig
        {
            private readonly string oldConfig =
                AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();

            private bool disposedValue;

            public ChangeAppConfig(string path)
            {
                AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", path);
                ResetConfigMechanism();
            }

            public override void Dispose()
            {
                if (!disposedValue)
                {
                    AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", oldConfig);
                    ResetConfigMechanism();


                    disposedValue = true;
                }
                GC.SuppressFinalize(this);
            }

            private static void ResetConfigMechanism()
            {
                typeof(ConfigurationManager)
                    .GetField("s_initState", BindingFlags.NonPublic |
                                             BindingFlags.Static)
                    .SetValue(null, 0);

                typeof(ConfigurationManager)
                    .GetField("s_configSystem", BindingFlags.NonPublic |
                                                BindingFlags.Static)
                    .SetValue(null, null);

                typeof(ConfigurationManager)
                    .Assembly.GetTypes()
                    .Where(x => x.FullName ==
                                "System.Configuration.ClientConfigPaths")
                    .First()
                    .GetField("s_current", BindingFlags.NonPublic |
                                           BindingFlags.Static)
                    .SetValue(null, null);
            }
        }
    }
}
