// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;
using AdminToolbox.Dns;

namespace AdminToolbox.Tools.Helpers;

/// <summary>
/// Services owned by the lazily loaded tool assembly. They are created here instead of being registered
/// in Program.cs, so the host never touches Tools, Core or Dns types at startup (docs/adr/0005).
/// </summary>
public static class ToolServices
{
    private static readonly Lazy<IDnsResolver> Resolver = new(() => new DohClient(new HttpClient()));

    public static IDnsResolver Dns => Resolver.Value;
}
