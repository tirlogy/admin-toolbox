# Builds the static shell in index.html from AppText.resx (English) and the Razor drawings.
import re, sys, html, xml.etree.ElementTree as ET
R = sys.argv[1]; W = R + '/src/AdminToolbox.Web'
en = {d.get('name'): d.find('value').text for d in ET.parse(W + '/AppText.resx').getroot().findall('data')}
def t(k, tag='span', attrs=''):
    return f'<{tag}{attrs} data-i18n="{k}">{html.escape(en[k], quote=False)}</{tag}>'
icon_src = open(W + '/Layout/Icon.razor').read()
def icon(name):
    m = re.search(r'case "%s":\s*\n\s*(.*?)\n\s*break;' % name, icon_src) if name != 'system' else re.search(r'default:\s*\n\s*(.*?)\n\s*break;', icon_src)
    return '<svg class="icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">' + m.group(1).strip() + '</svg>'
logo = re.search(r'(<svg class="logo".*?</svg>)', open(W + '/Layout/Logo.razor').read(), re.S).group(1)
logo = re.sub(r'\s*\n\s*', '', logo).replace('width="@Size" height="@Size"', 'width="32" height="32"')
hr_src = open(W + '/Pages/HeroReport.razor').read()
rows = re.findall(r'\("([^"]+)", "(\w+)", "([^"]+)", (\d+)\)', hr_src)
hero = ['<svg class="hero-report" viewBox="0 0 360 300" aria-hidden="true" focusable="false">']
hero += [l.strip() for l in re.search(r'focusable="false">\n(.*?)\n\s*@foreach', hr_src, re.S).group(1).split('\n')]
for label, cls, word, y in rows:
    y = int(y)
    hero.append(f'<text x="28" y="{y+17}" class="hr-label">{label}</text><rect x="244" y="{y}" width="88" height="26" rx="13" class="hr-pill {cls}" /><circle cx="262" cy="{y+13}" r="5" class="hr-pill-dot {cls}" /><text x="274" y="{y+18}" class="hr-pill-text {cls}">{word}</text>')
hero.append('</svg>')
hero = ''.join(hero)
tools = [('mail','mail'),('header','header'),('subnet','subnet'),('diff','dns-diff'),('helpers','helpers')]
guide = [('mail','mail'),('header','header'),('diff','dns-diff'),('subnet','subnet')]
I = '      '
out = []
out.append('''    <!-- Static shell: same markup as MainLayout + Home (English), painted before WebAssembly starts.
         Generated from AppText.resx and the Razor drawings by tools/build-shell.py. -->
    <header class="site-header">
      <div class="container header-inner">
        <a class="brand" href="">''' + logo + '''<span>Admin Toolbox</span></a>
        <div class="header-controls"></div>
      </div>
      <nav class="container main-nav" aria-label="Tools" data-i18n-label="nav">''')
for k, h in tools:
    out.append(f'{I}  <a href="{h}">{icon(k)}{t("tool."+k, attrs=" class=\"nav-long\"")}{t("tool."+k+".short", attrs=" class=\"nav-short\"")}</a>')
out.append('''      </nav>
    </header>
    <main id="main" class="container">
      <div class="shell-route"><h1></h1><p class="lead"></p></div>
      <p class="loading shell-loading" role="status" data-i18n="loading">''' + en['loading'] + '''</p>
      <div class="shell-home home">''')
out.append(f'{I}<section class="hero" aria-labelledby="home-title">')
out.append(f'{I}  <div class="hero-text">')
out.append(f'{I}    {t("home.eyebrow","p",chr(32)+"class=\"eyebrow\"")}')
out.append(f'{I}    {t("home.title","h1",chr(32)+"id=\"home-title\"")}')
out.append(f'{I}    {t("home.lead","p",chr(32)+"class=\"lead\"")}')
out.append(f'{I}    <p class="actions">{t("home.cta.mail","a",chr(32)+"class=\"btn\" href=\"mail\"")}{t("home.cta.header","a",chr(32)+"class=\"btn btn-secondary\" href=\"header\"")}</p>')
out.append(f'{I}    <ul class="trust">' + ''.join(t(f"home.trust.{i}", "li") for i in range(1,5)) + '</ul>')
out.append(f'{I}  </div>')
out.append(f'{I}  {hero}')
out.append(f'{I}</section>')
out.append(f'{I}<section class="block" aria-labelledby="tools-title">')
out.append(f'{I}  {t("home.tools.title","h2",chr(32)+"id=\"tools-title\"")}')
out.append(f'{I}  <ul class="tool-grid">')
for k, h in tools:
    out.append(f'{I}    <li><a class="tool-card" href="{h}"><span class="tool-icon">{icon(k)}</span>{t("tool."+k, attrs=" class=\"tool-name\"")}{t("tool."+k+".desc", attrs=" class=\"tool-desc\"")}</a></li>')
