# Third-party notices

VisualCommit is released under the MIT License: its own license is the file `LICENSE`. It is built
on the components below, and its build output ships them, each under its own license. This list
covers what the build output ships on Windows, macOS and Linux; the installers add the .NET
runtime's own notices. The notice files that components ship themselves are kept word for word in
the `notices/` folder. VisualCommit carries this file, `LICENSE` and the `notices/` files inside its
program, and shows them in Settings, About.

Each component below names its NuGet packages and their versions (`Packages:`), the files it ships
on any of the three systems (`Files:`), where its source is (`Source:`), its copyright lines and its
license (`License:`). The copyright lines are quoted as their sources write them. A license text
that names nobody follows the list once, under "License texts"; a BSD-3-Clause text, whose third
clause names a holder, once for each holder.

## Components

### Avalonia

The cross-platform user interface framework the app is written with. `libAvaloniaNative.dylib` ships
on macOS only.

Packages: Avalonia 12.1.3, Avalonia.Desktop 12.1.3, Avalonia.Fonts.Inter 12.1.3, Avalonia.FreeDesktop 12.1.3, Avalonia.FreeDesktop.AtSpi 12.1.3, Avalonia.HarfBuzz 12.1.3, Avalonia.Native 12.1.3, Avalonia.Remote.Protocol 12.1.3, Avalonia.Skia 12.1.3, Avalonia.Themes.Fluent 12.1.3, Avalonia.Win32 12.1.3, Avalonia.X11 12.1.3

Files: Avalonia.dll, Avalonia.Base.dll, Avalonia.Controls.dll, Avalonia.DesignerSupport.dll, Avalonia.Dialogs.dll, Avalonia.Markup.dll, Avalonia.Markup.Xaml.dll, Avalonia.Metal.dll, Avalonia.MicroCom.dll, Avalonia.OpenGL.dll, Avalonia.Vulkan.dll, Avalonia.Desktop.dll, Avalonia.Fonts.Inter.dll, Avalonia.FreeDesktop.dll, Avalonia.FreeDesktop.AtSpi.dll, Avalonia.HarfBuzz.dll, Avalonia.Native.dll, libAvaloniaNative.dylib, Avalonia.Remote.Protocol.dll, Avalonia.Skia.dll, Avalonia.Themes.Fluent.dll, Avalonia.Win32.dll, Avalonia.Win32.Automation.dll, Avalonia.X11.dll

Source: https://github.com/AvaloniaUI/Avalonia (commit 8eeda4f6f546165b3f72e63c9f42247abb306905)

```
Copyright (c) AvaloniaUI OÜ
All Rights Reserved
Copyright 2013-2026 © The AvaloniaUI Project
```

License: MIT, notices/Avalonia-NOTICE.md. Avalonia is under the MIT License. It incorporates works
that its file `NOTICE.md` names with their own licenses (WPF, Silverlight Toolkit under the
Microsoft Public License, wayland-protocols, Metsys.Bson, RichTextKit, Mono, Collections.Pooled,
EllipticalArc.java, WinUI, Chromium, Flutter and Reactive Extensions); that file, at the same
commit, is kept whole as `notices/Avalonia-NOTICE.md`.

### ANGLE, as Avalonia builds it

The OpenGL ES library through which Avalonia draws with Direct3D 11 on Windows: Avalonia's fork of
ANGLE, built for Direct3D 11 alone. It ships on Windows only. Compiled into it are parts of
abseil-cpp, zlib with Chromium's compression utilities, xxHash and Chromium's copy of libc++, each
named below.

Packages: Avalonia.Angle.Windows.Natives 2.1.27548.20260419

Files: av_libglesv2.dll

Source: https://github.com/AvaloniaUI/angle (commit 1c89805903c1482166356d3b950d474973180e61), a
fork of https://chromium.googlesource.com/angle/angle

```
Copyright 2018 The ANGLE Project Authors.
All rights reserved.
```

