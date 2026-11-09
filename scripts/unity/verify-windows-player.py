"""Portable Windows runner. Standard-library Python 3.10+, no Unity Editor required.

--check-only checks transferred files on any OS; it is never a Windows runtime pass.
The actual probe uses the player's default graphics selection and generated video only.
"""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, math, platform, struct, subprocess, sys, tempfile, time, zipfile

def sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as file:
        for chunk in iter(lambda:file.read(1024*1024),b''):digest.update(chunk)
    return digest.hexdigest()

def payload(root,name):
    relative=PurePosixPath(name)
    if not name or relative.is_absolute() or '..' in relative.parts or '\\' in name or ':' in name:
        raise ValueError('Invalid manifest path: '+name)
    path=root.joinpath(*relative.parts)
    if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('Payload escapes bundle: '+name)
    return path

def check_bundle(root):
    manifest_path=root/'bundle-manifest.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('schemaVersion')!=1 or manifest.get('platform')!='windows-x64':
        raise ValueError('Unsupported bundle manifest')
    files=manifest['files']
    for name,digest in files.items():
        path=payload(root,name)
        if not path.is_file() or sha(path)!=digest:raise ValueError('Missing or changed payload: '+name)
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    if actual!=set(files)|{'bundle-manifest.json'}:raise ValueError('Unexpected files in bundle; extract into an empty directory')
    if manifest['executable'] not in files:raise ValueError('Executable is not fingerprinted')
    return manifest

def validate_report(report):
    validate_environment(report)
    states=report.get('states',[])
    if len(states)!=10:raise ValueError('Expected ten generated-input states')
    for state in states:
        if state.get('scale')!=1 or state.get('model')!='Aoi':raise ValueError('Model/scale invariant failed')
        if state.get('expressionCount')!=4 or not state.get('expressionLabel') or state.get('expressionTranslation')!='Smile':
            raise ValueError('Typed expression metadata was lost')
    if not states[1]['faceSeen'] or not math.isfinite(states[2]['angleZ']) or abs(states[2]['angleZ'])<=5 or states[3]['faceSeen']:
        raise ValueError('Face detection/rotation/loss failed')
    if not states[4]['faceSeen'] or not states[5]['faceSeen']:raise ValueError('Face reacquisition/distance failed')
    if states[7]['tracking'] or not states[7]['faceStatus'].startswith('Failed:'):
        raise ValueError('Terminal source failure was not isolated')
    if states[7]['frames']<=states[6]['frames']:raise ValueError('Renderer stopped after input failure')
    if not all(report.get(key) for key in ['released','sourceFailureHandled','restarted','expressionLabels']):
        raise ValueError('Release/restart/metadata checks failed')
    if report.get('failedSourceReleases')!=1 or report.get('restartedSourceReleases')!=1:
        raise ValueError('Source was not released exactly once')

def validate_environment(report):
    if report.get('result')!='Passed' or report.get('error') or report.get('platform')!='WindowsPlayer' or report.get('graphics') not in ['Direct3D11','Direct3D12']:
        raise ValueError('Probe did not pass on a Windows Direct3D player: '+str(report.get('error')))

def validate_presentation(report):
    validate_environment(report)
    if not all(report.get(k) for k in ['pngPoster','webpFilePoster','invalidPoster','latePoster','disposed','supported','contextLostHandled']):
        raise ValueError('Presentation support/lifecycle contract failed')
    if any(report.get(k)!=0 for k in ['supportProbes','targets','materials','posterTextures','maskTextures']):
        raise ValueError('Presentation resources remained allocated')
    if report.get('jpegPosters')!=['baseline','progressive']:raise ValueError('JPEG cases missing')
    states=report['states']
    if [s['name'] for s in states]!=['portrait','reuse','landscape','density','budget','zero','recovered']:
        raise ValueError('Surface states missing')
    if states[0]['targetId']!=states[1]['targetId'] or states[2]['targetId']==states[1]['targetId']:
        raise ValueError('Surface target reuse failed')
    for s in states:
        if s['time']!=0 or s['scale']!=1:raise ValueError('Surface clock/scale changed')
        if s['name']=='zero':
            if s['width']!=0 or s['height']!=0:raise ValueError('Zero surface was not released')
        elif s['active']<=100 or not math.isfinite(s['roundtrip']) or s['roundtrip']>1:
            raise ValueError('Surface pixels/alpha roundtrip failed')
    if not states[4]['limited'] or states[4]['width']*states[4]['height']>10000:raise ValueError('Surface budget failed')
    cameras=report['cameraSurfaces']
    if [s['name'] for s in cameras]!=['front','right','left','up','down','orthographic','screen','nested']:
        raise ValueError('Camera surface states missing')
    for s in cameras:
        if abs(s['width']-s['expectedWidth'])>1 or abs(s['height']-s['expectedHeight'])>1 or s['active']<=2000 or s['time']!=0 or s['scale']!=1:
            raise ValueError('Camera surface pixels/density failed')
    masks=report['masks']
    if [s['name'] for s in masks]!=['glance','halfblink','combined','reused','larger','smaller','offscreen','recovered']:
        raise ValueError('Mask states missing')
    for s in masks:
        if s['scale']!=1 or s['storageBytes']>=s['fullSizeBytes'] or s['channels']!=(0 if s['name']=='offscreen' else 2):
            raise ValueError('Cropped mask bounds/storage failed')
    if masks[2]['maskId']!=masks[3]['maskId'] or masks[4]['maskId']==masks[3]['maskId'] or not masks[4]['maskId']==masks[5]['maskId']==masks[6]['maskId'] or masks[7]['maskId']==masks[6]['maskId']:
        raise ValueError('Mask reuse/recovery failed')
    text=report['textMarks']
    if [s['name'] for s in text]!=['empty','latin','korean','literal','default','retina-empty','retina','budget']:
        raise ValueError('Text states missing')
    for s in text:
        if any(s[k]!=0 for k in ['watermarks','coverageTargets','coverageMeshes','coverageMaterials']):raise ValueError('Text resources remained allocated')
        if s['font']!=('LivLivMarkFont' if s['name']=='default' else 'NanumGothic-Regular'):raise ValueError('Unexpected mark font')
        if s['name']=='budget':
            if s['code']!='too-large':raise ValueError('Text budget error missing')
        elif s.get('error'):raise ValueError('Text mark failed')

