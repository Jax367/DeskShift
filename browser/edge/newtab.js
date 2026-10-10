const base = 'http://127.0.0.1:47831';
const browserName = chrome.runtime.getManifest().name.includes('Edge') ? 'edge' : 'chrome';
let revision = '', loading = false, wallpaperURL = '';
const status = document.getElementById('status');

function applyPalette(data) {
  const p = data && data.palette;
  if (!p || ![p.Accent,p.Background,p.Text].every(value => /^#[0-9a-f]{6}$/i.test(value))) throw Error('Invalid palette');
  for (const key of ['Accent','Background','Text']) document.documentElement.style.setProperty('--'+key.toLowerCase(),p[key]);
  document.documentElement.dataset.mode=p.Dark===true?'dark':'light';
}
async function displayWallpaper(blob) {
  if (!blob || !blob.size || !blob.type.startsWith('image/')) return;
  const next = URL.createObjectURL(blob);
  try {
    const image = new Image(); image.src = next; await image.decode();
    document.getElementById('wallpaper').style.backgroundImage = 'url("'+next+'")';
    if (wallpaperURL) URL.revokeObjectURL(wallpaperURL);
    wallpaperURL = next;
  } catch (error) { URL.revokeObjectURL(next); throw error; }
}
async function restore() {
  try {
    const saved = await chrome.storage.local.get('theme');
    if (saved.theme) applyPalette(saved.theme);
    const cache = await caches.open('deskshift-wallpaper');
    const response = await cache.match(base+'/wallpaper');
    if (response) await displayWallpaper(await response.blob());
  } catch { /* A missing cache should not delay a new tab. */ }
}
async function sync() {
  if (loading) return; loading = true;
  try {
    const response = await fetch(base+'/theme?browser='+browserName,{signal:AbortSignal.timeout(3000)});
    if (!response.ok) throw Error('No active wallpaper');
    const data = await response.json();
    if (data.revision !== revision) {
      applyPalette(data);
      const wallpaper = await fetch(base+'/wallpaper?revision='+encodeURIComponent(data.revision),{signal:AbortSignal.timeout(15000)});
      if (!wallpaper.ok) throw Error('Wallpaper unavailable');
      const blob = await wallpaper.blob(); await displayWallpaper(blob);
      await chrome.storage.local.set({theme:data});
      try {const cache=await caches.open('deskshift-wallpaper');await cache.put(base+'/wallpaper',new Response(blob,{headers:{'Content-Type':blob.type}}));} catch { /* Storage quota does not stop live sync. */ }
      revision=data.revision;
    }
    status.textContent=''; status.classList.remove('warning');
  } catch {
    status.textContent=wallpaperURL?'DeskShift 离线 · 保留上次壁纸':'启动 DeskShift 并应用壁纸';
    status.classList.add('warning');
  } finally {loading=false;}
}
let sitePrefs={manual:[],hiddenHosts:[],auto:true},automaticSites=[],editing=null,saving=false;
const editor=document.getElementById('site-editor');
function validURL(value) {
  const raw=String(value || '').trim();if(!raw || raw.length>2048)throw Error('请输入有效的网址');
  let url;try{url=new URL(/^[a-z][\w+.-]*:/i.test(raw)?raw:'https://'+raw);}catch{throw Error('网址格式不正确，例如 github.com');}
  if(!['http:','https:'].includes(url.protocol) || url.username || url.password)throw Error('只支持不含账号密码的 HTTP / HTTPS 网址');
  return url;
}
function readPrefs(value) {
  const result={manual:[],hiddenHosts:[],auto:value?.auto!==false},used=new Set();
  for(const site of Array.isArray(value?.manual)?value.manual:[]) {
    try{const url=validURL(site.url);if(used.has(url.href))continue;used.add(url.href);result.manual.push({url:url.href,title:String(site.title || url.hostname).trim().slice(0,80)});if(result.manual.length===8)break;}catch{}
  }
  result.hiddenHosts=(Array.isArray(value?.hiddenHosts)?value.hiddenHosts:[]).filter(host=>typeof host==='string').slice(-128);
  return result;
}
function visibleSites() {
  const output=sitePrefs.manual.map(site=>({...site,manual:true})),hosts=new Set(output.map(site=>new URL(site.url).host));
  if(sitePrefs.auto)for(const site of automaticSites) {
    try{const url=validURL(site.url);if(hosts.has(url.host) || sitePrefs.hiddenHosts.includes(url.host))continue;hosts.add(url.host);output.push({url:url.href,title:String(site.title || url.hostname.replace(/^www\./,'')).trim(),manual:false});if(output.length>=8)break;}catch{}
  }
  return output.slice(0,8);
}
function renderSites() {
  const list=document.getElementById('sites'),sites=visibleSites();list.replaceChildren();
  list.style.setProperty('--columns',String(sites.length+1));
  for(const site of sites) {
    const url=new URL(site.url),name=site.title || url.hostname;
    const tile=document.createElement('div');tile.className='site-tile';
    const link=document.createElement('a');link.className='site';link.href=url.href;link.title=name+' · '+url.hostname;
    const icon=document.createElement('span');icon.className='site-icon';
    const image=document.createElement('img');image.alt='';image.width=28;image.height=28;
    // Read the browser's original site icon, without sending URLs to an icon service.
    const favicon=new URL(chrome.runtime.getURL('/_favicon/'));
    favicon.searchParams.set('pageUrl',url.href);favicon.searchParams.set('size','32');
    image.src=favicon.href;
    image.addEventListener('error',()=>{image.hidden=true;icon.classList.add('unavailable');});
    icon.append(image);
    const label=document.createElement('span');label.className='site-label';label.textContent=name;
    link.append(icon,label);
    const edit=document.createElement('button');edit.className='site-edit';edit.type='button';edit.textContent='⋯';edit.title='编辑 '+name;edit.setAttribute('aria-label','编辑 '+name);edit.addEventListener('click',()=>openEditor(site));
    tile.append(link,edit);list.append(tile);
  }
  const add=document.createElement('button');add.type='button';add.className='site add-site';add.title='添加网址或管理自动推荐';add.setAttribute('aria-label','添加常用网址');
  const icon=document.createElement('span');icon.className='site-icon';icon.textContent='+';
  const label=document.createElement('span');label.className='site-label';label.textContent='添加网址';add.append(icon,label);add.addEventListener('click',()=>openEditor(null));list.append(add);
  document.getElementById('empty').hidden=visibleSites().length!==0;
}
function openEditor(site) {
  editing=site;document.getElementById('editor-title').textContent=site?'编辑常用网址':'添加常用网址';
  document.getElementById('site-title').value=site?.title || '';document.getElementById('site-url').value=site?.url || '';
  document.getElementById('auto-sites').checked=sitePrefs.auto;document.getElementById('site-error').textContent='';
  const remove=document.getElementById('remove-site');remove.hidden=!site;remove.textContent=site?.manual?'删除网址':'隐藏此推荐';
  document.getElementById('restore-sites').hidden=sitePrefs.hiddenHosts.length===0;
  editor.showModal();
}
async function persistSites(next) {
  await chrome.storage.local.set({shortcutPrefs:next});sitePrefs=next;renderSites();
}
document.getElementById('site-form').addEventListener('submit',async event=>{
  event.preventDefault();if(saving)return;saving=true;document.getElementById('save-site').disabled=true;
  try {
    const raw=document.getElementById('site-url').value.trim(),auto=document.getElementById('auto-sites').checked;
    const next={manual:sitePrefs.manual.slice(),hiddenHosts:sitePrefs.hiddenHosts.slice(),auto};
    // An empty add dialog can change recommendation preferences without adding a link.
    if(raw || editing) {
      const url=validURL(raw),title=document.getElementById('site-title').value.trim().slice(0,80) || url.hostname.replace(/^www\./,'');
      const old=editing?.manual?next.manual.findIndex(site=>site.url===editing.url):-1;
      if(next.manual.some((site,index)=>index!==old && site.url===url.href))throw Error('这个网址已经添加过了');
      if(old>=0)next.manual[old]={url:url.href,title};else{if(next.manual.length>=8)throw Error('最多固定 8 个网址，请先删除一个');next.manual.push({url:url.href,title});}
      next.hiddenHosts=next.hiddenHosts.filter(host=>host!==url.host);
    }
    await persistSites(next);editor.close();
  } catch(error){document.getElementById('site-error').textContent=error.message || '保存失败，请重试';}
  finally{saving=false;document.getElementById('save-site').disabled=false;}
});
document.getElementById('remove-site').addEventListener('click',async()=>{
  if(!editing || saving)return;saving=true;
  try{const host=new URL(editing.url).host;await persistSites({manual:sitePrefs.manual.filter(site=>site.url!==editing.url),hiddenHosts:[...new Set([...sitePrefs.hiddenHosts,host])].slice(-128),auto:sitePrefs.auto});editor.close();}
  catch(error){document.getElementById('site-error').textContent=error.message || '删除失败';}finally{saving=false;}
});
document.getElementById('restore-sites').addEventListener('click',async()=>{
  if(saving)return;saving=true;
  try{await persistSites({...sitePrefs,hiddenHosts:[]});document.getElementById('restore-sites').hidden=true;}
  catch(error){document.getElementById('site-error').textContent=error.message || '恢复失败';}finally{saving=false;}
});
document.getElementById('cancel-site').addEventListener('click',()=>{if(!saving)editor.close();});
editor.addEventListener('cancel',event=>{if(saving)event.preventDefault();});
async function loadSites() {
  try{const saved=await chrome.storage.local.get('shortcutPrefs');sitePrefs=readPrefs(saved.shortcutPrefs);}catch{}
  if(!sitePrefs.auto){renderSites();return;}
  try{chrome.topSites.get(sites=>{automaticSites=chrome.runtime.lastError?[]:(sites || []);renderSites();});}catch{automaticSites=[];renderSites();}
}
if(chrome.storage.onChanged)chrome.storage.onChanged.addListener((changes,area)=>{if(area==='local' && changes.shortcutPrefs){sitePrefs=readPrefs(changes.shortcutPrefs.newValue);if(sitePrefs.auto)loadSites();else renderSites();}});

document.getElementById('search').addEventListener('submit',event=>{
  event.preventDefault();const text=document.getElementById('query').value.trim();if(!text)return;
  let address='';try{const candidate=/^https?:\/\//i.test(text)?text:/^[\w.-]+\.[a-z]{2,}(?::\d+)?(\/[^\s]*)?$/i.test(text)?'https://'+text:'';if(candidate){const url=new URL(candidate);if(['http:','https:'].includes(url.protocol))address=url.href;}}catch{}
  location.href=address || (browserName==='edge'?'https://www.bing.com/search?q=':'https://www.google.com/search?q=')+encodeURIComponent(text);
});
const ready=(async()=>{await loadSites();await restore();await sync();})();setInterval(()=>{if(!document.hidden)sync();},2500);
document.addEventListener('visibilitychange',()=>{if(!document.hidden){loadSites();sync();}});
