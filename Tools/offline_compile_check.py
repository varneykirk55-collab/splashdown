#!/usr/bin/env python3
"""Offline compile check for Bikini Wipeout (no Unity licence or editor launch needed).

Compiles the project's C# assemblies with the Roslyn compiler and the engine assemblies that ship
inside a Unity editor install, after first building reference assemblies for the UGUI, Input System
and Test Framework packages from their sources. It answers one question only: "does every script
compile against this exact Unity version?" It does not run the game.

Usage:
  python3 Tools/offline_compile_check.py --unity <path to Editor/Data> --inputsystem <path to package/InputSystem> [--out <dir>]

  --unity        e.g. C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data
  --inputsystem  the InputSystem folder of com.unity.inputsystem (from Library/PackageCache or the registry tarball)
"""
import argparse, glob, os, subprocess, sys

_p = argparse.ArgumentParser()
_p.add_argument("--unity", required=True)
_p.add_argument("--inputsystem", required=True)
_p.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Temp", "OfflineCompile"))
_a = _p.parse_args()

U = os.path.abspath(_a.unity)
B = f"{U}/Resources/PackageManager/BuiltInPackages"
OUT = os.path.abspath(_a.out)
INPUT_SYSTEM = os.path.abspath(_a.inputsystem)
PROJECT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
_exe = ".exe" if os.name == "nt" else ""
DOTNET = f"{U}/DotNetSdk/dotnet{_exe}"
CSC = glob.glob(f"{U}/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll")[0]
os.makedirs(OUT, exist_ok=True)

def unity_defines():
    d = ["UNITY_6000_6_6", "UNITY_6000_6", "UNITY_6000", "UNITY_5_3_OR_NEWER", "UNITY_5_4_OR_NEWER",
         "UNITY_5_5_OR_NEWER", "UNITY_5_6_OR_NEWER"]
    for y, mx in ((2017, 4), (2018, 4), (2019, 4), (2020, 3), (2021, 3), (2022, 3), (2023, 3)):
        d += [f"UNITY_{y}_{m}_OR_NEWER" for m in range(1, mx + 1)]
    d += [f"UNITY_6000_{m}_OR_NEWER" for m in range(0, 7)]
    d += ["UNITY_64", "PLATFORM_ARCH_64", "UNITY_INCLUDE_TESTS", "UNITY_ASSERTIONS", "DEBUG", "TRACE",
          "UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_WIN", "UNITY_STANDALONE", "UNITY_STANDALONE_WIN",
          "ENABLE_INPUT_SYSTEM", "ENABLE_MONO", "NET_STANDARD_2_0", "NET_STANDARD", "NET_STANDARD_2_1",
          "NETSTANDARD", "NETSTANDARD2_1", "CSHARP_7_OR_LATER", "CSHARP_7_3_OR_NEWER", "ENABLE_AUDIO",
          "ENABLE_PHYSICS", "ENABLE_PROFILER", "ENABLE_UNITYWEBREQUEST", "ENABLE_TEXTURE_STREAMING",
          "UNITY_TESTS_FRAMEWORK"]
    return d

def engine_refs(editor=True):
    refs = [f"{U}/NetStandard/ref/2.1.0/netstandard.dll"]
    refs += glob.glob(f"{U}/NetStandard/compat/2.1.0/shims/netstandard/*.dll")
    refs += glob.glob(f"{U}/NetStandard/compat/2.1.0/shims/netfx/*.dll")
    for p in sorted(glob.glob(f"{U}/Managed/UnityEngine/*.dll")):
        name = os.path.basename(p)
        if not editor and name.startswith("UnityEditor"):
            continue
        refs.append(p)
    return refs

def net_refs_only():
    return [f"{U}/NetStandard/ref/2.1.0/netstandard.dll"] + \
        glob.glob(f"{U}/NetStandard/compat/2.1.0/shims/netstandard/*.dll") + \
        glob.glob(f"{U}/NetStandard/compat/2.1.0/shims/netfx/*.dll")

