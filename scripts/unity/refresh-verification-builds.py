"""Refresh verification players sequentially through Unity CLI; stop at the first failed build.

iOS exports into this run's own directory, preserving the user's signing workspace.
This records compilation evidence only. Platform execution remains a separate check.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess, time

root=Path(__file__).resolve().parents[2]
targets={
    'webgl':('WebGL','WebGL'), 'macos':('StandaloneOSX','MacOS'),
    'macos-intel':('StandaloneOSX','MacOSIntel'), 'android':('Android','Android'),
    'windows':('StandaloneWindows64','Windows'), 'linux':('StandaloneLinux64','Linux'),
    'ios':('iOS','iOS')}
p=argparse.ArgumentParser();p.add_argument('targets',nargs='+',choices=targets);args=p.parse_args()
output=root/'unity/TestResults/build-refresh'/str(time.time_ns());output.mkdir(parents=True)
def hashes():
    files=[]
    for folder in [root/'unity/Packages/ai.livliv.player/Runtime',root/'unity/Packages/ai.livliv.ugui/Runtime',
                   root/'unity/Packages/ai.livliv.mediapipe/Runtime',root/'unity/Assets/Verification']:
        files.extend(f for f in folder.rglob('*') if f.suffix in ['.cs','.shader','.jslib'])
    return {str(f.relative_to(root)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(files)}
report={'result':'Running','scope':'Sequential target-selected Unity CLI compilation only; not device execution',
        'sourceHashes':hashes(),'builds':[],'output':str(output)}
def save(): (output/'evidence.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'output':str(output)}),flush=True);save()
try:
    for label in args.targets:
        target,method=targets[label]
        command=[str(Path.home()/'.unity/bin/unity'),'build',str(root/'unity'),'--target',target,
                 '--execute-method','VerificationBuild.'+method,'--log-file',str(output/(label+'.log')),'--no-tail']
        if label=='ios':command+=['--output-path',str(output/'ios-export')]
        before=time.monotonic()
        with (output/(label+'-cli.log')).open('w') as log:
            status=subprocess.run(command,cwd=root,stdout=log,stderr=subprocess.STDOUT).returncode
        row={'target':label,'command':command,'exitCode':status,'seconds':time.monotonic()-before}
        report['builds'].append(row);save()
        if status:raise RuntimeError(label+' CLI failed; inspect '+str(output/(label+'-cli.log')))
        summary=json.loads((root/'unity/TestResults'/(label+'-build.json')).read_text())
        row['unity']=summary
        (output/(label+'-build.json')).write_text(json.dumps(summary,indent=2)+'\n')
        if summary['result']!='Succeeded' or summary['errors']:raise RuntimeError(label+' build report failed')
        if hashes()!=report['sourceHashes']:raise RuntimeError('Verification sources changed during build refresh')
        save();print(json.dumps(row),flush=True)
    report['result']='Passed'
except BaseException as error:
    report['result']='Failed';report['error']=str(error);raise
finally:save()
