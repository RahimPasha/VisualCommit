#!/usr/bin/env python3
"""Checks the user guide's built site (docs/guide/book): its links, and that it loads nothing
from another host (D441, D451; docs/phase-8a-design.md 11.3.6).

Usage, with the standard library alone (run it with ``python3 -I``):

    python3 -I scripts/check-guide-links.py <site folder>
    python3 -I scripts/check-guide-links.py --self-test

Over each ``.html`` file under the folder:
  * every relative ``href``, ``src`` and ``srcset`` entry (and a media element's ``poster``, an
    object's ``data``) resolves to a file of the site, and every ``#fragment`` to an ``id`` (or an
    anchor's ``name``) in its target; a fragment alone names the page itself;
  * nothing that loads names another host or a protocol-relative address: ``script``, ``img``
    (its ``src`` and each ``srcset`` entry), ``iframe``, ``source``, ``video`` (with its
    ``poster``), ``audio``, ``track``, ``object``, ``embed``, a ``link`` whose ``rel`` is
    ``stylesheet``, ``icon``, ``preload``, ``modulepreload`` or ``manifest``, and every ``url(...)``
    and ``@import`` in an inline ``style`` attribute or a ``style`` element.
Over each ``.css`` file under the folder: every ``url(...)`` and ``@import`` resolves to a file of
the site; a ``data:`` URL passes.

Links to outside pages pass. A ``<base href>`` that is a path from the server's root (mdBook's
404 page has ``/VisualCommit/``, from book.toml's site-url) stands for the site's own root.

Findings print as ``<file>: <url>: <reason>``. Exit code 0 when the site is clean, 1 with
findings, 2 for a usage error. ``--self-test`` checks one good and one bad case per rule, and one
bad case per element, ``rel`` and CSS form, in a temporary folder.
"""

import argparse
import contextlib
import html.parser
import io
import os
import re
import sys
import tempfile
import urllib.parse

# What each element loads, by attribute (D441, D451).
LOADING_ATTRIBUTES = {
    "script": ("src",),
    "img": ("src", "srcset"),
    "iframe": ("src",),
    "source": ("src", "srcset"),
    "video": ("src", "poster"),
    "audio": ("src",),
    "track": ("src",),
    "object": ("data",),
    "embed": ("src",),
}

# A link loads its href when its rel holds one of these.
LOADING_RELS = frozenset({"stylesheet", "icon", "preload", "modulepreload", "manifest"})

# The attributes whose relative addresses must resolve, on any element.
LINK_ATTRIBUTES = ("href", "src", "srcset", "poster", "data")

SCHEME = re.compile(r"^[A-Za-z][A-Za-z0-9+.-]*:")

NO_SUCH_FILE = "no such file in the site"
OUTSIDE_THE_SITE = "outside the site"
NO_SUCH_ID = "no element with this id in {target}"
ANOTHER_HOST = "{what} loads from another host"
PROTOCOL_RELATIVE = "{what} loads through a protocol-relative address"
ROOT_PATH = "a path from the server's root, which the site does not hold"
BASE_ELSEWHERE = "a base on another host"


def srcset_urls(value):
    """The URLs of a srcset, as HTML reads it: a URL ends at whitespace, and a candidate's
    descriptors run to the next comma outside parentheses, so a data: URL's comma stays in it."""
    urls = []
    i, n = 0, len(value)
    while i < n:
        while i < n and (value[i].isspace() or value[i] == ","):
            i += 1
        if i >= n:
            break
        start = i
        while i < n and not value[i].isspace():
            i += 1
        url = value[start:i]
        if url.endswith(","):
            url = url.rstrip(",")
        else:
            depth = 0
            while i < n:
                character = value[i]
                i += 1
                if character == "(":
                    depth += 1
                elif character == ")":
                    depth = max(0, depth - 1)
                elif character == "," and depth == 0:
                    break
        if url:
            urls.append(url)
    return urls


