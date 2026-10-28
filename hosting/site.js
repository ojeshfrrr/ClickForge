export function renderHome(release) {
  const version = release?.version;
  const direct = name => '/downloads/' + version + '/' + name;
  const installer = version ? direct('ClickForge-Setup-v' + version + '.exe') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const portable = version ? direct('ClickForge-v' + version + '-win-x64.zip') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const source = version ? 'Version ' + version + ' · Windows 10 / 11' : 'Windows 10 / 11 · Download from the official GitHub release';
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="theme-color" content="#080a0d"><meta name="description" content="ClickForge puts click speed, mouse buttons and shortcuts in your hands."><title>ClickForge — Make every click count.</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin><link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700;800&display=swap" rel="stylesheet"><style>
:root{color-scheme:dark;--bg:#090b1b;--ink:#f4f5ff;--muted:#b0b5d2;--line:#ffffff20;--violet:#b9a4ff;--cyan:#8df3f5}

*{box-sizing:border-box}
 html{scroll-behavior:smooth}
 body{margin:0;background:#080a19;color:var(--ink);font:16px/1.55 'Outfit',system-ui,sans-serif;overflow-x:hidden}
 a{color:inherit}
 a:focus-visible{outline:3px solid #91f4fa;outline-offset:5px}
 .shell{width:min(1300px,calc(100% - 80px));margin:auto}

header{height:92px;display:flex;align-items:center;justify-content:space-between;gap:24px;border-bottom:1px solid var(--line)}
 .brand{display:flex;align-items:center;gap:12px;text-decoration:none;font-size:22px;font-weight:750;letter-spacing:-.04em}
 .mark{width:34px;height:34px;display:grid;place-items:center;border-radius:11px;background:linear-gradient(145deg,#95f3f6,#b8a4ff 65%,#f1a4e6);color:#10112a;box-shadow:0 0 32px #a389ff70}
 .mark:after{content:'✦';font-size:21px}
 nav{display:flex;align-items:center;gap:34px;color:#c3c6df;font-size:14px}
 nav a{text-decoration:none}
 nav a:hover{color:white}
 .nav-button{border:1px solid #8272ca;border-radius:9px;padding:10px 17px;color:#efebff;background:#ffffff0a}

.hero{position:relative;min-height:700px;display:grid;grid-template-columns:.92fr 1.08fr;align-items:center;gap:35px;padding:80px 0 105px}
 .hero:before{content:'';position:absolute;inset:-90px -20vw -40px;background:radial-gradient(ellipse at 75% 43%,#6938c954,transparent 35%),radial-gradient(ellipse at 34% 18%,#11647a38,transparent 33%),linear-gradient(#ffffff05 1px,transparent 1px),linear-gradient(90deg,#ffffff05 1px,transparent 1px);background-size:auto,auto,68px 68px,68px 68px;mask-image:linear-gradient(#000 10%,transparent 95%);pointer-events:none;z-index:-1}
 .hero-copy{position:relative;z-index:1}
 .chip{display:inline-flex;align-items:center;gap:9px;border:1px solid #a491ed66;border-radius:99px;padding:8px 14px;color:#ded6ff;background:#9d82ff18;font-size:12px;font-weight:600}
 .chip:before{content:'';width:7px;height:7px;border-radius:50%;background:#84f5e4;box-shadow:0 0 14px #84f5e4}
 .hero h1{font-size:clamp(62px,7.3vw,102px);line-height:.94;letter-spacing:-.065em;font-weight:750;margin:27px 0;max-width:680px}
 .hero h1 span{display:block;background:linear-gradient(95deg,#92f3ef 3%,#bca5ff 55%,#f9a9dc);color:transparent;background-clip:text}
 .hero p{font-size:19px;color:#c4c7dc;max-width:450px;margin:0}
 .actions{display:flex;flex-wrap:wrap;gap:12px;margin:34px 0 17px}
 .button{min-height:56px;display:inline-flex;align-items:center;justify-content:center;gap:10px;padding:14px 22px;border:1px solid #ffffff32;border-radius:10px;text-decoration:none;font-weight:650;font-size:14px;background:#ffffff08;transition:transform .2s,box-shadow .2s,background .2s}
 .button:hover{transform:translateY(-3px);background:#ffffff18}
 .primary{background:linear-gradient(110deg,#9ff2f1,#baa4ff 70%,#d8adff);border-color:#d1c8ff;color:#10102a;box-shadow:0 12px 40px #8d72f959}
 .primary:hover{background:linear-gradient(110deg,#b8ffff,#cdbdff);box-shadow:0 15px 45px #a58bff75}
 .meta{color:#aeb0c7;font-size:12px}
 .lucide{width:19px;height:19px;stroke-width:1.8;flex-shrink:0}

.stage{position:relative;min-width:0;display:flex;align-items:center;justify-content:center}
 .stage:before,.stage:after{content:'';position:absolute;border:1px solid #bb9cff65;border-radius:50%;width:108%;aspect-ratio:1;transform:rotate(-26deg) scaleY(.7);pointer-events:none;box-shadow:0 0 50px #8968ff15}
 .stage:after{width:127%;border-color:#74dfe544;transform:rotate(23deg) scaleY(.66)}
 .preview{position:relative;z-index:1;width:100%;padding:10px;border:1px solid #b7a7ff6b;border-radius:17px;background:linear-gradient(145deg,#7163a4,#1d1b36 25%,#11121e);box-shadow:0 32px 90px #000c,0 0 100px #8762ff40}
 .preview img{display:block;width:100%;height:auto;border-radius:9px}
 .caption{position:absolute;right:0;bottom:-52px;color:#bab3d8;font-size:12px}
.caption b{color:#b1fcfa;font-weight:600}

.strip{border-top:1px solid var(--line);border-bottom:1px solid var(--line);display:flex;justify-content:space-between;gap:20px;padding:23px 0;color:#d0d2e4;font-size:13px}
.strip span:before{content:'✦';color:#9cf6f1;margin-right:10px}

.features{padding:110px 0 105px;display:grid;grid-template-columns:.8fr 1.2fr;gap:100px}
.features h2{font-size:clamp(40px,4.5vw,64px);line-height:1.04;letter-spacing:-.055em;margin:0 0 20px}
.features .intro{color:var(--muted);max-width:340px}
.feature-list{border-top:1px solid var(--line)}
.feature{display:grid;grid-template-columns:52px 1fr;gap:20px;padding:28px 0;border-bottom:1px solid var(--line)}
.icon{width:48px;height:48px;display:grid;place-items:center;border:1px solid #aa9cff4d;border-radius:13px;color:#bcb0ff;background:#a88aff16;font-size:20px}
.feature:nth-child(2) .icon{color:#93ecee;background:#70d8ed16}
.feature:nth-child(3) .icon{color:#f4a8e0;background:#f09bd416}
.feature h3{font-size:20px;letter-spacing:-.025em;margin:0 0 6px}
.feature p{margin:0;color:#b8bdd5;font-size:15px;max-width:520px}

.end{position:relative;overflow:hidden;padding:70px;border:1px solid #ac9aff4a;border-radius:20px;background:radial-gradient(circle at 90% 0,#996dfc5e,transparent 42%),radial-gradient(circle at 0 100%,#26c2c532,transparent 40%),#14152d;display:flex;justify-content:space-between;align-items:center;gap:30px}
.end h2{font-size:clamp(34px,4vw,54px);line-height:1.1;letter-spacing:-.055em;margin:0 0 12px}
.end p{color:#cbcae2;margin:0;max-width:500px}
.end .button{white-space:nowrap}
footer{display:flex;justify-content:space-between;gap:20px;padding:40px 0 50px;color:#a8adc6;font-size:13px}
footer a{color:#c7bbff}

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
.hero{padding:55px 0 85px;gap:56px;min-height:0}
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

</style></head><body><header class="shell"><a class="brand" href="/"><span class="mark" aria-hidden="true"></span>ClickForge</a><nav aria-label="Main navigation"><a class="nav-features" href="#features">Features</a><a href="https://github.com/ojeshfrrr/ClickForge">Source</a><a class="nav-button" href="#download">Get ClickForge</a></nav></header><main class="shell"><section class="hero" id="download"><div class="hero-copy"><span class="chip">A little magic for Windows</span><h1>Click less.<span>Do more.</span></h1><p>Meet the clicker that keeps things simple. Pick your speed, choose your controls, and make every click yours.</p><div class="actions"><a class="button primary" href="${installer}"><i data-lucide="download" aria-hidden="true">↓</i> Download for Windows</a><a class="button" href="${portable}">Portable version</a></div><div class="meta">${source}</div></div><div class="stage"><div class="preview"><img src="/preview.png" alt="ClickForge showing its Minecraft Bedwars button setup" width="1000" height="880"><div class="caption"><b>ClickForge</b> — a little control goes a long way.</div></div></div></section><div class="strip" aria-label="Highlights"><span>Mouse &amp; keyboard output</span><span>Custom shortcuts</span><span>Emergency stop</span><span>Safe click test</span></div><section class="features" id="features"><div><h2>Everything you need. Nothing in your way.</h2><p class="intro">A little app with thoughtful controls, built for the way you play and work.</p></div><div class="feature-list"><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="mouse-pointer-2">◉</i></span><div><h3>Make it yours</h3><p>Choose your click speed, mouse button or keyboard key. Save your favorite setups as profiles.</p></div></article><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="keyboard">⌘</i></span><div><h3>Side buttons, sorted</h3><p>Use a simple mouse-button setup or the dedicated Minecraft Bedwars layout.</p></div></article><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="shield-check">◇</i></span><div><h3>Always in control</h3><p>Pause, stop, or use the emergency shortcut. Explore your settings with the built-in safe test.</p></div></article></div></section><section class="end"><div><h2>Ready when you are.</h2><p>Your setup, your pace. Get ClickForge and make it feel just right.</p></div><a class="button primary" href="${installer}"><i data-lucide="download" aria-hidden="true">↓</i> Download for Windows</a></section></main><footer class="shell"><span>ClickForge · Made for Windows</span><a href="https://github.com/ojeshfrrr/ClickForge/releases">Release notes &amp; checksums</a></footer><script src="https://unpkg.com/lucide@0.468.0/dist/umd/lucide.min.js"></script><script>if(window.lucide) window.lucide.createIcons();</script></body></html>`;
}
