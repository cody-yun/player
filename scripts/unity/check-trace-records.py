"""Compare every exported rounded value against original TS recorder output, not just summary stats."""
from pathlib import Path
import argparse,gzip,hashlib,json
p=argparse.ArgumentParser();p.add_argument('--directory',type=Path,required=True);p.add_argument('--prefix',default='trace-');args=p.parse_args()
root=Path(__file__).resolve().parents[2];fixture=root/'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/traces.json.gz'
data=json.loads(gzip.decompress(fixture.read_bytes()));report={'result':'Passed','fixtureSha256':hashlib.sha256(fixture.read_bytes()).hexdigest(),'records':[]}
for index,name in enumerate(['normal','narrow']):
 path=args.directory/(args.prefix+name+'.json');actual=json.loads(path.read_text());expected=data['pipelineRecords'][index];assert actual==expected,(name,'exported values differ from original')
 report['records'].append({'name':name,'frames':len(actual),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size})
(args.directory/'trace-record-parity.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