def _read_string(text, i):
    """A CSS string starting at text[i] (a quote): its value and the index after it. A backslash
    before a line break continues the string; a backslash before any other character is that
    character."""
    quote = text[i]
    i += 1
    value = []
    n = len(text)
    while i < n:
        character = text[i]
        if character == "\\" and i + 1 < n:
            following = text[i + 1]
            if following == "\r" and text.startswith("\r\n", i + 1):
                i += 3
            elif following in "\n\r\f":
                i += 2
            else:
                value.append(following)
                i += 2
            continue
        if character == quote:
            return "".join(value), i + 1
        value.append(character)
        i += 1
    return "".join(value), n


def _skip_space(text, i):
    while i < len(text) and text[i].isspace():
        i += 1
    return i


def _read_url(text, i):
    """The address of a url( whose parenthesis ends before text[i], and the index after its ')'."""
    i = _skip_space(text, i)
    if i < len(text) and text[i] in "\"'":
        url, i = _read_string(text, i)
        i = _skip_space(text, i)
        if i < len(text) and text[i] == ")":
            i += 1
        return url, i
    start = i
    while i < len(text) and text[i] != ")":
        i += 2 if text[i] == "\\" else 1
    return text[start:i].strip(), min(i + 1, len(text))


def css_references(text):
    """Every address a style sheet loads, as (form, address): 'url()', '@import' with a string,
    and '@import url()'. Comments and strings outside url( are skipped."""
    references = []
    i, n = 0, len(text)
    while i < n:
        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        character = text[i]
        if character in "\"'":
            _, i = _read_string(text, i)
            continue
        if text[i:i + 4].lower() == "url(" and (i == 0 or not (text[i - 1].isalnum() or text[i - 1] in "-_")):
            url, i = _read_url(text, i + 4)
            references.append(("url()", url))
            continue
        if text[i:i + 7].lower() == "@import":
            j = _skip_space(text, i + 7)
            if j < n and text[j] in "\"'":
                url, i = _read_string(text, j)
                references.append(("@import", url))
                continue
            if text[j:j + 4].lower() == "url(":
                url, i = _read_url(text, j + 4)
                references.append(("@import url()", url))
                continue
            i = j
            continue
        i += 1
    return references


class _Page(html.parser.HTMLParser):
    """One HTML page as the check reads it: its ids, its base and every address it names."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.ids = set()
        self.base = None
        # (what, address, loads): what is "<element attribute>", loads whether the address loads.
        self.addresses = []
        self._style_depth = 0
        self._style_text = []

    def handle_starttag(self, tag, attrs):
        self._start(tag, attrs)

    def handle_startendtag(self, tag, attrs):
        self._start(tag, attrs)
        if tag == "style":
            self._end_style()

    def _start(self, tag, attrs):
        values = {}
        for name, value in attrs:
            values.setdefault(name, "" if value is None else value)
        if "id" in values:
            self.ids.add(values["id"])
        if tag == "a" and "name" in values:
            self.ids.add(values["name"])
        if tag == "base" and "href" in values and self.base is None:
            self.base = values["href"]
            return
        loading = set(LOADING_ATTRIBUTES.get(tag, ()))
        if tag == "link":
            rels = set(values.get("rel", "").lower().split())
            if rels & LOADING_RELS:
                loading.add("href")
        for attribute in LINK_ATTRIBUTES:
            if attribute not in values:
                continue
            what = "<{} {}>".format(tag, attribute)
            urls = srcset_urls(values[attribute]) if attribute == "srcset" else [values[attribute].strip()]
            for url in urls:
                self.addresses.append((what, url, attribute in loading))
        if "style" in values:
            for form, url in css_references(values["style"]):
                self.addresses.append(("<{} style> {}".format(tag, form), url, True))
        if tag == "style":
            self._style_depth += 1
            self._style_text = []

    def handle_endtag(self, tag):
        if tag == "style" and self._style_depth:
            self._end_style()

    def _end_style(self):
        self._style_depth = 0
        for form, url in css_references("".join(self._style_text)):
            self.addresses.append(("<style> " + form, url, True))
        self._style_text = []

    def handle_data(self, data):
        if self._style_depth:
            self._style_text.append(data)


def _relative(path, root):
    return os.path.relpath(path, root).replace(os.sep, "/")


def _inside(path, root):
    path = os.path.normcase(os.path.realpath(path))
    root = os.path.normcase(os.path.realpath(root))
    return path == root or path.startswith(root.rstrip(os.sep) + os.sep)


def _target_file(path):
    """The file a resolved path names: the file itself, or a folder's index.html; else None."""
    if os.path.isfile(path):
        return path
    if os.path.isdir(path) and os.path.isfile(os.path.join(path, "index.html")):
        return os.path.join(path, "index.html")
    return None


