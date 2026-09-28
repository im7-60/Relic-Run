#!/usr/bin/env python3
"""Import a locally licensed 2D Game Kit 5.0 package and apply Relic Run changes.

No downloads, Unity account access, or third-party packages are used.
Only Assets/2DGamekit and its .meta are restored from the supplied package.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import sys
import tarfile

PACKAGE_SHA256 = '9fb3f15d73cf3035928a7f379aabd273b0e56b26e86ddd2e8d540f9d4b7c10bb'

def sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def sha_file(path: Path) -> str:
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(chunk)
    return result.hexdigest()

def normalized_text(path: Path) -> str:
    return path.read_text(encoding='utf-8-sig')

def text_sha(text: str) -> str:
    return sha_bytes(text.encode('utf-8'))

def destination(root: Path, relative: str) -> Path:
    posix = PurePosixPath(relative)
    if posix.is_absolute() or '..' in posix.parts or '\\' in relative:
        raise ValueError('Unsafe package path: ' + relative)
    if relative != 'Assets/2DGamekit.meta' and not relative.startswith('Assets/2DGamekit/'):
        raise ValueError('Not a kit asset: ' + relative)
    result = root.joinpath(*posix.parts).resolve()
    if not result.is_relative_to(root):
        raise ValueError('Package destination escapes project: ' + relative)
    return result

def import_package(root: Path, package: Path) -> None:
    if sha_file(package) != PACKAGE_SHA256:
        raise ValueError('Package differs from the tested 2D Game Kit 5.0 download. '
                         'No files changed. See docs/SETUP.md for the expected SHA-256.')
    with tarfile.open(package, 'r:gz') as archive:
        members = {member.name: member for member in archive.getmembers() if member.isfile()}
        planned = []
        seen = set()
        for name, member in members.items():
            if not name.endswith('/pathname'):
                continue
            relative = archive.extractfile(member).read().decode('utf-8-sig').splitlines()[0].rstrip('\x00')
            if relative != 'Assets/2DGamekit' and not relative.startswith('Assets/2DGamekit/'):
                continue
            key = name.rsplit('/', 1)[0]
            for source, target in [('asset', relative), ('asset.meta', relative + '.meta')]:
                content = members.get(key + '/' + source)
                if content is None:
                    continue
                target_path = destination(root, target)
                if target_path in seen:
                    raise ValueError('Duplicate package destination: ' + target)
                seen.add(target_path)
                planned.append((content, target_path))
        if not planned:
            raise ValueError('No 2D Game Kit assets found.')
        added = 0
        for content, target in planned:
            if target.exists():
                continue  # Do not overwrite an existing import or local edits.
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.extractfile(content).read())
            added += 1
        print(f'Imported {added} missing kit files; existing files preserved. Project settings unchanged.')

def apply_patches(root: Path, patch_path: Path, check: bool) -> None:
    patches = json.loads(patch_path.read_text(encoding='utf-8'))['patches']
    prepared = []
    already = 0
    for patch in patches:
        target = destination(root, patch['path'])
        if not target.is_file():
            raise ValueError('Missing dependency: ' + patch['path'] + '. Run with --package first.')
        current = normalized_text(target)
        digest = text_sha(current)
        if digest == patch['afterSha256']:
            already += 1
            continue
        if digest != patch['beforeSha256']:
            raise ValueError('Unexpected contents; refusing to overwrite: ' + patch['path'])
        if check:
            raise ValueError('Patch has not yet been applied: ' + patch['path'])
        lines = current.splitlines(keepends=True)
        for operation in reversed(patch['operations']):
            start = operation['start']
            end = start + len(operation['remove'])
            if lines[start:end] != operation['remove']:
                raise ValueError('Patch context mismatch: ' + patch['path'])
            lines[start:end] = operation['add']
        modified = ''.join(lines)
        if text_sha(modified) != patch['afterSha256']:
            raise ValueError('Patch result did not match the verified result: ' + patch['path'])
        prepared.append((target, modified))
    for target, modified in prepared:
        with target.open('w', encoding='utf-8', newline='\n') as stream:
            stream.write(modified)
    print(f'Verified {len(patches)} patches: {len(prepared)} applied, {already} already present.')

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--package', type=Path, help='Path to your own licensed 2D Game Kit 5.0 .unitypackage')
    parser.add_argument('--check', action='store_true', help='Check that patches are already present; do not write')
    args = parser.parse_args()
    root = args.project.resolve()
    if not (root / 'ProjectSettings' / 'ProjectVersion.txt').is_file():
        parser.error('Expected a Unity project with ProjectSettings/ProjectVersion.txt')
    if args.check and args.package:
        parser.error('--check cannot be combined with --package')
    try:
        if args.package:
            import_package(root, args.package.resolve())
        apply_patches(root, Path(__file__).with_name('kit-patches.json'), args.check)
    except (OSError, ValueError, tarfile.TarError) as error:
        print('Setup stopped: ' + str(error), file=sys.stderr)
        return 1
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
