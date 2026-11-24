#!/usr/bin/env python3
"""Build the GitHub Pages site from the repository documents.

Usage: python scripts/build_site.py [--out _site]
Environment: SITE_URL (default https://dahatake.github.io/StudyReport-Evaluator)

The build fails when an internal link, image, or anchor cannot be resolved.
"""
from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
import re
import shutil
import subprocess
import sys
import unicodedata
from pathlib import Path
from urllib.parse import unquote, urlparse

import markdown

ROOT = Path(__file__).resolve().parents[1]
SITE_SRC = ROOT / "site"
REPO_URL = "https://github.com/dahatake/StudyReport-Evaluator"
SITE_URL = os.environ.get("SITE_URL", "https://dahatake.github.io/StudyReport-Evaluator").rstrip("/")
BASE = urlparse(SITE_URL).path.rstrip("/")
SITE_NAME = "StudyReport Evaluator"
OG_IMAGE = "images/06-results-review.png"

TEACHERS = "teachers"
DEVELOPERS = "developers"
RELEASES = "releases"

# (source, section, slug, blurb)
PAGES: list[tuple[str, str, str, str]] = [
    ("docs/README.md", TEACHERS, "guide", "最初に読む順番と、やりたいことから探せる利用ガイドの入口です。"),
    ("docs/getting-started.md", TEACHERS, "getting-started", "入手方法から最初の採点、結果の出力までを順に進めます。"),
    ("docs/privacy-and-data-handling.md", TEACHERS, "privacy", "何がどこに保存され、何が外部へ送られるか。学校の許可の確認点。"),
    ("docs/features.md", TEACHERS, "features", "配点・評価項目・類似度など、点数が決まる仕組み。"),
    ("docs/settings.md", TEACHERS, "settings", "モデルや出力先、採点条件の保存と再利用。"),
    ("docs/result-excel-description.md", TEACHERS, "result-excel", "結果Excelの見方と、先生の点への修正方法。"),
    ("docs/custom-evaluator-guide.md", TEACHERS, "custom-evaluator", "自分の採点基準をAIへの指示文にする書き方。"),
    ("docs/troubleshooting.md", TEACHERS, "troubleshooting", "止まった・空欄がある・保存できないときの切り分け。"),
    ("README.md", TEACHERS, "product-readme", "配布形式、動作環境、SHA-256確認、Windowsの警告。"),
    ("docs/technical-guid.md", DEVELOPERS, "technical-guide", "内部構成と、変更・拡張するときの注意。"),
    ("dev/docs/architecture.md", DEVELOPERS, "architecture", "プロジェクト境界、実行データの流れ、AI境界、点数境界。"),
    ("dev/docs/excel-contract.md", DEVELOPERS, "excel-contract", "入出力Excelの契約。"),
    ("docs/prompt-launch.md", DEVELOPERS, "prompt-launch", "コマンドや起動引数からの起動。"),
    ("docs/release-automation.md", DEVELOPERS, "release-automation", "配布物の作成と公開前の検証。"),
    ("dev/docs/release.md", DEVELOPERS, "release", "リリースの手順と判断基準。"),
    ("dev/docs/release/verification-scope-0.8.6.md", DEVELOPERS, "verification-scope-0.8.6", "公開v0.8.6の確認範囲と、2026-11-25の再確認結果。"),
    ("dev/docs/version-management.md", DEVELOPERS, "version-management", "製品版の管理規則。"),
    ("docs/third-party-notices.md", DEVELOPERS, "third-party-notices", "依存ライブラリのライセンス表記。"),
    ("CHANGELOG.md", RELEASES, "changelog", "リリースごとの変更履歴。"),
]
for _adr in sorted((ROOT / "dev/docs/adr").glob("*.md")):
    PAGES.append((f"dev/docs/adr/{_adr.name}", DEVELOPERS, f"adr-{_adr.stem[:4]}", ""))

SECTION_LABEL = {TEACHERS: "先生向け", DEVELOPERS: "開発者向け", RELEASES: "リリース"}
SECTION_HOME = {TEACHERS: "/teachers/", DEVELOPERS: "/developers/", RELEASES: "/releases/"}