class Checker:
    def __init__(self, root):
        self.root = os.path.abspath(root)
        self.findings = []
        self._ids = {}

    def _ids_of(self, page):
        if page not in self._ids:
            parser = _Page()
            with open(page, encoding="utf-8", errors="replace") as handle:
                parser.feed(handle.read())
            parser.close()
            self._ids[page] = parser.ids
        return self._ids[page]

    def _find(self, source, url, reason):
        self.findings.append("{}: {}: {}".format(_relative(source, self.root), url, reason))

    def _resolve(self, source, url, folder, page_ids=None):
        """Checks that a relative address resolves from folder; page_ids are the source page's,
        for a fragment alone."""
        address, _, fragment = url.partition("#")
        address = address.partition("?")[0]
        if address == "":
            if fragment and page_ids is not None and urllib.parse.unquote(fragment) not in page_ids:
                self._find(source, url, NO_SUCH_ID.format(target=_relative(source, self.root)))
            return
        path = os.path.normpath(os.path.join(folder, urllib.parse.unquote(address)))
        if not _inside(path, self.root):
            self._find(source, url, OUTSIDE_THE_SITE)
            return
        target = _target_file(path)
        if target is None:
            self._find(source, url, NO_SUCH_FILE)
            return
        if fragment and target.lower().endswith((".html", ".htm")):
            if urllib.parse.unquote(fragment) not in self._ids_of(target):
                self._find(source, url, NO_SUCH_ID.format(target=_relative(target, self.root)))

    def check_page(self, page):
        parser = _Page()
        with open(page, encoding="utf-8", errors="replace") as handle:
            parser.feed(handle.read())
        parser.close()
        self._ids[page] = parser.ids
        folder = os.path.dirname(page)
        base_path = None
        if parser.base is not None:
            base = parser.base.strip()
            if SCHEME.match(base) or base.startswith("//"):
                self._find(page, base, BASE_ELSEWHERE)
            elif base.startswith("/"):
                base_path = base if base.endswith("/") else base + "/"
                folder = self.root
            else:
                folder = os.path.normpath(os.path.join(folder, urllib.parse.unquote(base)))
        for what, url, loads in parser.addresses:
            if url == "":
                continue
            if url.startswith("//"):
                if loads:
                    self._find(page, url, PROTOCOL_RELATIVE.format(what=what))
                continue
            if SCHEME.match(url):
                if loads and not url.lower().startswith("data:"):
                    self._find(page, url, ANOTHER_HOST.format(what=what))
                continue
            if url.startswith("#"):
                self._resolve(page, url, folder, parser.ids)
                continue
            if url.startswith("/"):
                if base_path is not None and url.startswith(base_path):
                    self._resolve(page, url[len(base_path):] or "./", self.root)
                elif base_path is not None and url == base_path.rstrip("/"):
                    self._resolve(page, "./", self.root)
                else:
                    self._find(page, url, ROOT_PATH)
                continue
            self._resolve(page, url, folder, parser.ids)

    def check_style_sheet(self, sheet):
        with open(sheet, encoding="utf-8", errors="replace") as handle:
            text = handle.read()
        folder = os.path.dirname(sheet)
        for form, url in css_references(text):
            if url == "" or url.lower().startswith("data:") or url.startswith("#"):
                continue
            if url.startswith("//"):
                self._find(sheet, url, PROTOCOL_RELATIVE.format(what=form))
            elif SCHEME.match(url):
                self._find(sheet, url, ANOTHER_HOST.format(what=form))
            elif url.startswith("/"):
                self._find(sheet, url, ROOT_PATH)
            else:
                self._resolve(sheet, url, folder)

    def run(self):
        for folder, _, files in sorted(os.walk(self.root)):
            for name in sorted(files):
                path = os.path.join(folder, name)
                lower = name.lower()
                if lower.endswith((".html", ".htm")):
                    self.check_page(path)
                elif lower.endswith(".css"):
                    self.check_style_sheet(path)
        return self.findings


