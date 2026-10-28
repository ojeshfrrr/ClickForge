export function renderHome(release) {
  const version = release?.version;
  const direct = name => '/downloads/' + version + '/' + name;
  const installer = version ? direct('ClickForge-Setup-v' + version + '.exe') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const portable = version ? direct('ClickForge-v' + version + '-win-x64.zip') : 'https://github.com/ojeshfrrr/ClickForge/releases/latest';
  const source = version ? 'Version ' + version + ' · Windows 10 / 11' : 'Windows 10 / 11 · Download from the official GitHub release';
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="theme-color" content="#1e3bd4"><meta name="description" content="ClickForge is a Windows click-control utility with custom speeds, shortcuts and profiles."><title>ClickForge — Click control for Windows</title><link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin><link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&display=swap" rel="stylesheet"><style>
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:#f0f2ef;color:#101b30;font:16px/1.55 'Space Grotesk',system-ui,sans-serif}
a{color:inherit}
a:focus-visible{outline:3px solid #ff7459;outline-offset:4px}
.wrap{width:min(1320px,calc(100% - 72px));margin:auto}
header{height:82px;display:flex;align-items:center;justify-content:space-between;gap:20px}
.brand{display:flex;align-items:center;gap:10px;text-decoration:none;font-size:21px;font-weight:700;letter-spacing:-.06em}
.brand-mark{display:grid;place-items:center;width:32px;height:32px;background:#213fdb;color:white;border-radius:50%}
.brand-mark svg{width:18px}
nav{display:flex;align-items:center;gap:34px;font-size:14px;font-weight:600}
nav a{text-decoration:none}
nav a:hover{text-decoration:underline}
.nav-download{background:#101b30;color:white;padding:11px 18px;border-radius:4px}

.hero{background:#1e3bd4;color:#fff;overflow:hidden}
.hero-inner{position:relative;display:grid;grid-template-columns:1fr .85fr;gap:50px;min-height:610px;align-items:center;padding-top:68px;padding-bottom:70px}
.hero-copy{position:relative;z-index:2}
.label{font-size:12px;letter-spacing:.12em;text-transform:uppercase;font-weight:700}
.label:before{content:'';display:inline-block;width:9px;height:9px;background:#fc806a;margin-right:9px;border-radius:50%}
.hero h1{font-size:clamp(64px,8.5vw,120px);line-height:.88;letter-spacing:-.085em;margin:28px 0 28px;max-width:760px}
.hero p{font-size:18px;color:#dce3ff;max-width:450px;margin:0}
.actions{display:flex;flex-wrap:wrap;gap:10px;margin:31px 0 15px}
.btn{display:inline-flex;align-items:center;justify-content:center;gap:10px;min-height:54px;padding:13px 20px;text-decoration:none;font-weight:700;font-size:14px;border:1px solid #ffffff70;border-radius:4px;transition:transform .15s,background .15s}
.btn:hover{transform:translateY(-2px);background:#ffffff18}
.btn-main{color:#132043;background:#fff;border-color:#fff}
.btn-main:hover{background:#e8ebff}
.btn svg{width:18px;height:18px}
.meta{font-size:12px;color:#cbd6ff}
.hero-art{position:relative;min-height:420px;display:flex;align-items:center}
.hero-art:before{content:'CLICK';position:absolute;top:-28px;right:-110px;font-weight:700;font-size:clamp(125px,15vw,210px);letter-spacing:-.11em;color:#ffffff12;line-height:1}
.hero-art:after{content:'';position:absolute;inset:10% -45% -20% 32%;border:1px solid #ffffff36;border-radius:50%;transform:rotate(-30deg);pointer-events:none}
.screen{position:relative;z-index:1;width:115%;max-width:690px;border:8px solid #111a32;box-shadow:18px 18px 0 #112579;border-radius:9px;background:#111a32}
.screen img{display:block;width:100%;height:auto}
.screen-label{position:absolute;bottom:-48px;left:0;color:#d4ddff;font-size:12px}

.ticker{border-bottom:1px solid #c3c9cb}
.ticker-inner{display:flex;align-items:center;justify-content:space-between;gap:22px;min-height:74px;font-size:13px;font-weight:600}
.ticker-inner span{display:flex;gap:9px;align-items:center}
.ticker svg{width:17px;height:17px;color:#203ed7}
.intro{display:grid;grid-template-columns:1fr 1fr;gap:70px;padding:110px 0 90px}
.intro h2{margin:0;font-size:clamp(44px,5.5vw,76px);letter-spacing:-.075em;line-height:1}
.intro-right{align-self:end}
.intro-right p{margin:0;max-width:510px;font-size:18px;color:#4f5d6d}
.feature-list{border-top:1px solid #a6afb7}
.feature{display:grid;grid-template-columns:58px 1fr 1fr;align-items:start;gap:25px;padding:31px 0;border-bottom:1px solid #c3c9cb}
.feature .icon{display:grid;place-items:center;width:42px;height:42px;border:1px solid #9ca9b5;border-radius:50%;color:#263fd6}
.icon svg{width:20px}
.feature h3{font-size:24px;letter-spacing:-.04em;margin:4px 0}
.feature p{font-size:15px;color:#536273;margin:5px 0;max-width:440px}
.end{margin-top:100px;background:#d9e1ff;padding:64px 68px;display:flex;align-items:center;justify-content:space-between;gap:35px}
.end h2{font-size:clamp(38px,5vw,62px);line-height:1;letter-spacing:-.07em;margin:0 0 15px}
.end p{margin:0;color:#4b596a}
.end .btn-main{background:#203ed5;border-color:#203ed5;color:white;white-space:nowrap}
.end .btn-main:hover{background:#162caf}
footer{display:flex;justify-content:space-between;gap:20px;padding:40px 0 48px;color:#526074;font-size:13px}
footer a{font-weight:600}

@media(max-width:900px){.wrap{width:calc(100% - 48px)}
.hero-inner{grid-template-columns:1fr;gap:55px;padding:65px 0 90px}
.hero-art{min-height:0;max-width:680px}
.screen{width:100%}
.hero-art:before{font-size:130px;right:0;top:-60px}
.ticker-inner{flex-wrap:wrap;padding:22px 0}
.intro{grid-template-columns:1fr;gap:22px;padding:75px 0 50px}
.feature{grid-template-columns:50px 1fr}
.feature p{grid-column:2}
.end{margin-top:70px;padding:42px;flex-wrap:wrap}
}

@media(max-width:520px){.wrap{width:calc(100% - 36px)}
header{height:70px}
nav{gap:15px}
nav .features-link{display:none}
.nav-download{padding:9px 12px;font-size:12px}
.hero-inner{padding:55px 0 90px}
.hero h1{font-size:64px}
.hero p{font-size:16px}
.actions .btn{width:100%}
.screen{border-width:5px;box-shadow:9px 9px 0 #112579}
.hero-art:before{font-size:95px;top:-55px}
.ticker-inner{justify-content:flex-start}
.intro{padding:65px 0 40px}
.feature{gap:14px;padding:24px 0}
.feature h3{font-size:21px}
.end{padding:30px}
.end .btn{width:100%}
footer{flex-direction:column}
}

@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}
.btn{transition:none}
}

</style></head><body>
<header class="wrap"><a href="/" class="brand"><span class="brand-mark" aria-hidden="true"><i data-lucide="mouse-pointer-2">↗</i></span>ClickForge</a><nav aria-label="Main navigation"><a class="features-link" href="#features">Features</a><a href="https://github.com/ojeshfrrr/ClickForge">Source</a><a class="nav-download" href="#download">Get ClickForge</a></nav></header>
<main><section class="hero" id="download"><div class="wrap hero-inner"><div class="hero-copy"><div class="label">A Windows desktop utility</div><h1>Clicks,<br>under your<br>control.</h1><p>Set a speed, choose a button, and get on with it. ClickForge puts the controls right where you need them.</p><div class="actions"><a class="btn btn-main" href="${installer}"><i data-lucide="download" aria-hidden="true">↓</i>Download for Windows</a><a class="btn" href="${portable}">Portable version</a></div><div class="meta">${source}</div></div><div class="hero-art"><div class="screen"><img src="/preview.png" width="1000" height="880" alt="The ClickForge app showing its Minecraft Bedwars button setup"><div class="screen-label">The real ClickForge app, shown in its Bedwars setup.</div></div></div></div></section>
<div class="ticker"><div class="wrap ticker-inner"><span><i data-lucide="mouse-pointer-2" aria-hidden="true">•</i>Mouse &amp; keyboard output</span><span><i data-lucide="keyboard" aria-hidden="true">•</i>Custom shortcuts</span><span><i data-lucide="shield-check" aria-hidden="true">•</i>Emergency stop</span><span><i data-lucide="flask-conical" aria-hidden="true">•</i>Safe click test</span></div></div>
<section class="wrap" id="features"><div class="intro"><h2>A small app.<br>A lot of control.</h2><div class="intro-right"><p>From a simple fixed speed to your own side-button setup, everything you need lives in one straightforward window.</p></div></div><div class="feature-list"><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="sliders-horizontal">◉</i></span><h3>Set it your way</h3><p>Choose your speed and mouse or keyboard output. Save configurations as profiles for next time.</p></article><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="mouse">◉</i></span><h3>Put side buttons to work</h3><p>Switch between the regular clicker and dedicated mouse-button layouts, including Minecraft Bedwars.</p></article><article class="feature"><span class="icon" aria-hidden="true"><i data-lucide="shield-check">◉</i></span><h3>Stay in control</h3><p>Use global shortcuts to start or pause, stop in an emergency, and try settings in the safe click test.</p></article></div><div class="end"><div><h2>Make it yours.</h2><p>Download ClickForge and set up your controls in minutes.</p></div><a class="btn btn-main" href="${installer}"><i data-lucide="download" aria-hidden="true">↓</i>Download for Windows</a></div></section></main>
<footer class="wrap"><span>ClickForge — built for Windows.</span><a href="https://github.com/ojeshfrrr/ClickForge/releases">Release notes &amp; checksums</a></footer><script src="https://unpkg.com/lucide@0.468.0/dist/umd/lucide.min.js"></script><script>if(window.lucide) window.lucide.createIcons();</script></body></html>`;
}
