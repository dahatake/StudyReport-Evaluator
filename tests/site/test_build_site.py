import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("build_site", ROOT / "scripts" / "build_site.py")
build_site = importlib.util.module_from_spec(spec)
sys.modules["build_site"] = build_site
spec.loader.exec_module(build_site)


def test_slugify_matches_github_style_for_japanese_and_version_headings():
    assert build_site.slugify("GitHubにログイン（v0.8.6）") == "githubにログインv086"
    assert build_site.slugify("3. 実行") == "3-実行"


def test_build_produces_seo_assets_and_valid_links(tmp_path):
    out = tmp_path / "site"
    assert build_site.Builder(out).run() == 0

    home = (out / "index.html").read_text(encoding="utf-8")
    assert '<html lang="ja">' in home
    assert 'rel="canonical"' in home and 'property="og:title"' in home
    assert "FAQPage" in home and "SoftwareApplication" in home
    assert home.count("<h1") == 1

    sitemap = (out / "sitemap.xml").read_text(encoding="utf-8")
    assert "/teachers/getting-started/" in sitemap and "/developers/architecture/" in sitemap
    assert "Sitemap:" in (out / "robots.txt").read_text(encoding="utf-8")
    assert (out / "404.html").exists() and (out / ".nojekyll").exists()

    doc = (out / "teachers/getting-started/index.html").read_text(encoding="utf-8")
    assert "BreadcrumbList" in doc and "TechArticle" in doc
    assert doc.count("<h1") == 1
    assert "(../images" not in doc

    index = json.loads((out / "search-index.json").read_text(encoding="utf-8"))
    assert any(e["u"].endswith("/teachers/privacy/") for e in index)


def test_internal_links_resolve_to_generated_pages(tmp_path):
    out = tmp_path / "site"
    assert build_site.Builder(out).run() == 0
    base = build_site.BASE
    missing = []
    for html_file in out.rglob("*.html"):
        text = html_file.read_text(encoding="utf-8")
        for link in re.findall(r'(?:href|src)="(' + re.escape(base) + r'/[^"#]*)', text):
            rel = link[len(base):].lstrip("/")
            target = out / rel
            if not (target.is_file() or (target / "index.html").is_file()):
                missing.append((html_file.relative_to(out).as_posix(), link))
    assert not missing
