using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace DeskShift {
    internal static class BridgeConnection {
        internal static string LoadOrCreate(string root) {
            string path=Path.Combine(root,"bridge-key.dat");byte[] key;
            if(File.Exists(path))key=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
            else {key=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(key);Directory.CreateDirectory(root);string temp=path+"."+Guid.NewGuid().ToString("N");File.WriteAllBytes(temp,ProtectedData.Protect(key,null,DataProtectionScope.CurrentUser));try{File.Move(temp,path);}catch{File.Delete(temp);throw;}}
            if(key.Length!=32)throw new InvalidOperationException("浏览器连接密钥无效。");
            return Convert.ToBase64String(key).TrimEnd('=').Replace('+','-').Replace('/','_');
        }
        internal static bool IsDeskShiftExtension(string path,string browser) {
            try {
                path=Path.GetFullPath(path);for(var parent=new DirectoryInfo(path);parent!=null;parent=parent.Parent)if((parent.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var data=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(path,"manifest.json")));
                var overrides=data.ContainsKey("chrome_url_overrides")?data["chrome_url_overrides"] as Dictionary<string,object>:null;
                return data.ContainsKey("name")&&(string)data["name"]=="DeskShift · "+(browser=="edge"?"Edge":"Chrome")+" 简洁新标签页"&&overrides!=null&&overrides.ContainsKey("newtab")&&(string)overrides["newtab"]=="newtab.html";
            }catch{return false;}
        }
        internal static string[] InstalledFolders(string browser) {
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string userData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),browser=="edge"?"Microsoft\\Edge\\User Data":"Google\\Chrome\\User Data");
            if(!Directory.Exists(userData))return new string[0];
            foreach(string profile in Directory.GetDirectories(userData)){string name=Path.GetFileName(profile);if(name!="Default"&&!name.StartsWith("Profile ",StringComparison.Ordinal))continue;foreach(string file in new[]{"Preferences","Secure Preferences"})try {
                string preference=Path.Combine(profile,file);if(!File.Exists(preference)||new FileInfo(preference).Length>32*1024*1024)continue;
                var data=new JavaScriptSerializer{MaxJsonLength=32*1024*1024}.Deserialize<Dictionary<string,object>>(File.ReadAllText(preference));var extensions=data["extensions"] as Dictionary<string,object>;var settings=extensions["settings"] as Dictionary<string,object>;
                foreach(var entry in settings.Values){var value=entry as Dictionary<string,object>;object raw;if(value!=null&&value.TryGetValue("path",out raw)&&raw is string&&Path.IsPathRooted((string)raw)&&IsDeskShiftExtension((string)raw,browser))paths.Add(Path.GetFullPath((string)raw));}
            }catch{}}
            return new List<string>(paths).ToArray();
        }
        internal static void UpdateFolder(string source,string target,string key) {
            Directory.CreateDirectory(target);
            foreach(string name in new[]{"manifest.json","INSTALL.html","newtab.html","newtab.css","newtab.js"})if(!String.Equals(Path.GetFullPath(source),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))File.Copy(Path.Combine(source,name),Path.Combine(target,name),true);
            var manifest=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(source,"manifest.json")));
            Library.AtomicWrite(Path.Combine(target,"connection.json"),Library.Json.Serialize(new{key=key,extensionVersion=(string)manifest["version"]}));
        }
        internal static string Configure(string root,string applicationFolder,string key,bool repairInstalled=false) {
            var report=new List<string>();
            foreach(string browser in new[]{"chrome","edge"}) {
                string source=Path.Combine(applicationFolder,"browser",browser),target=Path.Combine(root,"browser-extensions",browser);UpdateFolder(source,target,key);
                if(repairInstalled)foreach(string installed in InstalledFolders(browser))try{UpdateFolder(source,installed,key);report.Add((browser=="edge"?"Edge":"Chrome")+" 原安装目录已修复");}catch{report.Add((browser=="edge"?"Edge":"Chrome")+" 原目录不可写，请按指南重新加载");}
            }
            return String.Join("；",report);
        }
    }
}
