export function renderHome(release) {
  const version = release?.version;
  const direct = name => '/downloads/' + version + '/' + name;
  const installer = version ? direct('ClickForge-Setup-v' + version + '.exe') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const portable = version ? direct('ClickForge-v' + version + '-win-x64.zip') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const source = version ? 'Version ' + version + ' · Windows 10 / 11' : 'Windows 10 / 11 · Download from the official GitHub release';
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="theme-color" content="#080a0d"><meta name="description" content="ClickForge puts click speed, mouse buttons and shortcuts in your hands."><title>ClickForge — Your clicks. Your rules.</title>
<style>
:root{color-scheme:dark;--bg:#090b0f;--surface:#11141a;--line:#343332;--gold:#f4ce78;--muted:#adafb9;--ink:#f7f5ef}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--bg);color:var(--ink);font:16px/1.55 'Segoe UI',system-ui,sans-serif;overflow-x:hidden}
a{color:inherit}
a:focus-visible{outline:3px solid #ffe19a;outline-offset:5px}
 .shell{width:min(1280px,calc(100% - 72px));margin:auto}
header{height:90px;display:flex;align-items:center;justify-content:space-between;gap:24px;border-bottom:1px solid #ffffff20}
.brand{display:flex;align-items:center;gap:11px;text-decoration:none;font-size:21px;font-weight:750;letter-spacing:-.05em}
.mark{width:30px;height:30px;display:grid;place-items:center;background:linear-gradient(145deg,#ffe9a7,#b98b3e);color:#17120b;clip-path:polygon(0 0,100% 62%,57% 68%,38% 100%)}
.mark:after{content:'✦';font-size:12px}
nav{display:flex;align-items:center;gap:34px;color:#c5c4c2;font-size:14px}
nav a{text-decoration:none}
nav a:hover{color:#ffdc91}
.nav-button{border:1px solid #8c7446;border-radius:8px;padding:9px 17px;color:#f7e3b4}
.hero{position:relative;min-height:650px;display:grid;grid-template-columns:1fr 1fr;align-items:center;gap:48px;padding:90px 0 100px}
.hero:before{content:'';position:absolute;width:680px;height:680px;right:-90px;top:-15px;background:radial-gradient(circle,#8e642832 0,transparent 64%);pointer-events:none;z-index:-1}
.hero-copy{position:relative;z-index:1}
.chip{display:inline-flex;align-items:center;gap:9px;border:1px solid #5a4a2d;border-radius:99px;padding:7px 13px;color:#f2d89e;font-size:12px}
.chip:before{content:'';width:7px;height:7px;border-radius:100%;background:#f1c76a;box-shadow:0 0 12px #f1c76a}
.hero h1{font-size:clamp(58px,6.8vw,96px);line-height:.95;letter-spacing:-.075em;margin:27px 0 26px;max-width:650px}
.hero h1 span{display:block;color:#e9cb8f}
.hero p{font-size:18px;color:#c1bfc0;max-width:465px;margin:0}
.actions{display:flex;flex-wrap:wrap;gap:12px;margin:34px 0 16px}
.button{min-height:56px;display:inline-flex;align-items:center;justify-content:center;gap:10px;padding:14px 22px;border:1px solid #45413a;border-radius:9px;text-decoration:none;font-weight:650;font-size:14px;transition:transform .18s,background .18s}
.button:hover{transform:translateY(-2px);background:#26231e}
.primary{background:linear-gradient(135deg,#ffe3a0,#e2b65d);border-color:#f8db95;color:#16120c;box-shadow:0 10px 35px #e0b55b30}
.primary:hover{background:#ffe2a0}
.meta{color:#a5a2a0;font-size:12px}
.stage{position:relative;min-width:0;display:flex;align-items:center;justify-content:center}
.stage:before,.stage:after{content:'';position:absolute;border:1px solid #d7b46642;border-radius:50%;width:106%;aspect-ratio:1;transform:rotate(-23deg) scaleY(.66);pointer-events:none}
.stage:after{width:125%;border-color:#c69c4d24;transform:rotate(23deg) scaleY(.63)}
.preview{position:relative;z-index:1;width:100%;padding:9px;border:1px solid #8c744d;border-radius:15px;background:linear-gradient(145deg,#37312a,#101216 25%);box-shadow:0 32px 90px #000c,0 0 75px #bd965023}
.preview img{display:block;width:100%;height:auto;border-radius:8px}
.caption{position:absolute;right:0;bottom:-52px;color:#baaa8c;font-size:12px}
.caption b{color:#f6d995;font-weight:600}
.strip{border-top:1px solid #ffffff22;border-bottom:1px solid #ffffff22;display:flex;justify-content:space-between;gap:20px;padding:22px 0;color:#c6c4bd;font-size:13px}
.strip span:before{content:'✦';color:#e8c679;margin-right:10px}
.features{padding:105px 0 100px;display:grid;grid-template-columns:.8fr 1.2fr;gap:100px}
.features h2{font-size:clamp(38px,4.3vw,62px);line-height:1.06;letter-spacing:-.06em;margin:0 0 20px}
.features .intro{color:#adafb9;max-width:330px}
.feature-list{border-top:1px solid var(--line)}
.feature{display:grid;grid-template-columns:44px 1fr;gap:18px;padding:27px 0;border-bottom:1px solid var(--line)}
.icon{width:40px;height:40px;display:grid;place-items:center;border:1px solid #5e5137;border-radius:10px;color:#ffda8a;font-size:20px}
.feature h3{font-size:19px;letter-spacing:-.025em;margin:0 0 5px}
.feature p{margin:0;color:#b5b5bd;font-size:15px;max-width:510px}
.end{position:relative;overflow:hidden;padding:70px;border:1px solid #625239;border-radius:16px;background:radial-gradient(circle at 90% 0,#73552b55,transparent 42%),#141519;display:flex;justify-content:space-between;align-items:center;gap:30px}
.end h2{font-size:clamp(32px,3.8vw,52px);line-height:1.1;letter-spacing:-.055em;margin:0 0 12px}
.end p{color:#c6bfb3;margin:0;max-width:500px}
.end .button{white-space:nowrap}
footer{display:flex;justify-content:space-between;gap:20px;padding:40px 0 50px;color:#9c9da3;font-size:13px}
footer a{color:#d6c69f}
 @media(max-width:900px){.shell{width:calc(100% - 48px)}
.hero{grid-template-columns:1fr;gap:70px;padding:70px 0 90px}
.hero h1{max-width:700px}
.stage{max-width:700px;margin:auto}
.features{grid-template-columns:1fr;gap:35px;padding:75px 0}
.strip{flex-wrap:wrap}
.end{padding:45px;flex-wrap:wrap}
}
@media(max-width:520px){.shell{width:calc(100% - 36px)}
header{height:72px}
nav{gap:14px}
nav .nav-features{display:none}
.nav-button{padding:8px 12px}
.hero{padding:55px 0 85px;gap:56px}
.hero h1{font-size:57px}
.hero p{font-size:16px}
.actions .button{width:100%}
.strip{gap:12px;font-size:12px}
.features{padding:65px 0}
.end{padding:30px}
.end .button{width:100%}
footer{flex-direction:column}
}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}
.button{transition:none}
}

</style></head><body><header class="shell"><a class="brand" href="/"><span class="mark" aria-hidden="true"></span>ClickForge</a><nav aria-label="Main navigation"><a class="nav-features" href="#features">Features</a><a href="https://github.com/ojeshfrrr/ClickForge">Source</a><a class="nav-button" href="#download">Get ClickForge</a></nav></header><main class="shell"><section class="hero" id="download"><div class="hero-copy"><span class="chip">Made for Windows</span><h1>Your clicks.<span>Your rules.</span></h1><p>Meet the clicker that keeps things simple. Pick your speed, choose your controls, and make every click yours.</p><div class="actions"><a class="button primary" href="${installer}"><span aria-hidden="true">↓</span> Download for Windows</a><a class="button" href="${portable}">Portable version</a></div><div class="meta">${source}</div></div><div class="stage"><div class="preview"><img src="/preview.png" alt="ClickForge showing its Minecraft Bedwars button setup" width="1000" height="880"><div class="caption"><b>ClickForge</b> — a little control goes a long way.</div></div></div></section><div class="strip" aria-label="Highlights"><span>Mouse &amp; keyboard output</span><span>Custom shortcuts</span><span>Emergency stop</span><span>Safe click test</span></div><section class="features" id="features"><div><h2>Everything you need. Nothing in your way.</h2><p class="intro">A little app with thoughtful controls, built for the way you play and work.</p></div><div class="feature-list"><article class="feature"><span class="icon" aria-hidden="true">◉</span><div><h3>Make it yours</h3><p>Choose your click speed, mouse button or keyboard key. Save your favorite setups as profiles.</p></div></article><article class="feature"><span class="icon" aria-hidden="true">⌘</span><div><h3>Side buttons, sorted</h3><p>Use a simple mouse-button setup or the dedicated Minecraft Bedwars layout.</p></div></article><article class="feature"><span class="icon" aria-hidden="true">◇</span><div><h3>Always in control</h3><p>Pause, stop, or use the emergency shortcut. Explore your settings with the built-in safe test.</p></div></article></div></section><section class="end"><div><h2>Ready when you are.</h2><p>Your setup, your pace. Get ClickForge and make it feel just right.</p></div><a class="button primary" href="${installer}"><span aria-hidden="true">↓</span> Download for Windows</a></section></main><footer class="shell"><span>ClickForge · Made for Windows</span><a href="https://github.com/ojeshfrrr/ClickForge/releases">Release notes &amp; checksums</a></footer></body></html>`;
}
