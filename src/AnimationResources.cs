using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace DeskShift {
    internal sealed class ResourceLease<T> : IDisposable where T:class,IDisposable {
        public readonly T Value;Action release;
        public ResourceLease(T value,Action cleanup){Value=value;release=cleanup;}
        public void Dispose(){var action=Interlocked.Exchange(ref release,null);if(action!=null)action();}
    }
    // Leases keep active artwork alive; unused entries are evicted by a bounded LRU.
    internal static class AnimationResources {
        sealed class Entry {public string Key;public Lazy<IDisposable> Value;public long Bytes,Stamp;public int Users;}
        static readonly object gate=new object();static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();static long sequence;
        internal const long Budget=160L*1024*1024;
        internal static int Builds {get;private set;}
        internal static int Hits {get;private set;}
        internal static int Count {get{lock(gate)return entries.Count;}}
        public static ResourceLease<T> Acquire<T>(string key,long bytes,Func<T> create) where T:class,IDisposable {
            Entry entry;
            lock(gate){if(entries.TryGetValue(key,out entry))Hits++;else{entry=new Entry{Key=key,Bytes=bytes,Value=new Lazy<IDisposable>(()=>{var value=create();lock(gate)Builds++;return value;},LazyThreadSafetyMode.ExecutionAndPublication)};entries.Add(key,entry);}entry.Users++;entry.Stamp=++sequence;}
            T result;
            try{result=(T)entry.Value.Value;}catch{lock(gate){entry.Users--;if(entry.Users==0)entries.Remove(key);}throw;}
            Trim();return new ResourceLease<T>(result,()=>{lock(gate){entry.Users--;entry.Stamp=++sequence;}Trim();});
        }
        static void Trim() {
            var remove=new List<Entry>();
            lock(gate){long bytes=entries.Values.Sum(e=>e.Bytes);foreach(var entry in entries.Values.Where(e=>e.Users==0 && e.Value.IsValueCreated).OrderBy(e=>e.Stamp).ToArray()){if(bytes<=Budget)break;entries.Remove(entry.Key);bytes-=entry.Bytes;remove.Add(entry);}}
            foreach(var entry in remove)entry.Value.Value.Dispose();
        }
        internal static void ClearUnused(){Entry[] remove;lock(gate){remove=entries.Values.Where(e=>e.Users==0).ToArray();foreach(var entry in remove)entries.Remove(entry.Key);}foreach(var entry in remove)if(entry.Value.IsValueCreated)entry.Value.Value.Dispose();}
        internal static string PaletteKey(Palette p){return p.Accent+"/"+p.Background+"/"+p.Text+"/"+p.Dark;}
    }
    internal sealed class GeometryFrames : IDisposable {
        public readonly Region[] Cover=new Region[49],Reveal=new Region[49];
        public GeometryFrames(Size size,int style){try{for(int i=0;i<49;i++){Cover[i]=TransitionWindow.Shape(size,0,i/48.0,style);Reveal[i]=TransitionWindow.Shape(size,2,i/48.0,style);}}catch{Dispose();throw;}}
        public void Dispose(){foreach(var r in Cover)if(r!=null)r.Dispose();foreach(var r in Reveal)if(r!=null)r.Dispose();}
    }
    internal sealed class TransitionArtwork : IDisposable {
        public ResourceLease<Bitmap> Background;public ResourceLease<GeometryFrames> Geometry;public ResourceLease<WaitingMotion> Motion;
        public TransitionArtwork(Size size,Palette palette,int style,int waitingStyle) {
            string dimensions=size.Width+"x"+size.Height,color=AnimationResources.PaletteKey(palette);
            try{
                Background=AnimationResources.Acquire("background/"+dimensions+"/"+color+"/"+(style==2?2:0),(long)size.Width*size.Height*4,()=>{var bmp=new Bitmap(size.Width,size.Height);try{using(var g=Graphics.FromImage(bmp))TransitionWindow.Render(g,new Rectangle(Point.Empty,size),palette,style,.2);return bmp;}catch{bmp.Dispose();throw;}});
                Geometry=AnimationResources.Acquire("geometry/"+dimensions+"/"+style,98*8192L,()=>new GeometryFrames(size,style));
                Motion=AnimationResources.Acquire("motion/"+color+"/"+waitingStyle,WaitingMotion.Width*(long)WaitingMotion.Height*4*WaitingMotion.Count,()=>new WaitingMotion(palette,waitingStyle));
            }catch{Dispose();throw;}
        }
        public void Dispose(){if(Background!=null)Background.Dispose();if(Geometry!=null)Geometry.Dispose();if(Motion!=null)Motion.Dispose();}
    }
}
