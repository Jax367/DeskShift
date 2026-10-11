using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace DeskShift {
    public sealed class Bridge : IDisposable {
        TcpListener listener;volatile bool stopped;readonly object gate=new object();
        string json;string image;string revision;int publication,activeClients;readonly string connectionKey;
        public const int Port=47831;
        public int ListeningPort {get;private set;}
        DateTime seenChrome,seenEdge;
        public DateTime LastSeenChrome {get{lock(gate)return seenChrome;}}
        public DateTime LastSeenEdge {get{lock(gate)return seenEdge;}}
        internal string ConnectionKey {get{return connectionKey;}}
        public Bridge(int port=Port,string key=null) {if(key==null){var bytes=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);key=Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}if(!Regex.IsMatch(key,"^[A-Za-z0-9_-]{43}$"))throw new ArgumentException("Invalid connection key");connectionKey=key;listener=new TcpListener(IPAddress.Loopback,port);listener.Start();ListeningPort=((IPEndPoint)listener.LocalEndpoint).Port;new Thread(Run){IsBackground=true}.Start(); }
        public void Publish(Wallpaper w,Palette p,string style="acrylic") {
            string backgroundStyle=BrowserWallpaper.Normalize(style);
            int ticket;string next=Guid.NewGuid().ToString("N"),source=w.File,data=Library.Json.Serialize(new{revision=next,name=w.Name,palette=p,backgroundStyle=backgroundStyle});lock(gate)ticket=++publication;
            ThreadPool.QueueUserWorkItem(_=>{string prepared;try{prepared=BrowserWallpaper.Prepare(source,backgroundStyle,p);}catch{return;}lock(gate){if(stopped||ticket!=publication)return;image=prepared;revision=next;json=data;}});
        }
        public void Clear() { lock(gate) { ++publication;image=null;json=null; } }
        void Run() { while(!stopped) { try { var client=listener.AcceptTcpClient();if(Interlocked.Increment(ref activeClients)>16){Interlocked.Decrement(ref activeClients);client.Close();continue;}ThreadPool.QueueUserWorkItem(_=>{try{Serve(client);}finally{Interlocked.Decrement(ref activeClients);}}); } catch { if(stopped) return; } } }
        internal static bool KeysEqual(string expected,string supplied){if(supplied==null||expected.Length!=supplied.Length)return false;int difference=0;for(int i=0;i<expected.Length;i++)difference|=expected[i]^supplied[i];return difference==0;}
        void Serve(TcpClient client) {
            using(client) try {
                client.ReceiveTimeout=2000;client.SendTimeout=3000;
                using(var stream=client.GetStream()) {
                    // Bounded request headers: this bridge is GET-only, never an execution endpoint.
                    var bytes=new MemoryStream();int previous=0,count=0;bool complete=false;var headerTime=Stopwatch.StartNew();
                    while(bytes.Length<8192&&headerTime.ElapsedMilliseconds<2000) { int b=stream.ReadByte();if(b<0) return;bytes.WriteByte((byte)b); if(b==10 && previous==13) count++; else if(b!=13) count=0;previous=b;if(count==2){complete=true;break;} }
                    string request=Encoding.ASCII.GetString(bytes.ToArray());string[] lines=request.Split(new[]{"\r\n"},StringSplitOptions.None);string[] first=lines[0].Split(' ');
                    string host="",origin="",key="";int hostCount=0,originCount=0,keyCount=0;foreach(string line in lines) { if(line.StartsWith("Host:",StringComparison.OrdinalIgnoreCase)){host=line.Substring(5).Trim();hostCount++;}if(line.StartsWith("Origin:",StringComparison.OrdinalIgnoreCase)){origin=line.Substring(7).Trim();originCount++;}if(line.StartsWith("X-DeskShift-Key:",StringComparison.OrdinalIgnoreCase)){key=line.Substring(16).Trim();keyCount++;} }
                    if(!complete||first.Length!=3 || first[0]!="GET" || first[2]!="HTTP/1.1" || hostCount!=1 || keyCount!=1 || originCount>1 ||host!="127.0.0.1:"+ListeningPort || !KeysEqual(connectionKey,key) || (originCount==1 && !Regex.IsMatch(origin,"^chrome-extension://[a-p]{32}$"))) { Reply(stream,403,"text/plain",Encoding.UTF8.GetBytes("Forbidden"));return; }
                    string data,path,currentRevision;lock(gate) { data=json;path=image;currentRevision=revision; }
                    string[] route=first[1].Split(new[]{'?'},2);string url=route[0];
                    if(url=="/theme" && data!=null) { lock(gate){if(route.Length==2 && Array.IndexOf(route[1].Split('&'),"browser=edge")>=0)seenEdge=DateTime.UtcNow;else seenChrome=DateTime.UtcNow;}Reply(stream,200,"application/json; charset=utf-8",Encoding.UTF8.GetBytes(data)); }
                    else if(url=="/wallpaper" && path!=null && System.IO.File.Exists(path) && (route.Length==1 || Array.IndexOf(route[1].Split('&'),"revision="+currentRevision)>=0)) Reply(stream,200,Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)?"image/png":Path.GetExtension(path).Equals(".bmp",StringComparison.OrdinalIgnoreCase)?"image/bmp":"image/jpeg",System.IO.File.ReadAllBytes(path));
                    else Reply(stream,404,"text/plain",Encoding.UTF8.GetBytes("Not found"));
                }
            } catch { /* A closed extension tab should never interrupt the desktop program. */ }
        }
        static void Reply(Stream stream,int status,string type,byte[] body) { byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+(status==200?" OK":status==403?" Forbidden":" Not Found")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");stream.Write(header,0,header.Length);stream.Write(body,0,body.Length); }
        public void Dispose() { stopped=true;listener.Stop(); }
    }
}
