const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const theme={revision:'test',palette:{Accent:'#587A68',Background:'#EEF4F0',Text:'#253C30'}};
const sites=[{title:'GitHub',url:'https://github.com/'},{title:'Duplicate',url:'https://github.com/explore'},{title:'Blocked',url:'javascript:alert(1)'},{title:'Local',url:'file:///private'},{title:'Bad URL',url:'%%%'}];
for(let i=0;i<10;i++)sites.push({title:'Site '+i,url:'https://example'+i+'.com/'});
async function check(browser,offline){
 const folder='browser/'+browser,manifest=JSON.parse(fs.readFileSync(folder+'/manifest.json','utf8'));
 assert.deepEqual(manifest.permissions,['storage','topSites','favicon']);assert.deepEqual(manifest.host_permissions,['http://127.0.0.1:47831/*']);assert.equal(manifest.chrome_url_overrides.newtab,'newtab.html');assert.ok(!manifest.permissions.includes('history'));
 const nodes={},styles={},requests=[],saved=[],timers=[],localData=offline?{theme}:{};let cached;
 const node=()=>({children:[],value:'',style:{setProperty(k,v){this[k]=v;}},classList:{add(){},remove(){}},append(...items){this.children.push(...items);},replaceChildren(){this.children=[];},setAttribute(k,v){this[k]=v;},showModal(){this.open=true;},close(){this.open=false;},addEventListener(type,fn){this[type]=fn;},hidden:false});
 const document={hidden:false,documentElement:{dataset:{},style:{setProperty(k,v){styles[k]=v;}}},getElementById:id=>nodes[id] ||= node(),createElement:node,addEventListener(type,fn){this[type]=fn;}};
 class LocalURL extends URL{static createObjectURL(){return 'blob:local-preview';}static revokeObjectURL(){}}
 const context=vm.createContext({document,location:{href:''},URL:LocalURL,Image:class{async decode(){}},Response,Blob,AbortSignal:{timeout:()=>({})},setInterval:fn=>timers.push(fn),
 chrome:{runtime:{getManifest:()=>manifest,getURL:path=>'chrome-extension://test-id'+path},topSites:{get:fn=>fn(sites)},storage:{local:{get:async()=>({...localData}),set:async value=>{Object.assign(localData,value);saved.push(value);}}}},
 caches:{open:async()=>({match:async()=>offline?new Response(new Blob(['original wallpaper bytes'],{type:'image/jpeg'})):undefined,put:async(key,response)=>cached=await response.blob()})},
 fetch:async url=>{requests.push(url);if(offline)throw Error('offline');return url.includes('/theme')?{ok:true,json:async()=>theme}:{ok:true,blob:async()=>new Blob(['original wallpaper bytes'],{type:'image/jpeg'})};}});
 vm.runInContext(fs.readFileSync(folder+'/newtab.js','utf8'),context);await vm.runInContext('ready',context);
 assert.equal(styles['--accent'],theme.palette.Accent);assert.ok(nodes.wallpaper.style.backgroundImage.includes('blob:'));
 assert.equal(document.documentElement.dataset.mode,'light');
 vm.runInContext("applyPalette({palette:{Accent:'#373748',Background:'#14171F',Text:'#F4F5FA',Dark:true}})",context);assert.equal(document.documentElement.dataset.mode,'dark');
 vm.runInContext('applyPalette('+JSON.stringify(theme)+')',context);assert.equal(document.documentElement.dataset.mode,'light','Switching away from dark mode restores light controls');
 assert.equal(nodes.sites.children.length,9);assert.equal(nodes.sites.children[0].children[0].href,'https://github.com/');assert.ok(nodes.sites.children.slice(0,8).every(tile=>tile.children[0].href.startsWith('https://')));
 const siteIcon=nodes.sites.children[0].children[0].children[0],favicon=new URL(siteIcon.children[0].src);
 assert.equal(siteIcon.textContent,undefined,'No generated initials or logos');assert.equal(favicon.protocol,'chrome-extension:');assert.equal(favicon.pathname,'/_favicon/');assert.equal(favicon.searchParams.get('pageUrl'),'https://github.com/');assert.equal(nodes.sites.style['--columns'],'9');
 siteIcon.children[0].error();assert.equal(siteIcon.children[0].hidden,true,'Missing favicon does not display a broken image');
 assert.equal(requests[0],'http://127.0.0.1:47831/theme?browser='+browser);assert.ok(requests.every(url=>url.startsWith('http://127.0.0.1:47831/')));
 assert.equal(saved.length,offline?0:1);if(!offline){assert.equal(await cached.text(),'original wallpaper bytes');let count=requests.length;await vm.runInContext('sync()',context);assert.equal(requests.length,count+1,'Unchanged revision does not redownload wallpaper');}
 document.getElementById('query').value='wallpaper colors';nodes.search.submit({preventDefault(){}});assert.equal(context.location.href,'https://www.'+(browser==='edge'?'bing':'google')+'.com/search?q=wallpaper%20colors');
 document.getElementById('query').value='github.com';nodes.search.submit({preventDefault(){}});assert.equal(context.location.href,'https://github.com/');
 nodes.sites.children.at(-1).click();assert.equal(nodes['site-editor'].open,true);nodes['site-title'].value='My notes';nodes['site-url'].value='notes.example.org';await nodes['site-form'].submit({preventDefault(){}});assert.equal(nodes['site-editor'].open,false);assert.equal(nodes.sites.children[0].children[0].href,'https://notes.example.org/');
 const pinned=saved.at(-1).shortcutPrefs;assert.equal(pinned.manual[0].title,'My notes');assert.ok(requests.every(url=>!url.includes('notes.example.org')),'Manual URLs are not sent to the desktop bridge');
 await vm.runInContext('loadSites()',context);assert.equal(nodes.sites.children[0].children[0].href,'https://notes.example.org/','Pinned sites survive preferences reload');
 nodes.sites.children[0].children[1].click();nodes['site-title'].value='Edited notes';await nodes['site-form'].submit({preventDefault(){}});assert.equal(saved.at(-1).shortcutPrefs.manual.length,1);assert.equal(saved.at(-1).shortcutPrefs.manual[0].title,'Edited notes');
 nodes.sites.children.at(-1).click();nodes['site-url'].value='javascript:alert(1)';await nodes['site-form'].submit({preventDefault(){}});assert.ok(nodes['site-error'].textContent);assert.equal(nodes['site-editor'].open,true);nodes['cancel-site'].click();
 nodes.sites.children.at(-1).click();nodes['auto-sites'].checked=false;await nodes['site-form'].submit({preventDefault(){}});assert.equal(nodes.sites.children.length,2,'Manual-only mode excludes history suggestions');
 nodes.sites.children[0].children[1].click();await nodes['remove-site'].click();assert.equal(saved.at(-1).shortcutPrefs.manual.length,0);assert.equal(nodes.sites.children.length,1);assert.equal(nodes.empty.hidden,false);
 nodes.sites.children.at(-1).click();nodes['auto-sites'].checked=true;await nodes['site-form'].submit({preventDefault(){}});await vm.runInContext('loadSites()',context);assert.equal(nodes.sites.children.length,9);
 nodes.sites.children[0].children[1].click();await nodes['remove-site'].click();assert.ok(saved.at(-1).shortcutPrefs.hiddenHosts.includes('github.com'));assert.ok(!nodes.sites.children.slice(0,-1).some(tile=>tile.children[0].href==='https://github.com/'));
 nodes.sites.children.at(-1).click();await nodes['restore-sites'].click();assert.equal(saved.at(-1).shortcutPrefs.hiddenHosts.length,0);nodes['cancel-site'].click();
 const html=fs.readFileSync(folder+'/newtab.html','utf8');assert.ok(!html.includes('<time'));assert.ok(!html.includes('id="clock"'));assert.ok(!html.includes('id="date"'));
 const count=requests.length;document.hidden=true;timers[0]();assert.equal(requests.length,count,'Inactive tabs do not poll');
 console.log('PASS '+browser+': '+(offline?'offline cache':'live wallpaper')+', automatic/manual sites, add/edit/delete/hide, unsafe URLs rejected, local storage, search');
}
(async()=>{for(const browser of ['chrome','edge'])for(const offline of [false,true])await check(browser,offline);})().catch(error=>{console.error(error);process.exitCode=1;});