Compiled into it, as that commit's `DEPS` and build files take them:

- abseil-cpp (https://github.com/abseil/abseil-cpp, Chromium's copy at commit
  677093525ed5a2f357482681a222274866088a2c): its files carry lines of the form "Copyright 2017 The
  Abseil Authors."; Apache-2.0.
- zlib (https://zlib.net, Chromium's copy at commit b80f1d1e5256ac25f6aea3f31f13d458981cb1f9): the
  lines below, as that copy's `LICENSE` writes them; zlib.

```
(C) 1995-2026 Jean-loup Gailly and Mark Adler

Jean-loup Gailly        Mark Adler
jloup@gzip.org          madler@alumni.caltech.edu
```

- Chromium's compression utilities, in the same copy of zlib: "Copyright 2019 The Chromium Authors";
  BSD-3-Clause (Google LLC).
- xxHash (https://github.com/Cyan4973/xxHash, in ANGLE's `src/common/third_party/xxhash`): the lines
  below, as the `LICENSE` there writes them; BSD-2-Clause.

```
Copyright (c) 2012-2021 Yann Collet
All rights reserved.
```

- libc++ (https://libcxx.llvm.org, Chromium's copy): Apache-2.0 with the LLVM exception, under which
  the compiled library needs no notice; it is named here so that the list is whole.

License: BSD-3-Clause (ANGLE), Apache-2.0, zlib, BSD-3-Clause (Google LLC), BSD-2-Clause. ANGLE is
under the BSD-3-Clause text whose third clause names TransGaming Inc., Google Inc. and 3DLabs Inc.
Ltd.; the works compiled into it are under the licenses given with each.

### SkiaSharp and HarfBuzzSharp

The .NET bindings of the Skia graphics library and of the HarfBuzz text shaper, with their native
libraries: the `.dll` files ship on Windows, the `.dylib` files on macOS and the `.so` files on
Linux.

Packages: SkiaSharp 3.119.4, SkiaSharp.NativeAssets.Win32 3.119.4, SkiaSharp.NativeAssets.macOS 3.119.4, SkiaSharp.NativeAssets.Linux 3.119.4, HarfBuzzSharp 8.3.1.3, HarfBuzzSharp.NativeAssets.Win32 8.3.1.3, HarfBuzzSharp.NativeAssets.macOS 8.3.1.3, HarfBuzzSharp.NativeAssets.Linux 8.3.1.3

Files: SkiaSharp.dll, libSkiaSharp.dll, libSkiaSharp.dylib, libSkiaSharp.so, HarfBuzzSharp.dll, libHarfBuzzSharp.dll, libHarfBuzzSharp.dylib, libHarfBuzzSharp.so

Source: https://github.com/mono/SkiaSharp (SkiaSharp 3.119.4 at commit
f568ac94dd768ef9a2f593537cfde2dd0d348ef5; HarfBuzzSharp 8.3.1.3 at commit
2888c737ad016d584c74525e2d35db5097ea8576)

```
Copyright (c) 2015-2016 Xamarin, Inc.
Copyright (c) 2017-2018 Microsoft Corporation.
© Microsoft Corporation. All rights reserved.
```

License: MIT, notices/SkiaSharp-THIRD-PARTY-NOTICES.txt. SkiaSharp and HarfBuzzSharp are under the
MIT License. The native libraries incorporate the works their packages' file
`THIRD-PARTY-NOTICES.txt` names (ANGLE, HarfBuzz, Skia, etc1, gif, libpng, the DNG SDK, expat,
FreeType, ICU, imgui, jsoncpp, libjpeg-turbo, libwebp, libmicrohttpd, piex, SDL, sfntly, SPIR-V
Headers, SPIR-V Tools and zlib), the same file in all eight packages; it is kept whole as
`notices/SkiaSharp-THIRD-PARTY-NOTICES.txt`. Two more works found in the native libraries follow.

### Wuffs, in SkiaSharp's native libraries

Skia decodes images with Wuffs; its code is in `libSkiaSharp` on all three systems, and the file
above does not name it.

Files: libSkiaSharp.dll, libSkiaSharp.dylib, libSkiaSharp.so

Source: https://github.com/google/wuffs

```
Copyright 2017 The Wuffs Authors.
```

License: MIT, Apache-2.0. Wuffs "is distributed under the terms of both the MIT license and the
Apache License (Version 2.0)", as its README says.

### SPIRV-Cross, in SkiaSharp's native library on Windows

Skia's Direct3D backend translates shaders with SPIRV-Cross; its code is in `libSkiaSharp.dll`, and
the file above does not name it.

Files: libSkiaSharp.dll

Source: https://github.com/KhronosGroup/SPIRV-Cross

```
Copyright 2015-2021 Arm Limited
```

License: Apache-2.0, MIT. Its files say `SPDX-License-Identifier: Apache-2.0 OR MIT`: the material
may be taken under either license.

### MicroCom.Runtime

The COM interop layer that Avalonia's Windows and macOS backends use.

Packages: MicroCom.Runtime 0.11.6

Files: MicroCom.Runtime.dll

Source: https://github.com/kekekeks/MicroCom (commit 76785efcafd91b5902fd19dd11145f6dd655b7b4)

```
Copyright (c) 2021 Nikita Tsukanov
```

License: MIT.

### Tmds.DBus.Protocol

The D-Bus library that Avalonia's Linux backends use.

Packages: Tmds.DBus.Protocol 0.94.1

Files: Tmds.DBus.Protocol.dll

Source: https://github.com/tmds/Tmds.DBus (commit b4a7fed0b878f74cb54f7cca84d2889af4e596ba)

```
Copyright 2006 Alp Toker <alp@atoker.com>
Copyright 2010 Other Contributors
Copyright 2016 Tom Deseyn <tom.deseyn@gmail.com>
```

License: MIT.

### .NET Community Toolkit: CommunityToolkit.Mvvm

The MVVM library of the app's view models.

Packages: CommunityToolkit.Mvvm 8.4.2

Files: CommunityToolkit.Mvvm.dll

Source: https://github.com/CommunityToolkit/dotnet (commit 35dfe7b12da0e64384bfaec7ebc0883196a89f9f)

```
Copyright © .NET Foundation and Contributors
All rights reserved.
```

License: MIT, notices/CommunityToolkit.Mvvm-ThirdPartyNotices.txt. The toolkit is under the MIT
License; the works it incorporates are named in the package's `ThirdPartyNotices.txt`, kept whole as
`notices/CommunityToolkit.Mvvm-ThirdPartyNotices.txt`.

### AvaloniaEdit

The text editor control of the diff, the blame and the conflict resolver, a port of AvalonEdit.
`AvaloniaEdit.resources.dll` is its Chinese (Simplified) text, in the folder `zh-Hans`.

Packages: Avalonia.AvaloniaEdit 12.0.0

Files: AvaloniaEdit.dll, AvaloniaEdit.resources.dll

Source: https://github.com/AvaloniaUI/AvaloniaEdit (commit 86fdebec4cff7affb0e14c7885df71d28edce777)

```
Copyright (c) 2017 Eli Arbel
Copyright 2017-2026 © The AvaloniaUI Project
Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
```

License: MIT. The last line is AvalonEdit's, which AvaloniaEdit's source files carry.

### TextMateSharp

The TextMate grammar engine that colors code in the diff and the blame.

Packages: TextMateSharp 2.0.4

Files: TextMateSharp.dll

Source: https://github.com/danipen/TextMateSharp (commit 622d1b240a5be474939857c0b2780249022f14a3)

```
Copyright (c) 2021 Daniel Peñalba
```

License: MIT. TextMateSharp is a port of microsoft/vscode-textmate based on the Java port
eclipse/tm4e.

### Onigwrap and Oniguruma

The regular expression library under TextMateSharp: Onigwrap wraps Oniguruma. Its native library
ships as `libonigwrap.dll` on Windows, `libonigwrap.dylib` on macOS and `libonigwrap.so` on Linux.

Packages: Onigwrap 1.0.11

Files: Onigwrap.dll, libonigwrap.dll, libonigwrap.dylib, libonigwrap.so

Source: https://github.com/aikawayataro/Onigwrap (commit dd8a1cbbe3190c18033b96f2922b95c1761ae793);
Oniguruma, https://github.com/kkos/oniguruma

```
Copyright (c) 2024 Aikawa Yataro
Copyright (c) 2002-2021  K.Kosako  <kkosako0@gmail.com>
Copyright (c) 2015 Fluent Solutions
```

License: MIT, BSD-2-Clause, notices/Onigwrap-THIRD-PARTY-NOTICES.txt. Onigwrap is under the MIT
License; Oniguruma, the second line, under BSD-2-Clause; fluentCODE/onigwrap, the third, under the
MIT License. The package's `THIRD-PARTY-NOTICES.TXT` names them, with TextMateSharp and SkiaSharp,
and is kept whole as `notices/Onigwrap-THIRD-PARTY-NOTICES.txt`.

### TextMate grammars and themes: TextMateSharp.Grammars

The grammars and color themes that TextMateSharp reads, all inside one assembly. The app loads a
grammar by a file's extension and only the Dark+ and Light+ themes, but the assembly ships them all.

Packages: TextMateSharp.Grammars 2.0.4

Files: TextMateSharp.Grammars.dll

Source: https://github.com/danipen/TextMateSharp (commit 622d1b240a5be474939857c0b2780249022f14a3)

```
Copyright (c) 2021 Daniel Peñalba
Copyright (c) 2015 - present Microsoft Corporation
```

License: MIT, TextMate bundle license, BSD-3-Clause (Google LLC), Apache-2.0. The package is under
the MIT License. Each grammar folder holds a grammar from the repository the table names, under the
license the package states for it (its own words, where they differ, in parentheses), and the
extension files of Visual Studio Code around it (`package.json`, `language-configuration.json` and
the snippets), which are Visual Studio Code's under the MIT License, "Copyright (c) 2015 - present
Microsoft Corporation", but those of asciidoc (Matt Neuburg, MIT), typst (Myriad-Dreamin,
Apache-2.0) and x86asm (RysteQ, MIT).

| Folder | Repository | License |
|---|---|---|
| asciidoc | https://github.com/mattneub/AsciiDoc-TextMate-2.tmbundle | MIT |
| bat | https://github.com/mmims/language-batchfile | MIT |
| clojure | https://github.com/atom/language-clojure | MIT |
| coffeescript | https://github.com/atom/language-coffee-script | MIT |
| cpp | https://github.com/jeff-hykin/better-cpp-syntax | MIT |
| cpp | https://github.com/jeff-hykin/better-c-syntax | MIT |
| cpp | https://github.com/textmate/c.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| cpp | https://github.com/NVIDIA/cuda-cpp-grammar | MIT |
| csharp | https://github.com/dotnet/csharp-tmLanguage | MIT |
| css | https://github.com/atom/language-css | MIT, TextMate bundle license ("GitHub License") |
| dart | https://github.com/dart-lang/dart-syntax-highlight | BSD-3-Clause (Google LLC) ("BSD") |
| diff | https://github.com/textmate/diff.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| docker | https://github.com/moby/moby | Apache-2.0 ("Apache2") |
| fsharp | https://github.com/ionide/ionide-fsgrammar | MIT |
| git | https://github.com/textmate/git.tmbundle | MIT |
| git | https://github.com/textmate/diff.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| go | https://github.com/jeff-hykin/better-go-syntax | MIT |
| groovy | https://github.com/textmate/groovy.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| handlebars | https://github.com/daaain/Handlebars | MIT |
| hlsl | https://github.com/tgjones/shaders-tmLanguage | MIT |
| html | https://github.com/textmate/html.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| ini | https://github.com/textmate/ini.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| java | https://github.com/atom/language-java | MIT |
| javascript | https://github.com/microsoft/TypeScript-TmLanguage | MIT |
| javascript | https://github.com/textmate/javascript.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| json | https://github.com/microsoft/vscode-JSON.tmLanguage | MIT |
| julia | https://github.com/JuliaEditorSupport/atom-language-julia | MIT |
| latex | https://github.com/jlelong/vscode-latex-basics | MIT |
| less | https://github.com/atom/language-less | MIT |
| log | https://github.com/emilast/vscode-logfile-highlighter | MIT |
| lua | https://github.com/sumneko/lua.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| make | https://github.com/fadeevab/make.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| markdownbasics | https://github.com/textmate/markdown.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| markdownbasics | https://github.com/microsoft/vscode-markdown-tm-grammar | MIT |
| markdownmath | https://github.com/James-Yu/LaTeX-Workshop | MIT |
| objectivec | https://github.com/jeff-hykin/cpp-textmate-grammar | MIT |
| pascal | https://github.com/AnsonYeung/vscode-pascal-language-basics | MIT |
| perl | https://github.com/textmate/perl.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| php | https://github.com/atom/language-php | MIT |
| powershell | https://github.com/PowerShell/EditorSyntax | MIT |
| pug | https://github.com/davidrios/pug-tmbundle | MIT |
| python | https://github.com/MagicStack/MagicPython | MIT |
| r | https://github.com/Ikuyadeu/vscode-R | MIT |
| razor | https://github.com/demyte/language-cshtml | MIT |
| ruby | https://github.com/textmate/ruby.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| rust | https://github.com/dustypomerleau/rust-syntax | MIT |
| scss | https://github.com/atom/language-sass | MIT |
| shaderlab | https://github.com/tgjones/shaders-tmLanguage | MIT |
| shellscript | https://github.com/atom/language-shellscript | MIT |
| sql | https://github.com/microsoft/vscode-mssql | MIT |
| swift | https://github.com/owensd/vscode-swift | MIT |
| swift | https://github.com/freebroccolo/atom-language-swift | MIT |
| typescriptbasics | https://github.com/microsoft/TypeScript-TmLanguage | MIT |
| typst | https://github.com/Myriad-Dreamin/tinymist | Apache-2.0 |
| vb | https://github.com/textmate/asp.vb.net.tmbundle | TextMate bundle license ("TextMate Bundle License") |
| x86asm | https://github.com/haxo-games/Universal-Assembly-Syntax-Highlighting | MIT |
| xml | https://github.com/atom/language-xml | MIT |
| yaml | https://github.com/textmate/yaml.tmbundle | TextMate bundle license ("TextMate Bundle License") |

The copyright lines that the package carries for its grammars:

- https://github.com/mattneub/AsciiDoc-TextMate-2.tmbundle: "Copyright (c) Matt Neuburg"
- https://github.com/textmate/c.tmbundle: "Copyright (c) textmate-c.tmbundle authors"
- https://github.com/atom/language-css: "Copyright (c) 2014 GitHub Inc."
- https://github.com/dart-lang/dart-syntax-highlight: "Copyright 2020, the Dart project authors."
- https://github.com/textmate/diff.tmbundle: "Copyright (c) textmate-diff.tmbundle project authors"
- https://github.com/textmate/git.tmbundle: "Copyright (c) 2008 Tim Harper"
- https://github.com/textmate/groovy.tmbundle: "Copyright (c) textmate-groovy.tmbundle project
  authors"
- https://github.com/daaain/Handlebars: "Copyright (c) daaain/Handlebars project authors"
- https://github.com/textmate/html.tmbundle: "Copyright (c) textmate-html.tmbundle project authors"
- https://github.com/textmate/ini.tmbundle: "Copyright (c) textmate-ini.tmbundle project authors"
- https://github.com/textmate/javascript.tmbundle: "Copyright (c) textmate-javascript.tmbundle
  project authors"
- https://github.com/jlelong/vscode-latex-basics: "Copyright (c) vscode-latex-basics authors"
- https://github.com/sumneko/lua.tmbundle: "Copyright (c) sumneko-lua.tmbundle project authors"
- https://github.com/fadeevab/make.tmbundle: "Copyright (c) textmate-make.tmbundle project authors"
- https://github.com/textmate/markdown.tmbundle: "Copyright (c) markdown.tmbundle authors"
- https://github.com/textmate/perl.tmbundle: "Copyright (c) textmate-perl.tmbundle project authors"
- https://github.com/textmate/ruby.tmbundle: "Copyright (c) textmate-ruby.tmbundle project authors"
- https://github.com/owensd/vscode-swift: "Copyright (c) 2015 David Owens II"
- https://github.com/freebroccolo/atom-language-swift: "Copyright (c) 2014 Darin Morrison"
- https://github.com/textmate/asp.vb.net.tmbundle: "Copyright (c) textmate-asp.vb.net.tmbundle
  project authors"
- https://github.com/textmate/yaml.tmbundle: the line below

```
Copyright (c) 2015 FichteFoll <fichtefoll2@googlemail.com>
```

The themes, each by its name, its file in the package and where it comes from:

| Theme | File | Origin |
|---|---|---|
| Abyss | abyss-color-theme.json | Visual Studio Code |
| Atom One Dark | atom-one-dark-color-theme.json | none stated |
| Atom One Light | atom-one-light-color-theme.json | none stated |
| Dark+ | dark_plus.json | Visual Studio Code |
| Dark (Visual Studio) | dark_vs.json | Visual Studio Code |
| Dimmed Monokai | dimmed-monokai-color-theme.json | Visual Studio Code |
| Dracula | dracula-color-theme.json | none stated |
| Dark High Contrast | hc_black.json | Visual Studio Code |
| Light High Contrast | hc_light.json | Visual Studio Code |
| Kimbie Dark | kimbie-dark-color-theme.json | Visual Studio Code |
| Light+ | light_plus.json | Visual Studio Code |
| Light (Visual Studio) | light_vs.json | Visual Studio Code |
| Monokai | monokai-color-theme.json | Visual Studio Code |
| OneDark | onedark-color-theme.json | none stated |
| Quiet Light | quietlight-color-theme.json | Visual Studio Code |
| Red | Red-color-theme.json | Visual Studio Code |
| Solarized Dark | solarized-dark-color-theme.json | Visual Studio Code |
| Solarized Light | solarized-light-color-theme.json | Visual Studio Code |
| Tomorrow Night Blue | tomorrow-night-blue-color-theme.json | Visual Studio Code |
| Visual Studio Dark | visual-studio-dark-theme.json | none stated |
| Visual Studio Light | visual-studio-light-theme.json | none stated |

Six themes in the package, Dracula, Atom One Dark, Atom One Light, OneDark, Visual Studio Dark and
Visual Studio Light, state no origin in the package or in TextMateSharp's history, and TextMateSharp
is a port of microsoft/vscode-textmate based on the Java port eclipse/tm4e. They are listed here
under TextMateSharp's own declared license, the MIT License, which is all the package states for
them.

### Inter

The app's typeface: Inter 3.019 ("Version 3.019;git-0a5106e0b"), six faces inside
`Avalonia.Fonts.Inter.dll`.

Files: Inter-Thin.ttf, Inter-Light.ttf, Inter-Regular.ttf, Inter-Medium.ttf, Inter-SemiBold.ttf, Inter-Bold.ttf

Source: https://github.com/rsms/inter (v3.19)

```
Copyright (c) 2016-2020 The Inter Project Authors.
"Inter" is trademark of Rasmus Andersson.
https://github.com/rsms/inter

Copyright 2020 The Inter Project Authors (https://github.com/rsms/inter)
```

License: OFL-1.1. The first three lines head Inter's license file; the last is the fonts' own
copyright line.

### JetBrains Mono

The typeface of code in the diff, the blame and the conflict resolver: JetBrains Mono NL Regular,
version 2.304, unchanged, inside `VisualCommit.dll` (from `src/VisualCommit.App/Assets/Fonts/`;
decision D64).

Files: JetBrainsMonoNL-Regular.ttf

Source: https://github.com/JetBrains/JetBrainsMono

```
Copyright 2020 The JetBrains Mono Project Authors (https://github.com/JetBrains/JetBrainsMono)
```

License: OFL-1.1.

### Lucide and Feather

The icon outlines in `src/VisualCommit.App/Theme/Icons.axaml`, inside `VisualCommit.dll`, are taken
from Lucide (https://lucide.dev), rewritten as path strings.

Source: https://github.com/lucide-icons/lucide; Feather, https://github.com/feathericons/feather

```
Copyright (c) 2026 Lucide Icons and Contributors
Copyright (c) 2013-present Cole Bemis
```

License: ISC, MIT. Lucide is under the ISC License, the first line; Feather under the MIT License,
the second.

Some Lucide icons derive from the Feather project. Of the icons used here, these do: arrow-down,
arrow-up, check, chevron-down, chevron-left, chevron-right, chevron-up, cloud, crosshair, download,
ellipsis (Feather's more-horizontal), eye-off, folder, git-pull-request, list, minus, moon, plus,
rotate-cw, search, settings, tag, triangle-alert (Feather's alert-triangle) and x.

## License texts

### MIT

The MIT License. Each component's copyright lines above stand for "the above copyright notice".

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### ISC

```
Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

### BSD-2-Clause

```
Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this
  list of conditions and the following disclaimer in the documentation and/or
  other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### BSD-3-Clause (ANGLE)

```
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

    Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer.

    Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution.

    Neither the name of TransGaming Inc., Google Inc., 3DLabs Inc.
    Ltd., nor the names of their contributors may be used to endorse
    or promote products derived from this software without specific
    prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
```

### BSD-3-Clause (Google LLC)

```
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * Neither the name of Google LLC nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### Apache-2.0

```
                                 Apache License
                           Version 2.0, January 2004
                        https://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.

      To apply the Apache License to your work, attach the following
      boilerplate notice, with the fields enclosed by brackets "[]"
      replaced with your own identifying information. (Don't include
      the brackets!)  The text should be enclosed in the appropriate
      comment syntax for the file format. We also recommend that a
      file or class name and description of purpose be included on the
      same "printed page" as the copyright notice for easier
      identification within third-party archives.

   Copyright [yyyy] [name of copyright owner]

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       https://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
```

### OFL-1.1

The SIL Open Font License, Version 1.1.

```
-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded, 
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.
```

### zlib

```
This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```

### TextMate bundle license

The grant that the TextMate bundles state for their files.

```
Permission to copy, use, modify, sell and distribute this
software is granted. This software is provided "as is" without
express or implied warranty, and with no claim as to its
suitability for any purpose.
```

## Keeping this file true

`ThirdPartyNoticesTests` holds this file to the build: a package that the build ships and this file
does not name with its version, a file it ships that is not on its entry's `Files:` line, a package
named here that the build no longer ships, a grammar or theme that is not listed, or a license named
with no text fails it. Each entry keeps the form above: one `Packages:` line of `<id> <version>`
pairs (NuGet packages only), one `Files:` line of file names, and a `License:` line whose names, up
to its first full stop, are headings under "License texts" or files in `notices/`. When a package is
added or updated, take its license, its copyright lines and its notice files from the package in the
NuGet cache (`~/.nuget/packages/<id>/<version>/`) and from its source at the commit its `.nuspec`
names in `<repository>`, and replace a vendor's file in `notices/` whole. The app embeds this file,
`LICENSE` and `notices/` (`VisualCommit.App.csproj`), so a change to any of them is a change to the
app.
