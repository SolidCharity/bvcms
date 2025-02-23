﻿using IntegrationTests.Support;
using SharedTestFixtures;
using System;
using System.Net;
using System.Xml;

namespace IntegrationTests
{
    public class WebAppFixture : IDisposable
    {
        private IISExpress cmswebInstance;

        public WebAppFixture()
        {
            Settings.EnsureApplicationHostConfig();
            SetupWebConfig();
            cmswebInstance = IISExpress.Start(Settings.ApplicationHostConfig, "CMSWeb");
            Warmup();
        }

        private void SetupWebConfig()
        {
            var xml = DatabaseFixture.LoadWebConfig();
            var appSettings = xml.GetElementsByTagName("appSettings")[0];
            var list = appSettings.SelectNodes("add[@key='UseRuntimeSettingsCache']");
            if (list.Count == 0)
            {
                var setting = xml.CreateElement("add");
                var attr = xml.CreateAttribute("key");
                attr.Value = "UseRuntimeSettingsCache";
                setting.Attributes.Append(attr);
                attr = xml.CreateAttribute("value");
                attr.Value = "false";
                setting.Attributes.Append(attr);
                appSettings.AppendChild(setting);
                xml.Save(DatabaseFixture.FindWebConfigPath());
            }
        }

        private void CleanupWebConfig()
        {
            var xml = DatabaseFixture.LoadWebConfig();
            var appSettings = xml.GetElementsByTagName("appSettings")[0];
            var list = appSettings.SelectNodes("add[@key='UseRuntimeSettingsCache']");
            if (list.Count > 0)
            {
                appSettings.RemoveChild(list[0]);
                xml.Save(DatabaseFixture.FindWebConfigPath());
            }
        }

        private void Warmup()
        {
            var attempts = 0;
            while (attempts++ < 100)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.DownloadString(Settings.RootUrl);
                        break;
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            cmswebInstance.Stop();
            cmswebInstance = null;
            CleanupWebConfig();
        }
    }
}