def check(root):
    """The findings over the site at root, each '<file>: <url>: <reason>'."""
    return Checker(root).run()


def main(argv):
    parser = argparse.ArgumentParser(
        prog="check-guide-links.py",
        description="Checks the user guide's built site: its links resolve, and nothing it loads comes from another host.")
    parser.add_argument("site", nargs="?", help="the built site's folder, such as docs/guide/book")
    parser.add_argument("--self-test", action="store_true", help="check the checker on made-up sites in a temporary folder")
    try:
        arguments = parser.parse_args(argv)
    except SystemExit as stop:
        return 2 if stop.code else 0
    if arguments.self_test:
        if arguments.site is not None:
            print("check-guide-links.py: give a site's folder or --self-test, not both.", file=sys.stderr)
            return 2
        return self_test()
    if arguments.site is None:
        parser.print_usage(sys.stderr)
        print("check-guide-links.py: give the built site's folder.", file=sys.stderr)
        return 2
    if not os.path.isdir(arguments.site):
        print("check-guide-links.py: {} is not a folder.".format(arguments.site), file=sys.stderr)
        return 2
    findings = check(arguments.site)
    for finding in findings:
        print(finding)
    if findings:
        print("{} finding(s) in {}.".format(len(findings), arguments.site), file=sys.stderr)
        return 1
    print("No findings in {}.".format(arguments.site))
    return 0


# The self-test. Each case is a made-up site: its files, and the one reason its check must find,
# or None for a site that must be clean.

_PAGE = "<!DOCTYPE html><html><head>{head}</head><body id=\"top\">{body}</body></html>"


def _page(body="", head=""):
    return _PAGE.format(head=head, body=body)


# The files every case's site holds besides its own.
_COMMON = {
    "other.html": _page('<h2 id="x">X</h2><a name="named">N</a>'),
    "sub/index.html": _page("<p>Sub</p>"),
    "ok.png": "png",
    "ok.js": "js",
    "ok.css": "body { color: black; }",
    "ok.vtt": "WEBVTT",
}

_HOSTS = (("https://cdn.example.com/x", ANOTHER_HOST), ("//cdn.example.com/x", PROTOCOL_RELATIVE))