ALERTS = {
    "NOTE": ("note", "ノート"),
    "TIP": ("tip", "ヒント"),
    "IMPORTANT": ("important", "重要"),
    "WARNING": ("warning", "警告"),
    "CAUTION": ("caution", "注意"),
}


def slugify(value: str, separator: str = "-") -> str:
    """Approximate GitHub heading anchors so existing in-repo links keep working."""
    value = unicodedata.normalize("NFKC", html.unescape(re.sub(r"<[^>]+>", "", value))).lower().strip()
    value = re.sub(r"[^\w\- 　]", "", value)
    return re.sub(r"[ 　]", separator, value)


def url_for(section: str, slug: str) -> str:
    return f"/{section}/{slug}/"


def abs_url(path: str) -> str:
    return f"{SITE_URL}{path}"


def href(path: str) -> str:
    return f"{BASE}{path}"


def git_lastmod(path: Path) -> str:
    try:
        out = subprocess.run(
            ["git", "log", "-1", "--format=%cI", "--", str(path.relative_to(ROOT))],
            cwd=ROOT, capture_output=True, text=True, check=True,
        ).stdout.strip()
        if out:
            return out
    except (subprocess.CalledProcessError, FileNotFoundError):
        pass
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat()


class Page:
    def __init__(self, src: str, section: str, slug: str, blurb: str):
        self.src = ROOT / src
        self.rel = src
        self.section = section
        self.slug = slug
        self.blurb = blurb
        self.url = url_for(section, slug)
        self.title = ""
        self.description = ""
        self.body = ""
        self.toc: list[dict] = []
        self.ids: set[str] = set()
        self.has_mermaid = False
        self.text = ""
        self.lastmod = git_lastmod(self.src)