def validate_speech(report):
    validate_environment(report)
    if report.get('inputs')!=['PCM16 WAV','MP3','float WAV']:raise ValueError('Encoded speech cases missing')
    if report.get('hooks')!=[f'{i}:{event}' for i in range(1,4) for event in ['start','end']]:raise ValueError('Speech order/hooks failed')
    if report.get('idle')!=1 or report.get('stopped')!=1 or report.get('stateEvents')!=[False,True,False]:raise ValueError('Speech state contract failed')
    if not report.get('peak',0)>1e-5 or not report.get('mouth',0)>.1 or report.get('scale')!=1:raise ValueError('Speech envelope/lip sync failed')
    if report.get('remainingPieces')!=0 or report.get('remainingClips')!=0:raise ValueError('Speech resources remained allocated')

def check_png(path,size=None):
    image=path.read_bytes()
    if image[:8]!=b'\x89PNG\r\n\x1a\n' or len(image)<24:raise ValueError('Missing or invalid PNG: '+path.name)
    width,height=struct.unpack('>II',image[16:24])
    if width<1 or height<1 or size is not None and (width,height)!=size:raise ValueError('Invalid PNG dimensions: '+path.name)

def require_windows():
    if sys.platform!='win32':raise RuntimeError('Actual execution requires Windows; use --check-only to audit files on this OS')

def run_probe(root,manifest,output,timeout):
    require_windows()
    evidence={'result':'Failed','scope':'Windows default-backend generated video, presentation and speech; no camera/microphone or physical device-removal test',
              'probes':{},'baseline':manifest['baseline'],'manifestSha256':sha(root/'bundle-manifest.json'),
              'os':platform.platform(),'machine':platform.machine()}
    try:
        for name,flag,file,validate in [('face','-livlivSmoke','native-player.json',validate_report),
                ('presentation','-livlivPresentationSmoke','presentation-player.json',validate_presentation),
                ('speech','-livlivSpeechSmoke','speech-player.json',validate_speech)]:
            folder=output/name;folder.mkdir()
            command=[str(payload(root,manifest['executable'])),'-batchmode','-logFile',str(folder/'player.log'),flag,str(folder)]
            record=evidence['probes'][name]={'command':command}
            with (folder/'launcher.log').open('w',encoding='utf-8') as log:
                process=subprocess.Popen(command,cwd=root,stdout=log,stderr=subprocess.STDOUT)
                try:code=process.wait(timeout=timeout)
                except subprocess.TimeoutExpired:
                    process.kill();process.wait();raise TimeoutError('Owned verification player timed out: '+name)
            record['exitCode']=code
            if code!=0:raise RuntimeError(name+' exited with code '+str(code)+'; inspect its player/launcher log')
            report=json.loads((folder/file).read_text(encoding='utf-8-sig'));validate(report)
            record.update(result='Passed',graphics=report['graphics'])
            if name=='face':
                for image in ['neutral','detected','rotated','farther','nearer','restarted']:check_png(folder/(image+'.png'),(512,768))
            if name=='presentation':
                for image in ['jpeg-baseline','jpeg-progressive']+['camera-'+r['name'] for r in report['cameraSurfaces']]+['text-'+r['name'] for r in report['textMarks'] if r['name']!='budget']:
                    check_png(folder/(image+'.png'))
        # Detect changes while the player was running, as well as before launch.
        check_bundle(root)
        evidence['result']='Passed'
    except Exception as error:
        evidence['error']=str(error)
    finally:
        evidence['files']={p.relative_to(output).as_posix():sha(p) for p in sorted(output.rglob('*')) if p.is_file()}
        (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n',encoding='utf-8')
    return evidence

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle',type=Path,default=Path(__file__).resolve().parent)
    parser.add_argument('--check-only',action='store_true')
    parser.add_argument('--output',type=Path)
    parser.add_argument('--timeout',type=int,default=180)
    args=parser.parse_args();root=args.bundle.resolve()
    try:
        manifest=check_bundle(root)
        if args.check_only:
            print(json.dumps({'result':'BundleVerified','runtimeExecuted':False,'files':len(manifest['files']),'baseline':manifest['baseline']},indent=2));return 0
        require_windows()
        output=(args.output or Path(tempfile.gettempdir())/'livliv-windows-verification'/str(time.time_ns())).resolve()
        if output.is_relative_to(root):raise ValueError('Evidence output must be outside the fingerprinted bundle')
        archive=output.parent/(output.name+'.zip')
        if archive.exists():raise FileExistsError('Evidence archive already exists: '+str(archive))
        output.mkdir(parents=True,exist_ok=False)
        report=run_probe(root,manifest,output,args.timeout)
        with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as zipped:
            for path in sorted(output.rglob('*')):
                if path.is_file():zipped.write(path,path.relative_to(output).as_posix())
        print(json.dumps({'result':report['result'],'output':str(output),'evidenceZip':str(archive),'error':report.get('error')},indent=2))
        return 0 if report['result']=='Passed' else 1
    except Exception as error:
        print(json.dumps({'result':'Failed','error':str(error)},indent=2));return 1

if __name__=='__main__':sys.exit(main())