# (the case, what a finding names, the page's body; {url} the address, {local} a file of the site).
_LOADING_ELEMENTS = (
    ("script", "<script src>", '<script src="{url}"></script>'),
    ("img", "<img src>", '<img src="{url}" alt="x">'),
    ("img srcset", "<img srcset>", '<img src="ok.png" srcset="{url} 2x" alt="x">'),
    ("iframe", "<iframe src>", '<iframe src="{url}"></iframe>'),
    ("source", "<source src>", '<video><source src="{url}"></video>'),
    ("source srcset", "<source srcset>", '<picture><source srcset="{url} 2x"><img src="ok.png" alt="x"></picture>'),
    ("video", "<video src>", '<video src="{url}"></video>'),
    ("video poster", "<video poster>", '<video src="ok.png" poster="{url}"></video>'),
    ("audio", "<audio src>", '<audio src="{url}"></audio>'),
    ("track", "<track src>", '<video src="ok.png"><track src="{url}"></video>'),
    ("object", "<object data>", '<object data="{url}"></object>'),
    ("embed", "<embed src>", '<embed src="{url}">'),
    ("link stylesheet", "<link href>", '<link rel="stylesheet" href="{url}">'),
    ("link icon", "<link href>", '<link rel="shortcut icon" href="{url}">'),
    ("link preload", "<link href>", '<link rel="preload" as="font" href="{url}">'),
    ("link modulepreload", "<link href>", '<link rel="modulepreload" href="{url}">'),
    ("link manifest", "<link href>", '<link rel="manifest" href="{url}">'),
    ("style attribute url()", "<div style> url()", '<div style="background: url({url})"></div>'),
    ("style attribute url('')", "<div style> url()", "<div style=\"background: url('{url}')\"></div>"),
    ("style element url()", "<style> url()", "<style>.a {{ background: url(\"{url}\"); }}</style>"),
    ("style element @import", "<style> @import", '<style>@import "{url}";</style>'),
    ("style element @import url()", "<style> @import url()", "<style>@import url({url});</style>"),
)

# (the case, what a finding names, the style sheet).
_CSS_FORMS = (
    ("url()", "url()", "a {{ background: url({url}); }}"),
    ("url('')", "url()", "a {{ background: url('{url}'); }}"),
    ('url("")', "url()", 'a {{ background: url("{url}"); }}'),
    ("@import ''", "@import", "@import '{url}';"),
    ('@import ""', "@import", '@import "{url}";'),
    ("@import url()", "@import url()", "@import url({url});"),
)


def _cases():
    cases = []

    def case(name, files, reason):
        cases.append((name, files, reason))

    # A relative href resolves to a file of the site.
    case("href to a file", {"index.html": _page('<a href="other.html">o</a>')}, None)
    case("href to a folder's index", {"index.html": _page('<a href="sub/">s</a>')}, None)
    case("href to no file", {"index.html": _page('<a href="missing.html">m</a>')}, NO_SUCH_FILE)
    case("href outside the site", {"index.html": _page('<a href="../outside.html">o</a>')}, OUTSIDE_THE_SITE)
    # A relative src resolves.
    case("src to a file", {"index.html": _page('<script src="ok.js"></script>')}, None)
    case("src to no file", {"index.html": _page('<script src="missing.js"></script>')}, NO_SUCH_FILE)
    # Each srcset entry resolves, a data: URL's comma included.
    case("srcset entries", {"index.html": _page('<img src="ok.png" srcset="ok.png 1x, data:image/png;base64,AAA= 2x" alt="x">')}, None)
    case("srcset entry without a descriptor", {"index.html": _page('<img src="ok.png" srcset="ok.png, ok.png 2x" alt="x">')}, None)
    case("srcset entry to no file", {"index.html": _page('<img src="ok.png" srcset="ok.png 1x, missing.png 2x" alt="x">')}, NO_SUCH_FILE)
    # A fragment names an id, or an anchor's name, in its target.
    case("fragment in another page", {"index.html": _page('<a href="other.html#x">x</a><a href="other.html#named">n</a>')}, None)
    case("fragment missing in another page", {"index.html": _page('<a href="other.html#nope">x</a>')}, NO_SUCH_ID.format(target="other.html"))
    case("fragment in the page", {"index.html": _page('<a href="#top">t</a>')}, None)
    case("fragment missing in the page", {"index.html": _page('<a href="#nope">t</a>')}, NO_SUCH_ID.format(target="index.html"))
    # Links to outside pages pass; so do data: images.
    case("outside page", {"index.html": _page('<a href="https://example.com/page">e</a><a href="//example.com/page">e</a><a href="mailto:demo@example.com">m</a>')}, None)
    case("data: image", {"index.html": _page('<img src="data:image/png;base64,AAA=" alt="x">')}, None)
    # A base that is a path from the server's root stands for the site's root.
    case("base path", {"index.html": _page(), "404.html": _page('<a href="/Site/">home</a><a href="other.html#x">x</a><img src="ok.png" alt="x">', '<base href="/Site/">')}, None)
    case("base path to no file", {"404.html": _page('<a href="/Site/missing.html">m</a>', '<base href="/Site/">')}, NO_SUCH_FILE)
    case("root path without a base", {"index.html": _page('<a href="/elsewhere/page.html">e</a>')}, ROOT_PATH)
    case("base on another host", {"index.html": _page("", '<base href="https://example.com/">')}, BASE_ELSEWHERE)
    # Nothing that loads names another host or a protocol-relative address: each element, rel and
    # style form, once with a host and once without a scheme. Each also passes with a file of the site.
    for name, what, template in _LOADING_ELEMENTS:
        local = "ok.css" if "stylesheet" in name or "@import" in name else "ok.png"
        case("{} of the site".format(name), {"index.html": _page(template.format(url=local))}, None)
        for url, reason in _HOSTS:
            case("{} at {}".format(name, url), {"index.html": _page(template.format(url=url))}, reason.format(what=what))
    # A style sheet's url() and @import resolve to a file of the site; a data: URL passes.
    for name, what, template in _CSS_FORMS:
        case("css {} to a file".format(name), {"index.html": _page(), "css/a.css": template.format(url="../ok.css")}, None)
        case("css {} to no file".format(name), {"index.html": _page(), "css/a.css": template.format(url="missing.css")}, NO_SUCH_FILE)
        for url, reason in _HOSTS:
            case("css {} at {}".format(name, url), {"index.html": _page(), "css/a.css": template.format(url=url)}, reason.format(what=what))
    case("css path from the server's root", {"index.html": _page(), "css/a.css": "a { background: url(/ok.png); }"}, ROOT_PATH)
    case("css data: URL", {"index.html": _page(), "css/a.css": "a { content: url('data:image/svg+xml,<svg xmlns=\"http://www.w3.org/2000/svg\">\\\n</svg>'); }"}, None)
    case("css comment", {"index.html": _page(), "css/a.css": "/* url(https://example.com/x.css) */ a { color: red; }"}, None)
    return cases


