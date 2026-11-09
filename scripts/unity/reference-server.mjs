// Local-only server for inspecting unmodified upstream TS with ego-browser.
// Node 22 strips types on demand; no browser bundle/dependency installation required.
import http from 'node:http'
import fs from 'node:fs'
import path from 'node:path'
import { stripTypeScriptTypes } from 'node:module'
import { fileURLToPath } from 'node:url'
const root=fileURLToPath(new URL('../../',import.meta.url))
const page=`<!doctype html><html><head><title>LivLiv upstream visual reference</title></head><body style="background:#454545;color:white;font-family:sans-serif"><h1>Upstream watermark reference</h1><div id="views"></div><script type="module">
import * as brand from '/src/runtime/brand.ts';
window.renderMark=({w,h,ms,center,dpr=1})=>{const c=document.createElement('canvas');c.width=w;c.height=h;const g=c.getContext('2d');if(center)brand.drawWatermark(g,w,h,ms,dpr);else brand.drawCornerLogo(g,w,h,ms,dpr);document.querySelector('#views').replaceChildren(c);return c.toDataURL('image/png')};
window.renderNeutral=async(id)=>{const {Puppet}=await import('/src/runtime/puppet.ts');const {Mark}=await import('/src/mark.ts');const c=document.createElement('canvas');c.width=512;c.height=768;document.querySelector('#views').replaceChildren(c);const puppet=new Puppet(c);let mark;try{puppet.size=[512,768];await puppet.load('/characters/'+id+'/model.json',{pack:false});puppet.update(0);puppet.render();const m=puppet.model;mark=new Mark(puppet.gl);const kind=m.meta?m.meta.watermark?.kind??'none':m.watermark==='LivLiv'?'livliv':m.watermark?'text':'none';mark.set(kind,m.watermark??null,m.meta?.watermark?.center!==false);mark.draw(0,1);return c.toDataURL('image/png')}finally{mark?.dispose();puppet.dispose()}};
window.ready=true;
</script></body></html>`
http.createServer((req,res)=>{
  try{
    const url=new URL(req.url,'http://localhost');if(url.pathname==='/'){res.setHeader('Content-Type','text/html');res.end(page);return}
    let p=path.resolve(root,'.'+decodeURIComponent(url.pathname));if(!p.startsWith(root))throw Error('outside root')
    if(!fs.existsSync(p)&&fs.existsSync(p+'.ts'))p+='.ts'
    if(p.endsWith('.ts')){res.setHeader('Content-Type','text/javascript');res.end(stripTypeScriptTypes(fs.readFileSync(p,'utf8'))+(url.searchParams.has('helpers')&&p.endsWith('/runtime/puppet.ts')?'\nexport { alphaGrid, coverAt, closedLashLine, nasalRoom };':''))}
    else {const ext=path.extname(p);res.setHeader('Content-Type',({'.png':'image/png','.webp':'image/webp','.json':'application/json'})[ext]??'application/octet-stream');res.end(fs.readFileSync(p))}
  }catch(e){res.statusCode=404;res.end(String(e))}
}).listen(8769,'127.0.0.1',()=>console.log('Upstream reference: http://127.0.0.1:8769'))
