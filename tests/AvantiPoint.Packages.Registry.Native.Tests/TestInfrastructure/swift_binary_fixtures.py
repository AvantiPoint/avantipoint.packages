"""Build synthetic universal macOS XCFrameworks; never download product artifacts."""

import argparse
import pathlib
import plistlib
import shutil
import subprocess
import sys
import zipfile


MODULES = ("NativeFixtureAlpha", "NativeFixtureBeta", "NativeFixtureGamma", "NativeFixtureDelta")


def run(*arguments):
    return subprocess.check_output(arguments, text=True).strip()


def build(output):
    if sys.platform != "darwin":
        raise RuntimeError("Synthetic XCFramework compilation requires macOS and Xcode.")
    output.mkdir(parents=True, exist_ok=False)
    sdk = run("xcrun", "--sdk", "macosx", "--show-sdk-path")
    for module in MODULES:
        work = output / module
        work.mkdir()
        source = work / "Fixture.swift"
        source.write_text('public func fixtureGreeting() -> String { "synthetic native feed fixture" }\n')
        framework = work / (module + ".framework")
        modules = framework / "Modules" / (module + ".swiftmodule")
        modules.mkdir(parents=True)
        binaries = []
        for architecture in ("arm64", "x86_64"):
            target = architecture + "-apple-macosx11.0"
            triple = architecture + "-apple-macos"
            binary = work / (architecture + ".dylib")
            run("xcrun", "swiftc", "-emit-library", "-emit-module", "-enable-library-evolution",
                "-module-name", module, "-target", target, "-sdk", sdk,
                "-emit-module-path", str(modules / (triple + ".swiftmodule")),
                "-emit-module-interface-path", str(modules / (triple + ".swiftinterface")),
                "-Xlinker", "-install_name", "-Xlinker", "@rpath/" + module + ".framework/" + module,
                "-o", str(binary), str(source))
            binaries.append(str(binary))
        run("xcrun", "lipo", "-create", *binaries, "-output", str(framework / module))
        for file in modules.glob("*.swiftsourceinfo"):
            file.unlink()
        with (framework / "Info.plist").open("wb") as file:
            plistlib.dump({
                "CFBundleExecutable": module,
                "CFBundleIdentifier": "test.nativefeed." + module,
                "CFBundlePackageType": "FMWK",
                "CFBundleVersion": "1",
                "CFBundleShortVersionString": "1.0.0",
                "CFBundleSupportedPlatforms": ["MacOSX"],
                "MinimumOSVersion": "11.0",
                "DTPlatformName": "macosx",
            }, file)
        xcframework = output / (module + ".xcframework")
        run("xcodebuild", "-create-xcframework", "-framework", str(framework), "-output", str(xcframework))
        with zipfile.ZipFile(output / (module + ".xcframework.zip"), "w", zipfile.ZIP_DEFLATED) as archive:
            for file in sorted(xcframework.rglob("*")):
                if file.is_file():
                    archive.write(file, file.relative_to(output).as_posix())
        shutil.rmtree(work)
        shutil.rmtree(xcframework)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    build(parser.parse_args().output.resolve())