def _write_site(folder, files):
    for relative, text in list(_COMMON.items()) + list(files.items()):
        path = os.path.join(folder, *relative.split("/"))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(text)


def self_test():
    failures = []
    cases = _cases()
    with tempfile.TemporaryDirectory(prefix="check-guide-links-") as temporary:
        for number, (name, files, reason) in enumerate(cases):
            site = os.path.join(temporary, "case-{:03d}".format(number), "site")
            _write_site(site, files)
            findings = check(site)
            if reason is None:
                if findings:
                    failures.append("{}: expected no finding, got {}".format(name, findings))
            elif len(findings) != 1 or not findings[0].endswith(": " + reason):
                failures.append("{}: expected one finding ending \"{}\", got {}".format(name, reason, findings))

        # The exit codes: 0 clean, 1 with findings, 2 for a usage error.
        clean = os.path.join(temporary, "case-{:03d}".format(len(cases)), "site")
        _write_site(clean, {"index.html": _page('<a href="other.html#x">x</a>')})
        broken = os.path.join(temporary, "case-{:03d}".format(len(cases) + 1), "site")
        _write_site(broken, {"index.html": _page('<a href="missing.html">m</a>')})
        for name, argv, expected in (
            ("a clean site", [clean], 0),
            ("a site with a finding", [broken], 1),
            ("no folder", [], 2),
            ("a folder that does not exist", [os.path.join(temporary, "none")], 2),
            ("an unknown option", ["--nope", clean], 2),
        ):
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                code = main(argv)
            if code != expected:
                failures.append("exit code for {}: expected {}, got {}".format(name, expected, code))

    total = len(cases) + 5
    if failures:
        for failure in failures:
            print("self-test failed: " + failure)
        print("{} of {} self-test cases failed.".format(len(failures), total), file=sys.stderr)
        return 1
    print("Self-test: {} cases passed.".format(total))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
