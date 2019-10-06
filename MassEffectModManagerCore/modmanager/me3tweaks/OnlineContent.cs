﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MassEffectModManager;
using MassEffectModManagerCore.modmanager.helpers;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    partial class OnlineContent
    {
        private static readonly string StartupManifestURL = "https://me3tweaks.com/modmanager/updatecheck?currentversion=" + App.BuildNumber;
        private const string ThirdPartyIdentificationServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyidentificationservice?highprioritysupport=true&allgames=true";
        private const string StaticFilesBaseURL = "https://raw.githubusercontent.com/ME3Tweaks/MassEffectModManager/master/MassEffectModManager/staticfiles/";
        private const string ThirdPartyImportingServiceURL = "https://me3tweaks.com/modmanager/services/thirdpartyimportingservice?allgames=true";
        private const string ThirdPartyModDescURL = "https://me3tweaks.com/mods/dlc_mods/importingmoddesc/";
        private const string ModInfoRelayEndpoint = "https://me3tweaks.com/modmanager/services/relayservice";

        private static readonly string TipsServiceURL = StaticFilesBaseURL + "tipsservice.json";
        public static Dictionary<string, string> FetchOnlineStartupManifest()
        {
            using (var wc = new System.Net.WebClient())
            {
                string json = wc.DownloadString(StartupManifestURL);
                App.ServerManifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return App.ServerManifest;
            }
        }
        public static Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>> FetchThirdPartyIdentificationManifest(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetThirdPartyIdentificationCachedFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {

                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyIdentificationServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyIdentificationCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(json);
                }
            }
            return JsonConvert.DeserializeObject<Dictionary<string, CaseInsensitiveDictionary<ThirdPartyServices.ThirdPartyModInfo>>>(File.ReadAllText(Utilities.GetThirdPartyIdentificationCachedFile()));
        }

        public static string FetchThirdPartyModdesc(string name)
        {
            using var wc = new System.Net.WebClient();
            string moddesc = wc.DownloadStringAwareOfEncoding(ThirdPartyModDescURL + name);
            return moddesc;
        }

        public static List<string> FetchTipsService(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetTipsServiceFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {
                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(TipsServiceURL);
                    File.WriteAllText(Utilities.GetTipsServiceFile(), json);
                    return JsonConvert.DeserializeObject<List<string>>(json);
                }
            }
            return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Utilities.GetTipsServiceFile()));
        }

        public static Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>> FetchThirdPartyImportingService(bool overrideThrottling = false)
        {
            if (!File.Exists(Utilities.GetThirdPartyImportingCachedFile()) || (!overrideThrottling && Utilities.CanFetchContentThrottleCheck()))
            {
                using (var wc = new System.Net.WebClient())
                {
                    string json = wc.DownloadStringAwareOfEncoding(ThirdPartyImportingServiceURL);
                    File.WriteAllText(Utilities.GetThirdPartyImportingCachedFile(), json);
                    return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>>(json);
                }
            }
            return JsonConvert.DeserializeObject<Dictionary<long, List<ThirdPartyServices.ThirdPartyImportingInfo>>>(File.ReadAllText(Utilities.GetThirdPartyImportingCachedFile()));
        }

        public static Dictionary<string, string> QueryModRelay(string md5, long size)
        {
            //Todo: Finish implementing relay service
            string finalRelayURL = $"{ModInfoRelayEndpoint}?modmanagerversion={App.BuildNumber}&md5={md5.ToLowerInvariant()}&size={size}";
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    Debug.WriteLine(finalRelayURL);
                    string json = wc.DownloadStringAwareOfEncoding(finalRelayURL);
                    //todo: Implement response format serverside
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
            }
            catch (Exception e)
            {
                Log.Error("Error querying relay service from ME3Tweaks: " + App.FlattenException(e));
            }
            return null;
        }

        public static bool EnsureCriticalFiles()
        {
            //7-zip
            try
            {
                string sevenZDLL = Utilities.Get7zDllPath();
                if (!File.Exists(sevenZDLL) || Utilities.CalculateMD5(sevenZDLL) != "72491c7b87a7c2dd350b727444f13bb4")
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        var fullURL = StaticFilesBaseURL + "7z.dll";
                        Log.Information("Downloading 7z.dll: " + fullURL);
                        wc.DownloadFile(fullURL, sevenZDLL);
                    }
                }

                if (File.Exists(sevenZDLL))
                {
                    Log.Information("Setting 7z dll path: " + sevenZDLL);
                    var p = Path.GetFullPath(sevenZDLL);
                    SevenZip.SevenZipBase.SetLibraryPath(sevenZDLL);
                }
                else
                {
                    Log.Fatal("Unable to load 7z dll! File doesn't exist: " + sevenZDLL);
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error("Exception ensuring critical files: " + App.FlattenException(e));
                return false;
            }

            return true;
        }
        public static bool EnsureStaticAssets()
        {
            string[] objectInfoFiles = { "ME1ObjectInfo.json", "ME2ObjectInfo.json", "ME3ObjectInfo.json" };
            string localBaseDir = Utilities.GetObjectInfoFolder();
            foreach (var info in objectInfoFiles)
            {
                var localPath = Path.Combine(localBaseDir, info);
                if (!File.Exists(localPath))
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        var fullURL = StaticFilesBaseURL + "objectinfos/" + info;
                        Log.Information("Downloading static asset: " + fullURL);
                        wc.DownloadFile(fullURL, localPath);
                    }
                }
            }

            return true;
        }
    }
}
