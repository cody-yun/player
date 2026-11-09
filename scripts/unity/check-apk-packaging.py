"""Measure live ZIP payload vs abandoned regions in the verification APK.

The APK signing block is accounted for separately. This does not rewrite/re-sign
the archive; check-android-apk.py validates its signature and 16 KB alignment.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--apk', type=Path, default=root/'unity/TestResults/Android/LivLivVerification.apk')
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--compare', type=Path, help='Previous JSON report (payload hashes)')
parser.add_argument('--max-unused-bytes', type=int, default=262144)
args = parser.parse_args()
raw = args.apk.read_bytes()
entries, intervals = {}, []
with zipfile.ZipFile(args.apk) as archive:
    central = archive.start_dir
    for entry in archive.infolist():
        assert entry.filename not in entries, ('Duplicate ZIP member', entry.filename)
        header = struct.unpack_from('<4s5H3I2H', raw, entry.header_offset)
        assert header[0] == b'PK\x03\x04', entry.filename
        end = entry.header_offset + 30 + header[9] + header[10] + entry.compress_size
        if header[2] & 8:
            # This small verification APK uses ZIP32, not ZIP64 descriptors.
            assert entry.file_size < 2**32 and entry.compress_size < 2**32
            end += 16 if raw[end:end+4] == b'PK\x07\x08' else 12
        intervals.append((entry.header_offset, end))
        entries[entry.filename] = {'size': entry.file_size, 'compressed': entry.compress_size,
                                   'sha256': hashlib.sha256(archive.read(entry)).hexdigest()}

signing_bytes = 0
if raw[central-16:central] == b'APK Sig Block 42':
    signing_bytes = struct.unpack_from('<Q', raw, central-24)[0] + 8
    signing_start = central-signing_bytes
    assert struct.unpack_from('<Q', raw, signing_start)[0] == signing_bytes-8
    intervals.append((signing_start, central))

cursor, gaps = 0, []
for start, end in sorted(intervals):
    assert cursor <= start <= end <= central, ('Overlapping or invalid ZIP region', cursor, start, end)
    if start > cursor:
        gaps.append({'offset': cursor, 'bytes': start-cursor, 'prefixHex': raw[cursor:cursor+16].hex()})
    cursor = end
if cursor < central:
    gaps.append({'offset': cursor, 'bytes': central-cursor, 'prefixHex': raw[cursor:cursor+16].hex()})

report = {'apk': str(args.apk.resolve()), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
          'compressed': sum(e['compressed'] for e in entries.values()), 'signingBlockBytes': signing_bytes,
          'centralDirectoryOffset': central, 'unusedBytes': sum(g['bytes'] for g in gaps),
          'gaps': sorted(gaps, key=lambda g: g['bytes'], reverse=True), 'entries': entries}
if args.compare:
    before = json.loads(args.compare.read_text())
    old = before['entries']
    report['comparison'] = {'beforeSha256': before['sha256'], 'beforeBytes': before['bytes'],
        'savedBytes': before['bytes']-len(raw),
        'added': sorted(entries.keys()-old.keys()), 'removed': sorted(old.keys()-entries.keys()),
        'changed': sorted(k for k in entries.keys() & old.keys() if entries[k]['sha256'] != old[k]['sha256']),
        'unchanged': sum(entries[k]['sha256'] == old[k]['sha256'] for k in entries.keys() & old.keys())}
report['passed'] = report['unusedBytes'] <= args.max_unused_bytes
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps({k: v for k, v in report.items() if k not in ('gaps', 'entries')}, indent=2))
assert report['passed'], ('APK contains excessive unused ZIP regions', report['unusedBytes'])
