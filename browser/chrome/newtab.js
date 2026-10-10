const base = 'http://127.0.0.1:47831';
let revision = '', loading = false;
function setTheme(data) {
  const p = data.palette;
  if (!p || ![p.Accent, p.Background, p.Surface, p.Text].every(x => /^#[0-9a-f]{6}$/i.test(x))) throw Error('Invalid palette');
  for (const key of ['Accent', 'Background', 'Surface', 'Text']) document.documentElement.style.setProperty('--' + key.toLowerCase(), p[key]);
  const c = p.Accent.slice(1).match(/../g).map(x => parseInt(x,16)/255).map(x => x <= .04045 ? x/12.92 : ((x+.055)/1.055)**2.4);
  document.documentElement.style.setProperty('--button-text', (.2126*c[0]+.7152*c[1]+.0722*c[2]) < .18 ? '#fff' : '#171a22');
  document.documentElement.style.colorScheme = p.Dark ? 'dark' : 'light';
  document.getElementById('name').textContent = data.name;
}
async function sync() {
  if (loading) return;
  loading = true;
  try {
    const response = await fetch(base + '/theme', {signal: AbortSignal.timeout(3000)});
    if (!response.ok) throw Error('No applied theme');
    const data = await response.json();
    if (data.revision !== revision) {
      setTheme(data);
      await chrome.storage.local.set({theme: data});
      revision = data.revision;
    }
    document.getElementById('connection').textContent = '桌面配色已同步';
  } catch {
    document.getElementById('connection').textContent = '请启动 DeskShift 并应用壁纸 · 离线保留上次配色';
  } finally { loading = false; }
}
function tick() {
  const now = new Date();
  document.getElementById('clock').textContent = now.toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit',hour12:false});
  document.getElementById('date').textContent = now.toLocaleDateString('zh-CN',{month:'long',day:'numeric',weekday:'long'});
}
document.getElementById('search').addEventListener('submit', e => {
  e.preventDefault();
  const text = document.getElementById('query').value.trim();
  if (!text) return;
  const address = /^(https?:\/\/)/i.test(text) ? text : /^[\w.-]+\.[a-z]{2,}(\/[^\s]*)?$/i.test(text) ? 'https://' + text : '';
  location.href = address || 'https://www.google.com/search?q=' + encodeURIComponent(text);
});
(async () => {const saved = await chrome.storage.local.get('theme');if(saved.theme) {try{setTheme(saved.theme);}catch{}}await sync();})();
tick();setInterval(tick,1000);setInterval(sync,2500);
