"""BlueStacks launcher smoke with no Unity command-line extras; catches APK default-backend failures."""
from pathlib import Path
import hashlib,json,subprocess,time
root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
serial='127.0.0.1:5555';package='ai.livliv.verification'
output=root/'unity/TestResults/android-startup'/str(time.time_ns());output.mkdir(parents=True)
def run(*args):return subprocess.check_output([adb,'-s',serial,*args],text=True,stderr=subprocess.STDOUT,timeout=40)
subprocess.run([adb,'connect',serial],check=True)
apk=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',str(apk))
run('shell','am','force-stop',package)
command=['shell','am','start','-W','-a','android.intent.action.MAIN','-c','android.intent.category.LAUNCHER','-n',package+'/com.unity3d.player.UnityPlayerGameActivity']
(output/'launch.log').write_text(run(*command))
time.sleep(1);pid=run('shell','pidof',package).strip();assert pid.isdigit(),pid
for _ in range(24):
    assert run('shell','pidof',package).strip()==pid,'Application exited after launcher startup'
    time.sleep(.5)
log=run('logcat','-d','--pid='+pid);(output/'player.log').write_text(log)
assert 'OpenGL ES' in log, 'Default launch did not select OpenGL ES'
assert 'LIVLIV_EVIDENCE' in log and '"status":"Ready"' in log, 'Sample did not become ready'
assert 'Fatal signal' not in log and 'FATAL EXCEPTION' not in log
evidence={'result':'Passed','scope':'BlueStacks normal MAIN/LAUNCHER intent; no Unity flags; 12 seconds alive',
          'pid':pid,'launchCommand':command,'apkBytes':apk.stat().st_size,'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
          'buildSourceSha256':hashlib.sha256((root/'unity/Assets/Verification/Editor/VerificationBuild.cs').read_bytes()).hexdigest()}
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
with (output/'screen.png').open('wb') as file:subprocess.run([adb,'-s',serial,'exec-out','screencap','-p'],stdout=file,check=True)
print(json.dumps({'result':'Passed','output':str(output),'apkSha256':evidence['apkSha256']},indent=2))