out.append(f'{I}  </ul>')
out.append(f'{I}</section>')
out.append(f'{I}<section class="block why" aria-labelledby="why-title">')
out.append(f'{I}  <div class="prose">{t("home.why.title","h2",chr(32)+"id=\"why-title\"")}' + ''.join(t(f"home.why.{i}","p") for i in (1,2,3)) + '</div>')
out.append(f'{I}  <div class="rules">{t("home.rules.title","h3")}<dl>' + ''.join(f'<div><dt>{n}</dt>{t("home.rules."+k,"dd")}</div>' for n,k in (("SPF","spf"),("DKIM","dkim"),("DMARC","dmarc"))) + '</dl>')
out.append(f'{I}    {t("home.rules.note","p",chr(32)+"class=\"small muted\"")}')
out.append(f'{I}    <ul class="sources"><li>{t("home.source.google","a",chr(32)+"href=\"https://support.google.com/a/answer/81126\" rel=\"noopener\"")}</li><li>{t("home.source.yahoo","a",chr(32)+"href=\"https://senders.yahooinc.com/best-practices/\" rel=\"noopener\"")}</li></ul>')
out.append(f'{I}  </div>')
out.append(f'{I}</section>')
out.append(f'{I}<section class="block" aria-labelledby="guide-title">')
out.append(f'{I}  {t("home.guide.title","h2",chr(32)+"id=\"guide-title\"")}')
out.append(f'{I}  <ol class="steps">')
for n, (k, h) in enumerate(guide, 1):
    out.append(f'{I}    <li class="step"><h3>{t(f"home.guide.{n}.title","a",chr(32)+f"href=\"{h}\"")}</h3>{t(f"home.guide.{n}","p")}<p class="step-tool">{icon(k)}{t("tool."+k)}</p></li>')
out.append(f'{I}  </ol>')
out.append(f'{I}</section>')
out.append(f'{I}<section class="block split">')
out.append(f'{I}  <div class="note-card">{t("home.privacy.title","h2")}{t("home.privacy","p")}</div>')
out.append(f'{I}  <div class="note-card">{t("home.tech.title","h2")}{t("home.tech","p")}<p>{t("home.tech.link","a",chr(32)+"href=\"https://github.com/tirlogy/admin-toolbox\" rel=\"noopener\"")}</p></div>')
out.append(f'{I}</section>')
out.append('      </div>')
shell = '\n'.join(out) + '\n'
p = W + '/wwwroot/index.html'; s = open(p).read()
a = s.index('    <!-- Static shell'); b = s.index('      <noscript>')
s = s[:a] + shell + s[b:]
open(p, 'w').write(s)

# boot.js SHELL: DE/TR texts for every key the shell uses
import json
keys = list(dict.fromkeys(re.findall(r'data-i18n(?:-label)?="([^"]+)"', s)))
shellj = {}
for lang in ('de', 'tr'):
    tr = {d.get('name'): d.find('value').text for d in ET.parse(f'{W}/AppText.{lang}.resx').getroot().findall('data')}
    shellj[lang] = {k: tr[k] for k in keys}
bp = W + '/wwwroot/js/boot.js'; b = open(bp).read()
x = b.index('  const SHELL = '); y = b.index('  /*END*/')
b = b[:x] + '  const SHELL = ' + json.dumps(shellj, ensure_ascii=False, indent=2).replace('\n', '\n  ') + ';\n' + b[y:]
open(bp, 'w').write(b)
print('keys', len(keys))
