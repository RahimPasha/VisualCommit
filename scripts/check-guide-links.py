#!/usr/bin/env python3
"""Checks the user guide's built site (docs/guide/book): its links, and that it loads nothing
from another host (D441, D451; docs/phase-8a-design.md 11.3.6).

Usage, with the standard library alone (run it with ``python3 -I``):

    python3 -I scripts/check-guide-links.py <site folder>
    python3 -I scripts/check-guide-links.py --self-test

Over each ``.html`` file under the folder, the addresses of every element's ``href``,
``xlink:href``, ``src``, ``srcset``, ``imagesrcset``, ``poster``, ``data``, ``background``,
``ping``, ``action``, ``formaction``, ``cite`` and ``longdesc``, of a ``meta`` refresh's content,
and of every ``url(...)``, ``@import`` and ``image-set(...)`` string in a ``style`` attribute or
element (an ``iframe``'s ``srcdoc`` is read as a page of its own):
  * every relative one resolves to a file of the site, and every ``#fragment`` to an ``id`` (or an
    anchor's ``name``) in its target; a fragment alone names the page itself;
  * nothing that loads names another host or a protocol-relative address. Every address loads
    except the ones a user goes to: an ``a``'s or ``area``'s ``href`` (an SVG ``a``'s
    ``xlink:href`` too), a ``form``'s ``action`` and a button's ``formaction``, a quotation's
    ``cite``, an image's ``longdesc``, and a ``link``'s ``href`` whose every ``rel`` is
    ``alternate``, ``author``, ``canonical``, ``help``, ``license``, ``next`` or ``prev``;
  * a ``data:`` URL passes, except where it would be a document, a script or a style sheet, which
    could load from another host itself: an ``iframe``'s, ``frame``'s, ``embed``'s or ``script``'s
    ``src``, an ``object``'s ``data``, a ``link`` to a style sheet, module or manifest, a refresh,
    an ``@import``;
  * no element has the class ``playground`` or ``mdbook-runnable``, for which mdBook's script
    fetches from play.rust-lang.org.
Over each ``.css`` file under the folder: every ``url(...)``, ``@import`` and ``image-set(...)``
string resolves to a file of the site; a ``data:`` URL passes, except in an ``@import``. CSS is
read with its escapes decoded, so that ``\\75 rl(`` is ``url(``.

A ``<base href>`` that is a path from the server's root (mdBook's 404 page has ``/VisualCommit/``,
from book.toml's site-url) stands for the site's own root.

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

# The attributes that hold addresses, on any element (D441, D451): each relative one must
# resolve, and each that loads must not name another host. srcset and imagesrcset hold image
# candidates, ping a list of addresses. xmlns and xmlns:* are names, never addresses.
LINK_ATTRIBUTES = ("href", "xlink:href", "src", "srcset", "imagesrcset", "poster", "data", "background",
                   "ping", "action", "formaction", "cite", "longdesc")

# The addresses a user goes to rather than ones the page loads, by element and attribute: an
# anchor's, a form's target, a quotation's source, an image's long description. Every other
# address loads, whatever its element (an SVG image, use or script by href, an image button).
NAVIGATION = {
    ("a", "href"), ("a", "xlink:href"), ("area", "href"),
    ("form", "action"), ("button", "formaction"), ("input", "formaction"),
    ("blockquote", "cite"), ("q", "cite"), ("del", "cite"), ("ins", "cite"),
    ("img", "longdesc"), ("iframe", "longdesc"),
}

# A link goes to its href, and loads nothing, only when it has a rel and every rel is one of
# these; any other rel, a stylesheet among them, loads it (prefetch, preconnect, an icon, an
# OpenSearch description, which a browser may fetch by itself).
NAVIGATION_RELS = frozenset({"alternate", "author", "canonical", "help", "license", "next", "prev"})

# Where a data: URL holds a document, a script or a style sheet, which could load from another
# host itself and whose content the check does not read. A data: image, font or medium passes.
DATA_DOCUMENTS = {("iframe", "src"), ("frame", "src"), ("object", "data"), ("embed", "src"), ("script", "src")}
DATA_DOCUMENT_RELS = frozenset({"stylesheet", "modulepreload", "manifest"})

# Class tokens for which mdBook's book.js fetches from play.rust-lang.org (a runnable Rust block).
PLAYGROUND_CLASSES = frozenset({"playground", "mdbook-runnable"})

SCHEME = re.compile(r"^[A-Za-z][A-Za-z0-9+.-]*:")

NO_SUCH_FILE = "no such file in the site"
OUTSIDE_THE_SITE = "outside the site"
NO_SUCH_ID = "no element with this id in {target}"
ANOTHER_HOST = "{what} loads from another host"
PROTOCOL_RELATIVE = "{what} loads through a protocol-relative address"
ROOT_PATH = "a path from the server's root, which the site does not hold"
BASE_ELSEWHERE = "a base on another host"
DATA_DOCUMENT = "{what} loads a data: document, script or style sheet, whose content the check does not read"
PLAYGROUND = "mdBook's playground script fetches from play.rust-lang.org for this element"


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


_HEX_DIGITS = "0123456789abcdefABCDEF"


def _read_escape(text, i):
    """A CSS escape whose backslash is text[i] and is not before a line break: the character it
    stands for and the index after it. Up to six hex digits, and one white space after them, name
    a code point (0, a surrogate or one past U+10FFFF is U+FFFD); any other character is itself."""
    n = len(text)
    j = i + 1
    if j >= n:
        return "�", n
    if text[j] not in _HEX_DIGITS:
        return text[j], j + 1
    end = j
    while end < n and end - j < 6 and text[end] in _HEX_DIGITS:
        end += 1
    code = int(text[j:end], 16)
    if text.startswith("\r\n", end):
        end += 2
    elif end < n and text[end] in " \t\n\r\f":
        end += 1
    if code == 0 or 0xD800 <= code <= 0xDFFF or code > 0x10FFFF:
        return "�", end
    return chr(code), end


def _is_escape(text, i):
    return text[i] == "\\" and i + 1 < len(text) and text[i + 1] not in "\n\r\f"


def _read_string(text, i):
    """A CSS string starting at text[i] (a quote): its value, escapes decoded, and the index after
    it. A backslash before a line break continues the string."""
    quote = text[i]
    i += 1
    value = []
    n = len(text)
    while i < n:
        character = text[i]
        if character == "\\":
            if i + 1 >= n:
                i += 1
            elif text.startswith("\r\n", i + 1):
                i += 3
            elif text[i + 1] in "\n\r\f":
                i += 2
            else:
                decoded, i = _read_escape(text, i)
                value.append(decoded)
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


def _is_name_character(text, i):
    character = text[i]
    return character.isalnum() or character in "-_" or ord(character) >= 0x80 or _is_escape(text, i)


def _read_name(text, i):
    """The run of name characters at text[i] (an identifier, or a number with its unit), escapes
    decoded, and the index after it."""
    name = []
    n = len(text)
    while i < n and _is_name_character(text, i):
        if text[i] == "\\":
            decoded, i = _read_escape(text, i)
            name.append(decoded)
        else:
            name.append(text[i])
            i += 1
    return "".join(name), i


def _read_url(text, i):
    """The address of a url( whose parenthesis ends before text[i], escapes decoded, and the index
    after its ')'."""
    i = _skip_space(text, i)
    if i < len(text) and text[i] in "\"'":
        url, i = _read_string(text, i)
        i = _skip_space(text, i)
        if i < len(text) and text[i] == ")":
            i += 1
        return url, i
    url = []
    n = len(text)
    while i < n and text[i] != ")":
        if _is_escape(text, i):
            decoded, i = _read_escape(text, i)
            url.append(decoded)
        else:
            url.append(text[i])
            i += 1
    return "".join(url).strip(), min(i + 1, n)


_IMAGE_SETS = ("image-set", "-webkit-image-set")


def css_references(text):
    """Every address a style sheet loads, as (form, address): 'url()', '@import' with a string,
    '@import url()', and each string inside 'image-set()' or '-webkit-image-set()'. Names are read
    as CSS reads them, escapes decoded, so that '\\75 rl(' is url( too; comments and other strings
    are skipped."""
    references = []
    # The functions open around the reader: a name for one whose strings are image sets, else None.
    functions = []
    i, n = 0, len(text)
    while i < n:
        if text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        character = text[i]
        if character in "\"'":
            value, i = _read_string(text, i)
            if any(function is not None for function in functions):
                references.append(("image-set()", value))
            continue
        if character == "@" and i + 1 < n and _is_name_character(text, i + 1):
            keyword, i = _read_name(text, i + 1)
            if keyword.lower() != "import":
                continue
            j = _skip_space(text, i)
            if j < n and text[j] in "\"'":
                url, i = _read_string(text, j)
                references.append(("@import", url))
                continue
            if j < n and _is_name_character(text, j):
                name, k = _read_name(text, j)
                if name.lower() == "url" and k < n and text[k] == "(":
                    url, i = _read_url(text, k + 1)
                    references.append(("@import url()", url))
                    continue
            i = j
            continue
        if _is_name_character(text, i):
            name, i = _read_name(text, i)
            if i < n and text[i] == "(":
                lower = name.lower()
                if lower == "url":
                    url, i = _read_url(text, i + 1)
                    references.append(("url()", url))
                    continue
                functions.append(lower if lower in _IMAGE_SETS else None)
                i += 1
            continue
        if character == "(":
            functions.append(None)
        elif character == ")" and functions:
            functions.pop()
        i += 1
    return references


def refresh_url(content):
    """The address of a meta refresh's content (``5; url=page.html``), as HTML reads it, or ''."""
    match = re.match(r"\s*[0-9.]*\s*[;,]?\s*(?:url\s*=\s*)?(.*)$", content, re.IGNORECASE | re.DOTALL)
    url = match.group(1).strip() if match else ""
    if url[:1] in ("'", '"'):
        end = url.find(url[0], 1)
        url = url[1:] if end < 0 else url[1:end]
    return url.strip()


class _Page(html.parser.HTMLParser):
    """One HTML page as the check reads it: its ids, its base, every address it names and its
    elements for mdBook's playground."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.ids = set()
        self.base = None
        # (what, address, loads, document): what is "<element attribute>", loads whether the
        # address loads, document whether a data: URL there is a document, script or style sheet.
        self.addresses = []
        # (what, class token) of each element mdBook's playground script fetches for.
        self.playgrounds = []
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
        rels = set(values.get("rel", "").lower().split()) if tag == "link" else set()
        for attribute in LINK_ATTRIBUTES:
            if attribute not in values:
                continue
            what = "<{} {}>".format(tag, attribute)
            value = values[attribute]
            if attribute in ("srcset", "imagesrcset"):
                urls = srcset_urls(value)
            elif attribute == "ping":
                urls = value.split()
            else:
                urls = [value.strip()]
            if tag == "link" and attribute == "href":
                loads = not rels or not rels <= NAVIGATION_RELS
                document = bool(rels & DATA_DOCUMENT_RELS)
            else:
                loads = (tag, attribute) not in NAVIGATION
                document = (tag, attribute) in DATA_DOCUMENTS
            for url in urls:
                self.addresses.append((what, url, loads, document))
        if tag == "meta" and values.get("http-equiv", "").strip().lower() == "refresh":
            self.addresses.append(("<meta refresh>", refresh_url(values.get("content", "")), True, True))
        if "srcdoc" in values:
            # A frame's srcdoc is a page of its own, whose relative addresses resolve as this one's.
            inner = _Page()
            inner.feed(values["srcdoc"])
            inner.close()
            prefix = "<{} srcdoc> ".format(tag)
            self.addresses.extend((prefix + what, url, loads, document) for what, url, loads, document in inner.addresses)
            self.playgrounds.extend((prefix + what, token) for what, token in inner.playgrounds)
        for token in values.get("class", "").split():
            if token in PLAYGROUND_CLASSES:
                self.playgrounds.append(("<{} class>".format(tag), token))
        if "style" in values:
            for form, url in css_references(values["style"]):
                self.addresses.append(("<{} style> {}".format(tag, form), url, True, form.startswith("@import")))
        if tag == "style":
            self._style_depth += 1
            self._style_text = []

    def handle_endtag(self, tag):
        if tag == "style" and self._style_depth:
            self._end_style()

    def _end_style(self):
        self._style_depth = 0
        for form, url in css_references("".join(self._style_text)):
            self.addresses.append(("<style> " + form, url, True, form.startswith("@import")))
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
        for what, token in parser.playgrounds:
            self._find(page, '{}="{}"'.format(what, token), PLAYGROUND)
        for what, url, loads, document in parser.addresses:
            if url == "":
                continue
            if url.startswith("//"):
                if loads:
                    self._find(page, url, PROTOCOL_RELATIVE.format(what=what))
                continue
            if SCHEME.match(url):
                if not loads:
                    continue
                if not url.lower().startswith("data:"):
                    self._find(page, url, ANOTHER_HOST.format(what=what))
                elif document:
                    self._find(page, url, DATA_DOCUMENT.format(what=what))
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
            if url == "" or url.startswith("#"):
                continue
            if url.lower().startswith("data:"):
                if form.startswith("@import"):
                    self._find(sheet, url, DATA_DOCUMENT.format(what=form))
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
    # Inline SVG: an image, a script and a use load by href or xlink:href.
    ("svg image href", "<image href>", '<svg xmlns="http://www.w3.org/2000/svg"><image href="{url}"/></svg>'),
    ("svg image xlink:href", "<image xlink:href>", '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="{url}"/></svg>'),
    ("svg script href", "<script href>", '<svg xmlns="http://www.w3.org/2000/svg"><script href="{url}"></script></svg>'),
    ("svg use href", "<use href>", '<svg xmlns="http://www.w3.org/2000/svg"><use href="{url}"/></svg>'),
    # The background attribute, an image button, a link's image set and a link's ping.
    ("table background", "<table background>", '<table background="{url}"><tr><td>x</td></tr></table>'),
    ("input image", "<input src>", '<input type="image" src="{url}" alt="x">'),
    ("link imagesrcset", "<link imagesrcset>", '<link rel="preload" as="image" href="ok.png" imagesrcset="{url} 2x">'),
    ("a ping", "<a ping>", '<a href="other.html" ping="{url}">p</a>'),
    # A link loads whatever its rel, unless every rel names a page to go to.
    ("link prefetch", "<link href>", '<link rel="prefetch" href="{url}">'),
    ("link preconnect", "<link href>", '<link rel="preconnect" href="{url}">'),
    ("link dns-prefetch", "<link href>", '<link rel="dns-prefetch" href="{url}">'),
    ("link prerender", "<link href>", '<link rel="prerender" href="{url}">'),
    ("link apple-touch-icon", "<link href>", '<link rel="apple-touch-icon" href="{url}">'),
    ("link mask-icon", "<link href>", '<link rel="mask-icon" href="{url}">'),
    ("link search", "<link href>", '<link rel="search" type="application/opensearchdescription+xml" href="{url}">'),
    ("link alternate stylesheet", "<link href>", '<link rel="alternate stylesheet" href="{url}">'),
    ("link without a rel", "<link href>", '<link href="{url}">'),
    # A refresh goes to its address by itself; a frame's srcdoc is a page of its own.
    ("meta refresh", "<meta refresh>", '<meta http-equiv="refresh" content="0; url={url}">'),
    ("iframe srcdoc", "<iframe srcdoc> <img src>", '<iframe srcdoc="<img src=&quot;{url}&quot; alt=&quot;x&quot;>"></iframe>'),
    # CSS: an image set's strings, and url( written with an escape.
    ("style element image-set()", "<style> image-set()", '<style>.a {{ background-image: image-set("{url}" 1x); }}</style>'),
    ("style attribute -webkit-image-set()", "<div style> image-set()", "<div style=\"background-image: -webkit-image-set('{url}' 1x)\"></div>"),
    ("style element escaped url()", "<style> url()", "<style>.a {{ background: \\75 rl({url}); }}</style>"),
)

# (the case, what a finding names, the style sheet).
_CSS_FORMS = (
    ("url()", "url()", "a {{ background: url({url}); }}"),
    ("url('')", "url()", "a {{ background: url('{url}'); }}"),
    ('url("")', "url()", 'a {{ background: url("{url}"); }}'),
    ("@import ''", "@import", "@import '{url}';"),
    ('@import ""', "@import", '@import "{url}";'),
    ("@import url()", "@import url()", "@import url({url});"),
    ("image-set()", "image-set()", 'a {{ background-image: image-set("{url}" 1x); }}'),
    ("-webkit-image-set()", "image-set()", "a {{ background-image: -webkit-image-set('{url}' 1x); }}"),
    ("escaped url()", "url()", "a {{ background: \\75 rl({url}); }}"),
    ("escaped URL( in capitals", "url()", "a {{ background: \\55 RL('{url}'); }}"),
    ("escaped @import", "@import", '@\\69mport "{url}";'),
)

# (the case, what a finding names, the page's body; {url} a data: URL): a data: URL whose content
# would be a document, a script or a style sheet, which could load from another host itself.
_DATA_DOCUMENTS = (
    ("iframe", "<iframe src>", '<iframe src="{url}"></iframe>'),
    ("object", "<object data>", '<object data="{url}"></object>'),
    ("embed", "<embed src>", '<embed src="{url}">'),
    ("script", "<script src>", '<script src="{url}"></script>'),
    ("link stylesheet", "<link href>", '<link rel="stylesheet" href="{url}">'),
    ("meta refresh", "<meta refresh>", '<meta http-equiv="refresh" content="0; url={url}">'),
    ("style element @import", "<style> @import", '<style>@import "{url}";</style>'),
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
    case("css url() with an escaped colon", {"index.html": _page(), "css/a.css": 'a { background: url("https\\3a //cdn.example.com/x"); }'}, ANOTHER_HOST.format(what="url()"))
    case("css image-set() of a data: image", {"index.html": _page(), "css/a.css": 'a { background-image: image-set("data:image/png;base64,AAA=" 1x); }'}, None)
    # A data: document, script or style sheet fails; a data: image or icon passes.
    for name, what, template in _DATA_DOCUMENTS:
        case("data: {}".format(name), {"index.html": _page(template.format(url="data:text/html;base64,PHA+eDwvcD4="))}, DATA_DOCUMENT.format(what=what))
    case("css @import of a data: style sheet", {"index.html": _page(), "css/a.css": '@import "data:text/css,a{}";'}, DATA_DOCUMENT.format(what="@import"))
    case("data: icon", {"index.html": _page("", '<link rel="icon" href="data:,">')}, None)
    # Addresses to go to, not to load, pass outside the site; inline SVG's namespaces are no addresses.
    case("link canonical to an outside page", {"index.html": _page("", '<link rel="canonical" href="https://example.com/page">')}, None)
    case("links license and alternate to outside pages", {"index.html": _page("", '<link rel="license" href="https://example.com/license"><link rel="alternate" type="application/rss+xml" href="https://example.com/feed">')}, None)
    case("form action and formaction to outside pages", {"index.html": _page('<form action="https://example.com/search"><button formaction="//example.com/other">go</button></form>')}, None)
    case("cite and longdesc to outside pages", {"index.html": _page('<blockquote cite="https://example.com/q">q</blockquote><img src="ok.png" longdesc="https://example.com/d" alt="x">')}, None)
    case("inline svg with its namespaces and a link", {"index.html": _page('<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><a xlink:href="https://example.com/page"><path d="M0 0"/></a></svg>')}, None)
    case("form action to no file", {"index.html": _page('<form action="missing.html"></form>')}, NO_SUCH_FILE)
    case("cite to no file", {"index.html": _page('<q cite="missing.html">q</q>')}, NO_SUCH_FILE)
    case("ping to no file", {"index.html": _page('<a href="other.html" ping="missing.html">p</a>')}, NO_SUCH_FILE)
    case("svg use of no id", {"index.html": _page('<svg><use href="#nope"/></svg>')}, NO_SUCH_ID.format(target="index.html"))
    case("meta refresh to no file", {"index.html": _page("", '<meta http-equiv="refresh" content="5; URL=\'missing.html\'">')}, NO_SUCH_FILE)
    # mdBook's playground: its script fetches from play.rust-lang.org for every such element.
    case("mdBook's playground", {"index.html": _page('<pre class="playground"><code class="language-rust">fn main() {}</code></pre>')}, PLAYGROUND)
    case("a runnable rust block", {"index.html": _page('<pre><code class="language-rust mdbook-runnable">fn main() {}</code></pre>')}, PLAYGROUND)
    case("a rust block that does not run", {"index.html": _page('<pre><code class="language-rust">fn main() {}</code></pre><div class="playgrounds">x</div>')}, None)
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