class Builder:
    def __init__(self, out: Path):
        self.out = out
        self.pages = [Page(*p) for p in PAGES]
        self.by_rel = {p.rel: p for p in self.pages}
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.sitemap: list[tuple[str, str]] = []
        self.search: list[dict] = []
        self.link_targets: list[tuple[str, str, str]] = []  # (from url, target url, anchor)
        self.page_ids: dict[str, set[str]] = {}
        self.used_images: set[str] = set()

    # ---------- markdown ----------
    def render_markdown(self, page: Page) -> None:
        text = page.src.read_text(encoding="utf-8")
        md = markdown.Markdown(
            extensions=["tables", "fenced_code", "toc", "sane_lists"],
            extension_configs={"toc": {"slugify": slugify, "toc_depth": "2-3", "permalink": False}},
        )
        body = md.convert(text)
        m = re.search(r"<h1[^>]*>(.*?)</h1>", body, re.S)
        raw_title = re.sub(r"<[^>]+>", "", m.group(1)) if m else page.slug
        page.title = html.unescape(raw_title).strip()
        if m:
            body = body.replace(m.group(0), "", 1)
        page.toc = self._flatten_toc(md.toc_tokens)
        page.ids = set(re.findall(r'\sid="([^"]+)"', body))
        page.has_mermaid = 'class="language-mermaid"' in body
        body = self._callouts(body)
        body = self._rewrite_links(body, page)
        body = re.sub(r"<table>", '<div class="table-wrap"><table>', body)
        body = body.replace("</table>", "</table></div>")
        body = re.sub(
            r'<pre><code class="language-mermaid">(.*?)</code></pre>',
            lambda mm: f'<pre class="mermaid">{mm.group(1)}</pre>', body, flags=re.S)
        page.body = body
        plain = re.sub(r"<pre.*?</pre>", " ", body, flags=re.S)
        plain = html.unescape(re.sub(r"<[^>]+>", " ", plain))
        page.text = re.sub(r"\s+", " ", plain).strip()
        page.description = self._description(body, page)

    @staticmethod
    def _flatten_toc(tokens: list[dict]) -> list[dict]:
        flat = []
        for t in tokens:
            flat.append({"id": t["id"], "name": html.unescape(re.sub(r"<[^>]+>", "", t["name"])), "level": t["level"]})
            flat.extend(Builder._flatten_toc(t.get("children", [])))
        return flat

    @staticmethod
    def _callouts(body: str) -> str:
        def repl(m: re.Match) -> str:
            cls, label = ALERTS[m.group(1)]
            return f'<blockquote class="callout callout-{cls}"><p class="callout-title">{label}</p><p>'
        return re.sub(r"<blockquote>\s*<p>\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*(?:<br\s*/?>)?\s*", repl, body)

    def _description(self, body: str, page: Page) -> str:
        for m in re.finditer(r"<p>(.*?)</p>", body, re.S):
            text = html.unescape(re.sub(r"<[^>]+>", "", m.group(1))).strip()
            if len(text) >= 30 and not text.startswith("[!"):
                text = re.sub(r"\s+", " ", text)
                return text if len(text) <= 120 else text[:117].rstrip("、。 ") + "…"
        return page.blurb or f"{page.title} - {SITE_NAME}"

    def _rewrite_links(self, body: str, page: Page) -> str:
        def attr(m: re.Match) -> str:
            name, quote, value = m.group(1), m.group(2), html.unescape(m.group(3))
            return f'{name}={quote}{html.escape(self._resolve(value, page, name), quote=True)}{quote}'
        body = re.sub(r'\b(href|src)=(")([^"]*)"', attr, body)
        body = re.sub(r"<img ", '<img loading="lazy" decoding="async" ', body)
        body = re.sub(r'<a href="(https?://[^"]+)"', r'<a rel="noopener" href="\1"', body)
        return body

    def _resolve(self, value: str, page: Page, attr: str) -> str:
        if not value or re.match(r"^(https?:|mailto:|tel:|data:)", value):
            return value
        if value.startswith("#"):
            self.link_targets.append((page.url, page.url, value[1:]))
            return value
        path, _, frag = value.partition("#")
        target = (page.src.parent / unquote(path)).resolve()
        try:
            rel = target.relative_to(ROOT).as_posix()
        except ValueError:
            self.errors.append(f"{page.rel}: link escapes repository: {value}")
            return value
        if rel in self.by_rel:
            dest = self.by_rel[rel]
            if frag:
                self.link_targets.append((page.url, dest.url, frag))
            return href(dest.url) + (f"#{frag}" if frag else "")
        if rel.startswith("images/") and target.is_file() and target.suffix.lower() in {".png", ".svg", ".jpg", ".jpeg", ".webp", ".gif"}:
            self.used_images.add(rel)
            return href("/" + rel)
        if target.is_file():
            return f"{REPO_URL}/blob/main/{rel}" + (f"#{frag}" if frag else "")
        if target.is_dir():
            return f"{REPO_URL}/tree/main/{rel}"
        self.warnings.append(f"{page.rel}: link target not found in repository: {value}")
        return f"{REPO_URL}/blob/main/{rel}"

    # ---------- layout ----------
    def head(self, *, title: str, description: str, path: str, jsonld: list[dict], og_type: str = "website",
             mermaid: bool = False, noindex: bool = False) -> str:
        full_title = title if title == SITE_NAME else f"{title} | {SITE_NAME}"
        canonical = abs_url(path)
        og_img = abs_url("/" + OG_IMAGE)
        ld = "".join(
            f'<script type="application/ld+json">{json.dumps(d, ensure_ascii=False, separators=(",", ":"))}</script>'
            for d in jsonld)
        robots = '<meta name="robots" content="noindex">' if noindex else '<meta name="robots" content="index,follow,max-image-preview:large">'
        return f"""<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{html.escape(full_title)}</title>
<meta name="description" content="{html.escape(description, quote=True)}">
{robots}
<meta name="theme-color" content="#070b1a">
<link rel="canonical" href="{canonical}">
<link rel="icon" type="image/png" href="{href('/assets/icon.png')}">
<link rel="apple-touch-icon" href="{href('/assets/icon.png')}">
<meta property="og:site_name" content="{SITE_NAME}">
<meta property="og:type" content="{og_type}">
<meta property="og:locale" content="ja_JP">
<meta property="og:title" content="{html.escape(full_title, quote=True)}">
<meta property="og:description" content="{html.escape(description, quote=True)}">
<meta property="og:url" content="{canonical}">
<meta property="og:image" content="{og_img}">
<meta property="og:image:alt" content="StudyReport Evaluatorの結果確認画面">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="{html.escape(full_title, quote=True)}">
<meta name="twitter:description" content="{html.escape(description, quote=True)}">
<meta name="twitter:image" content="{og_img}">
<link rel="preload" href="{href('/assets/style.css')}" as="style">
<link rel="stylesheet" href="{href('/assets/style.css')}">
<noscript><style>.reveal{{opacity:1;transform:none}}</style></noscript>
{ld}
<script>document.documentElement.dataset.motion=(localStorage.getItem('motion')||(matchMedia('(prefers-reduced-motion: reduce)').matches?'off':'on'));</script>
<script defer src="{href('/assets/app.js')}"></script>
{self._mermaid_loader() if mermaid else ''}
</head>"""

    @staticmethod
    def _mermaid_loader() -> str:
        return ('<script type="module">import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";'
                'mermaid.initialize({startOnLoad:true,theme:"dark",securityLevel:"strict"});</script>')

    def header(self, current: str = "") -> str:
        def nav(label: str, path: str, key: str) -> str:
            cur = ' aria-current="page"' if key == current else ""
            return f'<a href="{href(path)}"{cur}>{label}</a>'
        return f"""<a class="skip" href="#main">本文へスキップ</a>
<header class="site-header" id="top">
<a class="brand" href="{href('/')}"><img src="{href('/assets/icon.png')}" alt="" width="28" height="28"><span>StudyReport Evaluator</span></a>
<nav class="nav" aria-label="メイン">
{nav('先生向け', '/teachers/', TEACHERS)}
{nav('開発者向け', '/developers/', DEVELOPERS)}
{nav('変更履歴', '/releases/', RELEASES)}
<button class="icon-btn" id="search-open" type="button" aria-label="サイト内検索を開く" aria-keyshortcuts="/"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M10 4a6 6 0 1 0 3.9 10.6l4.8 4.8 1.4-1.4-4.8-4.8A6 6 0 0 0 10 4Zm0 2a4 4 0 1 1 0 8 4 4 0 0 1 0-8Z" fill="currentColor"/></svg></button>
<button class="icon-btn" id="motion-toggle" type="button" aria-pressed="true" aria-label="アニメーションを停止"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M12 3 9.5 9.5 3 12l6.5 2.5L12 21l2.5-6.5L21 12l-6.5-2.5Z" fill="currentColor"/></svg></button>
<a class="btn btn-sm" href="{REPO_URL}/releases/latest" rel="noopener">ダウンロード</a>
</nav>
</header>
<div id="search" class="search" hidden role="dialog" aria-modal="true" aria-label="サイト内検索">
<div class="search-box"><input id="search-input" type="search" placeholder="検索（例: 配点、プライバシー、ログイン）" autocomplete="off" aria-label="検索語"><ul id="search-results" aria-live="polite"></ul><p class="muted">Escで閉じます</p></div>
</div>"""

    def footer(self) -> str:
        return f"""<footer class="site-footer">
<div class="foot-grid">
<div><strong>StudyReport Evaluator</strong><p>学生の回答をAIで評価し、結果を別のExcelに保存します。AIの点数は下書きで、最終判断は先生が行います。</p></div>
<div><h2>先生向け</h2><ul><li><a href="{href(url_for(TEACHERS, 'getting-started'))}">はじめに</a></li><li><a href="{href(url_for(TEACHERS, 'privacy'))}">データと個人情報</a></li><li><a href="{href(url_for(TEACHERS, 'troubleshooting'))}">トラブルシューティング</a></li></ul></div>
<div><h2>開発者向け</h2><ul><li><a href="{href(url_for(DEVELOPERS, 'architecture'))}">アーキテクチャ</a></li><li><a href="{href(url_for(DEVELOPERS, 'technical-guide'))}">技術ガイド</a></li><li><a href="{href(url_for(DEVELOPERS, 'release-automation'))}">Release自動化</a></li></ul></div>
<div><h2>リンク</h2><ul><li><a href="{REPO_URL}" rel="noopener">GitHub</a></li><li><a href="{REPO_URL}/releases" rel="noopener">Releases</a></li><li><a href="{REPO_URL}/blob/main/LICENSE" rel="noopener">MIT License</a></li><li><a href="{href('/sitemap.xml')}">サイトマップ</a></li></ul></div>
</div>
<p class="muted small">このサイトは<a href="{REPO_URL}" rel="noopener">リポジトリ</a>のドキュメントから自動生成されています。</p>
</footer>"""

    def breadcrumbs(self, items: list[tuple[str, str]]) -> tuple[str, dict]:
        lis = []
        for i, (label, path) in enumerate(items):
            if i == len(items) - 1:
                lis.append(f'<li aria-current="page">{html.escape(label)}</li>')
            else:
                lis.append(f'<li><a href="{href(path)}">{html.escape(label)}</a></li>')
        ld = {
            "@context": "https://schema.org", "@type": "BreadcrumbList",
            "itemListElement": [
                {"@type": "ListItem", "position": i + 1, "name": label, "item": abs_url(path)}
                for i, (label, path) in enumerate(items)],
        }
        return f'<nav class="crumbs" aria-label="パンくず"><ol>{"".join(lis)}</ol></nav>', ld

    def write(self, path: str, content: str, lastmod: str | None = None, sitemap: bool = True) -> None:
        dest = self.out / path.strip("/") / "index.html" if path != "/" else self.out / "index.html"
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(content, encoding="utf-8")
        if sitemap:
            self.sitemap.append((path, lastmod or max(p.lastmod for p in self.pages)))

    # ---------- page types ----------
    def build_doc(self, page: Page) -> None:
        section_label = SECTION_LABEL[page.section]
        crumbs_html, crumbs_ld = self.breadcrumbs([
            ("ホーム", "/"), (section_label, SECTION_HOME[page.section]), (page.title, page.url)])
        siblings = [p for p in self.pages if p.section == page.section and not p.slug.startswith("adr-")]
        side = "".join(
            f'<li><a href="{href(p.url)}"{" aria-current=page" if p is page else ""}>{html.escape(re.sub(r"^StudyReport Evaluator\s*", "", p.title) or p.title)}</a></li>'
            for p in siblings)
        toc = "".join(
            f'<li class="l{t["level"]}"><a href="#{t["id"]}">{html.escape(t["name"])}</a></li>' for t in page.toc)
        article_ld = {
            "@context": "https://schema.org", "@type": "TechArticle", "headline": page.title,
            "description": page.description, "inLanguage": "ja", "dateModified": page.lastmod,
            "mainEntityOfPage": abs_url(page.url), "image": abs_url("/" + OG_IMAGE),
            "author": {"@type": "Person", "name": "dahatake"},
            "publisher": {"@type": "Organization", "name": SITE_NAME, "url": SITE_URL},
        }
        date = page.lastmod[:10]
        content = f"""{self.head(title=page.title, description=page.description, path=page.url,
                                 jsonld=[article_ld, crumbs_ld], og_type="article", mermaid=page.has_mermaid)}
<body class="doc">
{self.header(page.section)}
<div class="doc-layout">
<aside class="doc-side" aria-label="{section_label}の文書"><p class="side-title">{section_label}</p><ul>{side}</ul></aside>
<main id="main" class="doc-main">
{crumbs_html}
<article class="prose">
<h1>{html.escape(page.title)}</h1>
<p class="meta">最終更新: <time datetime="{date}">{date}</time> ・ <a href="{REPO_URL}/blob/main/{page.rel}" rel="noopener">GitHubで原文を見る</a></p>
{page.body}
</article>
</main>
<aside class="doc-toc" aria-label="このページの目次"><p class="side-title">このページ</p><ul>{toc}</ul></aside>
</div>
{self.footer()}
</body>
</html>"""
        self.write(page.url, content, page.lastmod)
        self.search.append({"t": page.title, "u": href(page.url), "s": SECTION_LABEL[page.section],
                            "x": page.text[:1500]})

    def card_list(self, section: str, skip_adr: bool = False) -> str:
        items = []
        for p in self.pages:
            if p.section != section or (skip_adr and p.slug.startswith("adr-")):
                continue
            items.append(
                f'<a class="card reveal" href="{href(p.url)}"><h3>{html.escape(p.title)}</h3>'
                f'<p>{html.escape(p.blurb or p.description)}</p><span class="more">読む →</span></a>')
        return "".join(items)

    def build_hub(self, section: str, title: str, lead: str, extra: str = "") -> None:
        path = SECTION_HOME[section]
        crumbs_html, crumbs_ld = self.breadcrumbs([("ホーム", "/"), (title, path)])
        collection = {
            "@context": "https://schema.org", "@type": "CollectionPage", "name": f"{title} | {SITE_NAME}",
            "description": lead, "url": abs_url(path), "inLanguage": "ja"}
        content = f"""{self.head(title=title, description=lead, path=path, jsonld=[collection, crumbs_ld])}
<body class="hub">
{self.header(section)}
<main id="main">
<section class="hub-hero"><div class="aurora" aria-hidden="true"></div>{crumbs_html}<h1>{html.escape(title)}</h1><p class="lead">{html.escape(lead)}</p></section>
<section class="wrap"><div class="cards">{self.card_list(section, skip_adr=section == DEVELOPERS)}</div>{extra}</section>
</main>
{self.footer()}
</body>
</html>"""
        self.write(path, content)

    def build_releases(self) -> None:
        page = self.by_rel["CHANGELOG.md"]
        # Releases hub is the changelog page itself; redirect hub path to it.
        path = SECTION_HOME[RELEASES]
        dest = self.out / path.strip("/") / "index.html"
        dest.parent.mkdir(parents=True, exist_ok=True)
        target = abs_url(page.url)
        dest.write_text(
            f'<!doctype html><html lang="ja"><meta charset="utf-8"><title>変更履歴 | {SITE_NAME}</title>'
            f'<meta name="robots" content="noindex"><link rel="canonical" href="{target}">'
            f'<meta http-equiv="refresh" content="0;url={href(page.url)}"><a href="{href(page.url)}">変更履歴へ</a></html>',
            encoding="utf-8")

    def build_adr_index(self) -> str:
        adrs = [p for p in self.pages if p.slug.startswith("adr-")]
        rows = "".join(
            f'<li><a href="{href(p.url)}">{html.escape(p.title)}</a></li>' for p in adrs)
        return f'<h2 class="sub reveal">設計判断の記録（ADR）</h2><ul class="adr-list reveal">{rows}</ul>'

    def build_404(self) -> None:
        content = f"""{self.head(title="ページが見つかりません", description="お探しのページは見つかりませんでした。", path="/404.html", jsonld=[], noindex=True)}
<body class="hub">
{self.header()}
<main id="main"><section class="hub-hero"><div class="aurora" aria-hidden="true"></div><h1>ページが見つかりません</h1>
<p class="lead">URLが変わった可能性があります。<a href="{href('/teachers/')}">先生向け</a>、<a href="{href('/developers/')}">開発者向け</a>、またはサイト内検索からお探しください。</p></section></main>
{self.footer()}
</body></html>"""
        (self.out / "404.html").write_text(content, encoding="utf-8")

    def build_home(self) -> None:
        tpl = (SITE_SRC / "home.html").read_text(encoding="utf-8")
        faq = [
            ("元のExcelは書き換えられますか？", "いいえ。元のExcelは変更せず、結果は別のExcelに保存します。"),
            ("AIの点数で成績が自動的に決まりますか？", "決まりません。AIの点数・理由は先生が確認するための補助です。成績や不正行為を自動で確定するものではなく、最終判断は先生が行います。"),
            ("どの環境で使えますか？", "Windows 11 x64と、標準の.xlsx形式が対象です。暗号化Excel、マクロ付きExcel、macOS、Linux、Windows Arm64は対象外です。"),
            ("使うために何が必要ですか？", "アプリの起動にExcelやOfficeは必要ありません。AIの利用には、本人のGitHub Copilot利用権限、インターネット接続、利用可能なモデル、学校の許可が必要です。"),
            ("起動時にWindowsの警告が出ます。", "公開EXE・ZIPは電子署名がなく、Windowsや学校の端末管理により起動を拒否される場合があります。保護機能を無効にせず、管理者に相談してください。"),
        ]
        faq_html = "".join(
            f'<details class="reveal"><summary>{html.escape(q)}</summary><p>{html.escape(a)}</p></details>' for q, a in faq)
        description = ("Excelにまとめた学生の回答をAIで評価し、元のExcelを変えずに結果を別のExcelへ保存するWindowsアプリ。"
                       "先生の確認を前提にした採点支援ツールの利用ガイドと開発者ドキュメント。")
        ld = [
            {"@context": "https://schema.org", "@type": "WebSite", "name": SITE_NAME, "url": SITE_URL + "/",
             "inLanguage": "ja", "description": description},
            {"@context": "https://schema.org", "@type": "SoftwareApplication", "name": SITE_NAME,
             "applicationCategory": "EducationalApplication", "operatingSystem": "Windows 11 x64",
             "description": description, "url": SITE_URL + "/", "downloadUrl": f"{REPO_URL}/releases/latest",
             "license": f"{REPO_URL}/blob/main/LICENSE", "inLanguage": "ja",
             "offers": {"@type": "Offer", "price": "0", "priceCurrency": "JPY"},
             "image": abs_url("/" + OG_IMAGE)},
            {"@context": "https://schema.org", "@type": "FAQPage",
             "mainEntity": [{"@type": "Question", "name": q,
                             "acceptedAnswer": {"@type": "Answer", "text": a}} for q, a in faq]},
        ]
        body = (tpl.replace("{{FAQ}}", faq_html)
                   .replace("{{BASE}}", BASE)
                   .replace("{{REPO}}", REPO_URL))
        content = (self.head(title=SITE_NAME, description=description, path="/", jsonld=ld)
                   + '\n<body class="home">\n' + self.header() + "\n" + body + "\n" + self.footer() + "\n</body>\n</html>")
        self.write("/", content)

    # ---------- checks & outputs ----------
    def check_links(self) -> None:
        ids = {p.url: p.ids for p in self.pages}
        for src, dest, frag in self.link_targets:
            if frag and frag not in ids.get(dest, set()):
                self.errors.append(f"{src}: missing anchor #{frag} in {dest}")

    def write_static(self) -> None:
        (self.out / "assets").mkdir(parents=True, exist_ok=True)
        for f in (SITE_SRC / "assets").iterdir():
            shutil.copy2(f, self.out / "assets" / f.name)
        shutil.copy2(ROOT / "src/StudyReportEvaluator.App/Assets/AppIcon.png", self.out / "assets/icon.png")
        (self.out / "images").mkdir(exist_ok=True)
        for f in (ROOT / "images").iterdir():
            if f.suffix.lower() in {".png", ".svg", ".jpg", ".webp"}:
                shutil.copy2(f, self.out / "images" / f.name)
        (self.out / ".nojekyll").write_text("", encoding="utf-8")
        (self.out / "robots.txt").write_text(
            f"User-agent: *\nAllow: /\n\nSitemap: {SITE_URL}/sitemap.xml\n", encoding="utf-8")
        entries = "".join(
            f"<url><loc>{abs_url(p)}</loc><lastmod>{m}</lastmod></url>" for p, m in sorted(self.sitemap))
        (self.out / "sitemap.xml").write_text(
            '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">'
            + entries + "</urlset>\n", encoding="utf-8")
        (self.out / "search-index.json").write_text(
            json.dumps(self.search, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")

    def run(self) -> int:
        if self.out.exists():
            shutil.rmtree(self.out)
        self.out.mkdir(parents=True)
        for p in self.pages:
            self.render_markdown(p)
        for p in self.pages:
            self.build_doc(p)
        self.build_hub(TEACHERS, "先生向けガイド",
                       "導入から最初の採点、結果の確認、データの扱いまで。ソースコードやコマンドの知識は不要です。")
        self.build_hub(DEVELOPERS, "開発者向けドキュメント",
                       "アーキテクチャ、Excelの契約、リリース自動化、設計判断の記録。拡張や配布を行う方へ。",
                       self.build_adr_index())
        self.build_releases()
        self.build_home()
        self.build_404()
        self.check_links()
        for rel in self.used_images:
            if not (self.out / rel).exists() and not (ROOT / rel).exists():
                self.errors.append(f"missing image: {rel}")
        self.write_static()
        for w in sorted(set(self.warnings)):
            print(f"::warning::{w}" if os.environ.get("GITHUB_ACTIONS") else f"warning: {w}", file=sys.stderr)
        for rel in self.used_images:
            if not (self.out / rel).exists():
                self.errors.append(f"image not published: {rel}")
        if self.errors:
            print("SITE BUILD FAILED:", file=sys.stderr)
            for e in sorted(set(self.errors)):
                print(f"  - {e}", file=sys.stderr)
            return 1
        print(f"Built {len(self.sitemap)} pages into {self.out}")
        return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(ROOT / "_site"))
    args = ap.parse_args()
    return Builder(Path(args.out)).run()


if __name__ == "__main__":
    raise SystemExit(main())
