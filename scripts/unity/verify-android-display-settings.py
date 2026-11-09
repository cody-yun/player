"""Rotate the authorized BlueStacks display, capture actual player output, restore system settings.

No permissions or camera/microphone starts. Image review is separate from these runtime assertions.
"""
from pathlib import Path
import argparse,hashlib,json,re,struct,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--force-display-rotation',action='store_true',help='Override emulator app/overlay orientation policy; does not verify sensor auto-rotation')
parser.add_argument('--resize-display',action='store_true',help='Exercise actual Android portrait/landscape display sizes with rotation zero; not sensor rotation')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
serial='127.0.0.1:5555';package='ai.livliv.verification'
output=root/'unity/TestResults/android-orientation'/str(time.time_ns());output.mkdir(parents=True)
def run(*args):return subprocess.check_output([adb,'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT,timeout=30)
def setting(name):return run('shell','settings','get','system',name).strip()
original={k:setting(k) for k in ['accelerometer_rotation','user_rotation']}
fixed=run('shell','wm','fixed-to-user-rotation').strip();assert fixed in ['default','enabled','disabled'],fixed
sizeBefore=run('shell','wm','size');override=re.search(r'Override size: (\d+x\d+)',sizeBefore)
evidence={'result':'Running','scope':'BlueStacks Android display rotation; no sensor auto-rotation, physical-device or live-input claim','forcedDisplayRotation':args.force_display_rotation,'originalSettings':original,'originalFixedToUserRotation':fixed,'cases':[],'visualReview':'Pending'}
apk=root/'unity/TestResults/Android/LivLivVerification.apk'
evidence['apkSha256']=hashlib.sha256(apk.read_bytes()).hexdigest()
installed=run('shell','pm','path',package).strip().splitlines();assert len(installed)==1,installed
installedPath=installed[0].removeprefix('package:')
evidence['installedApkSha256']=run('shell','sha256sum',installedPath).split()[0]
assert evidence['installedApkSha256']==evidence['apkSha256'],'Install the current APK before checking rotation'
pid=run('shell','pidof',package).strip();assert pid.isdigit(),'Start the current sample normally before this probe'
evidence['pid']=pid
try:
    if args.force_display_rotation:run('shell','wm','fixed-to-user-rotation','enabled')
    for rotation,size in ([(0,'1080x1920'),(0,'1920x1080'),(0,'1080x1920')] if args.resize_display else [(r,None) for r in [0,1,2,3,0]]):
        if size:run('shell','wm','size',size)
        started=time.monotonic();run('shell','wm','user-rotation','lock',rotation)
        while True:
            display=run('shell','dumpsys','window','displays')
            if 'mDisplayRotation=ROTATION_'+str(rotation*90) in display:break
            if time.monotonic()-started>12:
                (output/'failed.display.txt').write_text(display)
                (output/'failed.window.txt').write_text(run('shell','dumpsys','window'))
                raise TimeoutError('Android did not reach rotation '+str(rotation))
            time.sleep(.25)
        time.sleep(1)
        assert run('shell','pidof',package).strip()==pid,'Player process restarted during rotation'
        log=run('logcat','-d','--pid='+pid)
        records=[json.loads(m) for m in re.findall(r'LIVLIV_EVIDENCE (\{[^\n]+\})',log)]
        assert records and records[-1]['status']=='Ready'
        record=records[-1]
        assert record['scale']==1 and not record['tracking'] and not record['microphone']
        layout=record['layout'];assert layout['visibleButtons']==6 and layout['clippedButtons']==0 and layout['overlappingPairs']==0 and layout['scrollComponents']==0,layout
        if evidence['cases']:assert record['frames']>evidence['cases'][-1]['player']['frames']
        name=f'{len(evidence["cases"])}-rotation-{rotation*90}'
        image=subprocess.check_output([adb,'-s',serial,'exec-out','screencap','-p'],timeout=30)
        assert image[:8]==b'\x89PNG\r\n\x1a\n';width,height=struct.unpack('>II',image[16:24])
        (output/(name+'.png')).write_bytes(image);(output/(name+'.display.txt')).write_text(display)
        evidence['cases'].append({'rotation':rotation*90,'displaySizeOverride':size,'width':width,'height':height,'player':record,'screenshot':name+'.png','sha256':hashlib.sha256(image).hexdigest()})
        if size:assert f'{layout["width"]}x{layout["height"]}'==size,layout
    evidence['result']='RuntimePassed'
except BaseException as error:
    evidence['result']='Failed';evidence['error']=str(error);raise
finally:
    if args.resize_display:run('shell','wm','size',override.group(1) if override else 'reset')
    for k,v in original.items():
        if v=='null':run('shell','settings','delete','system',k)
        else:run('shell','settings','put','system',k,v)
    if args.force_display_rotation:run('shell','wm','fixed-to-user-rotation',fixed)
    evidence['restoredSettings']={k:setting(k) for k in original}
    assert evidence['restoredSettings']==original,'Android rotation settings were not restored'
    evidence['restoredFixedToUserRotation']=run('shell','wm','fixed-to-user-rotation').strip()
    assert evidence['restoredFixedToUserRotation']==fixed,'Android display policy was not restored'
    evidence['resizeDisplay']=args.resize_display;evidence['originalDisplaySize']=sizeBefore;evidence['restoredDisplaySize']=run('shell','wm','size')
    assert evidence['restoredDisplaySize']==sizeBefore,'Android display size was not restored'
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print(json.dumps({'result':evidence['result'],'output':str(output),'visualReview':evidence['visualReview']},indent=2))
