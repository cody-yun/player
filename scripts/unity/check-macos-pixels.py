"""Compare explicit previous ARM64/current ARM64/x64 presentation evidence, using Pillow."""
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
import argparse,hashlib,json

parser=argparse.ArgumentParser()
for key in ['baseline','arm64','x64','output']:parser.add_argument('--'+key,type=Path,required=True)
args=parser.parse_args()
names=[name+'.png' for name in ['budget','density','landscape','portrait','recovered','reuse',
       'mask-combined','mask-glance','mask-halfblink','mask-larger','mask-offscreen','mask-recovered','mask-reused','mask-smaller',
       'text-default','text-empty','text-korean','text-latin','text-literal','text-retina-empty','text-retina']]
report={}
for label,directory in [('arm64',args.arm64),('x64',args.x64)]:
    evidence=json.loads((directory/'evidence.json').read_text());assert evidence['result']=='Passed'
    assert evidence['requestedArchitecture']==('arm64' if label=='arm64' else 'x86_64')
    rows={}
    for name in names:
        a=args.baseline/name;b=directory/name
        with Image.open(a) as left,Image.open(b) as right:
            assert left.size==right.size
            diff=ImageChops.difference(left.convert('RGBA'),right.convert('RGBA'))
            row={'size':list(left.size),'maxChannelLsb':max(v[1] for v in diff.getextrema()),
                 'meanChannelLsb':sum(ImageStat.Stat(diff).mean)/4,
                 'baseline':str(a.resolve()),'current':str(b.resolve()),
                 'baselineSha256':hashlib.sha256(a.read_bytes()).hexdigest(),
                 'currentSha256':hashlib.sha256(b.read_bytes()).hexdigest()}
        rows[name]=row
    report[label]=rows
args.output.write_text(json.dumps(report,indent=2)+'\n')
for label,rows in report.items():
    maximum=max(row['maxChannelLsb'] for row in rows.values())
    print(label,len(rows),'images; max channel difference:',maximum)
    assert maximum==0,(label,'presentation changed; inspect evidence before accepting')