def sources(root, exclude_dirs=()):
    out = []
    for dp, dn, fn in os.walk(root):
        dn[:] = [d for d in dn if not d.endswith("~") and os.path.join(dp, d) not in exclude_dirs]
        # nested asmdef => separate assembly
        if dp != root and any(f.endswith(".asmdef") for f in fn):
            dn[:] = []
            continue
        out += [os.path.join(dp, f) for f in fn if f.endswith(".cs")]
    return sorted(out)

def compile(name, srcs, refs, defines=(), unsafe=False, warn_as_info=False, nowarn=""):
    out = f"{OUT}/{name}.dll"
    rsp = f"{OUT}/{name}.rsp"
    with open(rsp, "w") as f:
        f.write("-target:library\n-nostdlib+\n-noconfig\n-langversion:9.0\n-nologo\n-deterministic\n")
        f.write(f"-out:{out}\n")
        if unsafe: f.write("-unsafe+\n")
        if nowarn: f.write(f"-nowarn:{nowarn}\n")
        f.write("-define:" + ";".join(list(unity_defines()) + list(defines)) + "\n")
        for r in refs: f.write(f'-r:"{r}"\n')
        for s in srcs: f.write(f'"{s}"\n')
    p = subprocess.run([DOTNET, CSC, f"@{rsp}"], capture_output=True, text=True)
    lines = [l for l in (p.stdout + p.stderr).splitlines() if l.strip()]
    errors = [l for l in lines if ": error " in l]
    warnings = [l for l in lines if ": warning " in l]
    return p.returncode, errors, warnings, out

def report(name, rc, errors, warnings, show_warnings=True, max_lines=60):
    print(f"== {name}: rc={rc} errors={len(errors)} warnings={len(warnings)}")
    for l in errors[:max_lines]: print("  " + l.replace(PROJECT + "/", ""))
    if show_warnings:
        for l in warnings[:max_lines]: print("  " + l.replace(PROJECT + "/", ""))

def build_packages():
    rc, e, w, ui = compile("UnityEngine.UI", sources(f"{B}/com.unity.ugui/Runtime/UGUI"), engine_refs(),
                           defines=["PACKAGE_PHYSICS", "PACKAGE_ANIMATION", "PACKAGE_UITOOLKIT", "PACKAGE_INPUTSYSTEM_DISABLED"],
                           nowarn="CS0618,CS0612,CS0672,CS0109,CS0414,CS0649,CS0169,CS0067,CS1701,CS1702,CS8632")
    report("UnityEngine.UI", rc, e, w, show_warnings=False)
    rc, e, w, uied = compile("UnityEditor.UI", sources(f"{B}/com.unity.ugui/Editor/UGUI"), engine_refs() + [ui],
                             defines=["PACKAGE_PHYSICS", "PACKAGE_ANIMATION", "PACKAGE_UITOOLKIT"],
                             nowarn="CS0618,CS0612,CS0672,CS0109,CS0414,CS0649,CS0169,CS0067,CS1701,CS1702,CS8632")
    report("UnityEditor.UI", rc, e, w, show_warnings=False)
    ins = INPUT_SYSTEM
    rc, e, w, inp = compile("Unity.InputSystem", sources(ins), engine_refs() + [ui, uied],
                            defines=["UNITY_INPUT_SYSTEM_ENABLE_PHYSICS", "UNITY_INPUT_SYSTEM_ENABLE_UI",
                                     "UNITY_INPUT_SYSTEM_PLATFORM_SCROLL_DELTA", "UNITY_INPUT_SYSTEM_INPUT_MODULE_SCROLL_DELTA",
                                     "UNITY_INPUT_SYSTEM_INPUT_MODULE_NAVIGATION_DEVICE_TYPE",
                                     "UNITY_INPUT_SYSTEM_SENDPOINTERHOVERTOPARENT", "UNITY_INPUT_SYSTEM_PLATFORM_POLLING_FREQUENCY",
                                     "UNITY_INPUTSYSTEM_SUPPORTS_MOUSE_SCRIPT_EVENTS", "UNITY_INPUTSYSTEM_SUPPORTS_FOCUS_EVENTS"],
                            unsafe=True, nowarn="CS0618,CS0612,CS0672,CS0109,CS0414,CS0649,CS0169,CS0067,CS1701,CS1702,CS8632,CS0436,CS0419,CS1591,CS0108,CS0219,CS0162,CS0168")
    report("Unity.InputSystem", rc, e, w, show_warnings=False)
    nunit = f"{B}/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll"
    rc, e, w, tr = compile("UnityEngine.TestRunner", sources(f"{B}/com.unity.test-framework/UnityEngine.TestRunner"),
                           engine_refs() + [nunit],
                           nowarn="CS0618,CS0612,CS0672,CS0109,CS0414,CS0649,CS0169,CS0067,CS1701,CS1702,CS8632,CS0436")
    report("UnityEngine.TestRunner", rc, e, w, show_warnings=False)

