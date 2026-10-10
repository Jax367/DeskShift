using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DeskShift {
    public sealed class Bridge : IDisposable {
        TcpListener listener;volatile bool stopped;readonly object gate=new object();
        string json;string image;
        public const int Port=47831;
        public int ListeningPort {get;private set;}
        public DateTime LastSeen {get;private set;}
        public Bridge(int port=Port) { listener=new TcpListener(IPAddress.Loopback,port);listener.Start();ListeningPort=((IPEndPoint)listener.LocalEndpoint).Port;new Thread(Run){IsBackground=true}.Start(); }
        public void Publish(Wallpaper w,Palette p) { lock(gate) { image=w.File;json=Library.Json.Serialize(new{revision=Guid.NewGuid().ToString("N"),name=w.Name,palette=p}); } }
        public void Clear() { lock(gate) { image=null;json=null; } }
        void Run() { while(!stopped) { try { var client=listener.AcceptTcpClient();ThreadPool.QueueUserWorkItem(_=>Serve(client)); } catch { if(stopped) return; } } }
        void Serve(TcpClient client) {
            using(client) try {
                client.ReceiveTimeout=2000;client.SendTimeout=3000;
                using(var stream=client.GetStream()) {
                    // Bounded request headers: this bridge is GET-only, never an execution endpoint.
                    var bytes=new MemoryStream();int previous=0,count=0;
                    while(bytes.Length<8192) { int b=stream.ReadByte();if(b<0) return;bytes.WriteByte((byte)b); if(b==10 && previous==13) count++; else if(b!=13) count=0;previous=b;if(count==2) break; }
                    string request=Encoding.ASCII.GetString(bytes.ToArray());string[] lines=request.Split(new[]{"\r\n"},StringSplitOptions.None);string[] first=lines[0].Split(' ');
                    string host="",origin="";foreach(string line in lines) { if(line.StartsWith("Host:",StringComparison.OrdinalIgnoreCase)) host=line.Substring(5).Trim();if(line.StartsWith("Origin:",StringComparison.OrdinalIgnoreCase)) origin=line.Substring(7).Trim(); }
                    if(first.Length!=3 || first[0]!="GET" || host!="127.0.0.1:"+ListeningPort || (origin!="" && !origin.StartsWith("chrome-extension://",StringComparison.Ordinal))) { Reply(stream,403,"text/plain",Encoding.UTF8.GetBytes("Forbidden"));return; }
                    string data,path;lock(gate) { data=json;path=image; }
                    string url=first[1].Split('?')[0];
                    if(url=="/theme" && data!=null) { LastSeen=DateTime.UtcNow;Reply(stream,200,"application/json; charset=utf-8",Encoding.UTF8.GetBytes(data)); }
                    else if(url=="/wallpaper" && path!=null && System.IO.File.Exists(path)) Reply(stream,200,Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)?"image/png":"image/jpeg",System.IO.File.ReadAllBytes(path));
                    else Reply(stream,404,"text/plain",Encoding.UTF8.GetBytes("Not found"));
                }
            } catch { /* A closed extension tab should never interrupt the desktop program. */ }
        }
        static void Reply(Stream stream,int status,string type,byte[] body) { byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+(status==200?" OK":status==403?" Forbidden":" Not Found")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");stream.Write(header,0,header.Length);stream.Write(body,0,body.Length); }
        public void Dispose() { stopped=true;listener.Stop(); }
    }
}
