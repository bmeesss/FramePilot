# Third-party notices

FramePilot itself is distributed under the MIT license (see [LICENSE](LICENSE)).
FramePilot does **not** redistribute any third-party binaries. The items below
describe external software that FramePilot can *optionally* interact with on
the user's machine, and the obligations that come with using it.

## PresentMon (optional, not bundled)

* Project: <https://github.com/GameTechDev/PresentMon>
* Version consulted while writing the CSV parser: **2.6.0** (released 2026-09-21)
* Copyright: Copyright (C) 2017-2026 Intel Corporation
* License: MIT (see <https://github.com/GameTechDev/PresentMon/blob/main/LICENSE.txt>)
* Binary used in this repository: **none**

FramePilot reads CSV files produced by PresentMon and can optionally launch a
PresentMon executable that the user has installed themselves. PresentMon is
**not** shipped with FramePilot, so no binary redistribution occurs and no
PresentMon binaries are included in the FramePilot installer or publish
output. Users who download PresentMon are subject to the MIT license shipped
with that download; the MIT license text is reproduced below for convenience.

```
MIT License

Copyright (C) 2017-2026 Intel Corporation

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

PresentMon also documents third-party components it uses (for example ETW
consumer libraries); those obligations apply to the PresentMon binary the user
downloads, not to FramePilot.

## .NET runtime

FramePilot is published as a self-contained application. The .NET runtime and
the WPF framework are redistributed under the MIT license
(<https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>). The generated
`ThirdPartyNotices.txt` in the publish output lists the exact .NET
components that are bundled.

## NuGet dependencies (build-time only)

The shipped application has **no** NuGet dependencies. The test project uses
`xunit`, `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`, which are
build-time only and are not redistributed with the application. All of them
are MIT-licensed.