def build_project():
    A = f"{PROJECT}/Assets/BikiniWipeout"
    ui, inp, tr = (f"{OUT}/{n}.dll" for n in ("UnityEngine.UI", "Unity.InputSystem", "UnityEngine.TestRunner"))
    nunit = f"{B}/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll"
    noise = "CS1701,CS1702"
    ok = True
    rc, e, w, core = compile("BikiniWipeout.Core", sources(f"{A}/Scripts/Core"), net_refs_only(), nowarn=noise)
    report("BikiniWipeout.Core", rc, e, w); ok &= rc == 0
    rc, e, w, rt = compile("BikiniWipeout.Runtime", sources(f"{A}/Scripts/Runtime"),
                           engine_refs(editor=True) + [core, ui, inp], nowarn=noise)
    report("BikiniWipeout.Runtime", rc, e, w); ok &= rc == 0
    # Player build flavour: no UNITY_EDITOR, no UnityEditor references.
    global unity_defines
    full = unity_defines
    unity_defines = lambda: [d for d in full() if not d.startswith("UNITY_EDITOR")]
    rc, e, w, _ = compile("BikiniWipeout.Runtime.Player", sources(f"{A}/Scripts/Runtime"),
                          engine_refs(editor=False) + [core, ui, inp], nowarn=noise)
    unity_defines = full
    report("BikiniWipeout.Runtime (player defines)", rc, e, w, show_warnings=False); ok &= rc == 0
    if os.path.isdir(f"{A}/Scripts/Editor"):
        rc, e, w, ed = compile("BikiniWipeout.Editor", sources(f"{A}/Scripts/Editor"),
                               engine_refs() + [core, rt, ui, inp], nowarn=noise)
        report("BikiniWipeout.Editor", rc, e, w); ok &= rc == 0
    if os.path.isdir(f"{A}/Tests/EditMode"):
        rc, e, w, _ = compile("BikiniWipeout.Tests.EditMode", sources(f"{A}/Tests/EditMode"),
                              engine_refs() + [core, rt, ed, ui, inp, tr, nunit], nowarn=noise)
        report("BikiniWipeout.Tests.EditMode", rc, e, w); ok &= rc == 0
    if os.path.isdir(f"{A}/Tests/PlayMode"):
        rc, e, w, _ = compile("BikiniWipeout.Tests.PlayMode", sources(f"{A}/Tests/PlayMode"),
                              engine_refs() + [core, rt, ui, inp, tr, nunit], nowarn=noise)
        report("BikiniWipeout.Tests.PlayMode", rc, e, w); ok &= rc == 0
    print("PROJECT COMPILE:", "OK" if ok else "FAILED")
    return ok

if __name__ == "__main__":
    build_packages()
    sys.exit(0 if build_project() else 1)
