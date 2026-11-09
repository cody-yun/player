"""Portable-runner contract tests. Synthetic reports; never a Windows runtime result."""
from pathlib import Path
import contextlib,importlib.util,io,json,subprocess,tempfile,unittest
from unittest.mock import patch,Mock
spec=importlib.util.spec_from_file_location('windows_runner',Path(__file__).with_name('verify-windows-player.py'))
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)

def environment():return {'result':'Passed','platform':'WindowsPlayer','graphics':'Direct3D11'}
def face():
    report=environment();report.update(released=True,sourceFailureHandled=True,restarted=True,expressionLabels=True,failedSourceReleases=1,restartedSourceReleases=1)
    report['states']=[dict(scale=1,model='Aoi',expressionCount=4,expressionLabel='Smile',expressionTranslation='Smile',
                          faceSeen=True,tracking=True,angleZ=-14,frames=100+i,faceStatus='Active') for i in range(10)]
    report['states'][3]['faceSeen']=False
    report['states'][7].update(tracking=False,faceStatus='Failed: Generated camera capture ended')
    return report
def presentation():
    report=environment();report.update({k:True for k in ['pngPoster','webpFilePoster','invalidPoster','latePoster','disposed','supported','contextLostHandled']})
    report.update({k:0 for k in ['supportProbes','targets','materials','posterTextures','maskTextures']});report['jpegPosters']=['baseline','progressive']
    report['states']=[dict(name=n,time=0,scale=1,width=64,height=96,active=300,roundtrip=.5,targetId=i+1,limited=False)
                      for i,n in enumerate(['portrait','reuse','landscape','density','budget','zero','recovered'])]
    report['states'][1]['targetId']=report['states'][0]['targetId'];report['states'][4].update(width=100,height=100,limited=True)
    report['states'][5].update(width=0,height=0)
    report['cameraSurfaces']=[dict(name=n,width=64,height=96,expectedWidth=64,expectedHeight=96,active=3000,time=0,scale=1)
                             for n in ['front','right','left','up','down','orthographic','screen','nested']]
    report['masks']=[dict(name=n,scale=1,storageBytes=128,fullSizeBytes=1024,channels=0 if n=='offscreen' else 2,maskId=mid)
                     for n,mid in zip(['glance','halfblink','combined','reused','larger','smaller','offscreen','recovered'],[1,2,3,3,4,4,4,5])]
    report['textMarks']=[dict(name=n,watermarks=0,coverageTargets=0,coverageMeshes=0,coverageMaterials=0,
                             font='LivLivMarkFont' if n=='default' else 'NanumGothic-Regular',code='too-large' if n=='budget' else None)
                         for n in ['empty','latin','korean','literal','default','retina-empty','retina','budget']]
    return report
def speech():
    report=environment();report.update(inputs=['PCM16 WAV','MP3','float WAV'],hooks=[f'{i}:{e}' for i in range(1,4) for e in ['start','end']],
                                     idle=1,stopped=1,stateEvents=[False,True,False],peak=.1,mouth=.5,scale=1,remainingPieces=0,remainingClips=0)
    return report
def bundle(root):
    path=root/'Player/test.exe';path.parent.mkdir();path.write_bytes(b'opaque unit-test payload, not executable')
    manifest={'schemaVersion':1,'platform':'windows-x64','baseline':'unit-fixture','executable':'Player/test.exe','files':{'Player/test.exe':runner.sha(path)}}
    (root/'bundle-manifest.json').write_text(json.dumps(manifest));return manifest

class RunnerContracts(unittest.TestCase):
    def test_synthetic_report_contracts(self):
        for graphics in ['Direct3D11','Direct3D12']:
            for factory,validate in [(face,runner.validate_report),(presentation,runner.validate_presentation),(speech,runner.validate_speech)]:
                report=factory();report['graphics']=graphics;validate(report)
    def test_rejects_foreign_platform_and_backend(self):
        for key,value in [('platform','OSXPlayer'),('graphics','Null'),('error','unexpected')]:
            report=face();report[key]=value
            with self.assertRaises(ValueError):runner.validate_report(report)
    def test_rejects_missing_metadata_scale_and_nonfinite_rotation(self):
        for key,value in [('expressionCount',0),('expressionTranslation',''),('scale',1.1),('angleZ',float('nan'))]:
            report=face();report['states'][2][key]=value
            with self.assertRaises(ValueError):runner.validate_report(report)
    def test_rejects_no_recovery_or_double_release(self):
        for key,value in [('restarted',False),('failedSourceReleases',2)]:
            report=face();report[key]=value
            with self.assertRaises(ValueError):runner.validate_report(report)
    def test_rejects_presentation_leak_and_wrong_density(self):
        report=presentation();report['materials']=1
        with self.assertRaises(ValueError):runner.validate_presentation(report)
        report=presentation();report['cameraSurfaces'][0]['width']=200
        with self.assertRaises(ValueError):runner.validate_presentation(report)
    def test_rejects_speech_state_or_clip_leak(self):
        for key,value in [('stateEvents',[True,False]),('remainingClips',1),('mouth',0),('mouth',float('nan')),('peak',float('nan'))]:
            report=speech();report[key]=value
            with self.assertRaises(ValueError):runner.validate_speech(report)
    def test_file_integrity_and_unexpected_file(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);bundle(root);runner.check_bundle(root)
            (root/'unexpected.dll').write_bytes(b'x')
            with self.assertRaises(ValueError):runner.check_bundle(root)
    def test_changed_or_missing_payload(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);bundle(root);path=root/'Player/test.exe';path.write_bytes(b'changed')
            with self.assertRaises(ValueError):runner.check_bundle(root)
            path.unlink()
            with self.assertRaises(ValueError):runner.check_bundle(root)
    def test_manifest_path_escape(self):
        for path in ['../outside','/absolute','C:/outside','Player\\outside']:
            with self.assertRaises(ValueError):runner.payload(Path('/unit'),path)
    def test_non_windows_execution_guard(self):
        with patch.object(runner.sys,'platform','darwin'):
            with self.assertRaisesRegex(RuntimeError,'requires Windows'):runner.require_windows()
    def test_check_only_is_never_a_runtime_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);bundle(root);output=io.StringIO()
            with patch.object(runner.sys,'argv',['runner','--bundle',str(root),'--check-only']),contextlib.redirect_stdout(output):
                self.assertEqual(runner.main(),0)
            report=json.loads(output.getvalue());self.assertEqual(report['result'],'BundleVerified');self.assertFalse(report['runtimeExecuted'])
    def test_process_failure_preserves_logs_and_failure_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'bundle';root.mkdir();manifest=bundle(root);output=Path(folder)/'evidence';output.mkdir()
            process=Mock();process.wait.return_value=7
            with patch.object(runner,'require_windows'),patch.object(runner.platform,'platform',return_value='unit-platform'),patch.object(runner.platform,'machine',return_value='unit-machine'),patch.object(runner.subprocess,'Popen',return_value=process):
                report=runner.run_probe(root,manifest,output,1)
            self.assertEqual(report['result'],'Failed');self.assertEqual(report['probes']['face']['exitCode'],7)
            self.assertTrue((output/'evidence.json').exists());self.assertTrue((output/'face/launcher.log').exists())
    def test_timeout_kills_only_the_owned_process(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)/'bundle';root.mkdir();manifest=bundle(root);output=Path(folder)/'evidence';output.mkdir()
            process=Mock();process.wait.side_effect=[subprocess.TimeoutExpired('fixture',1),0]
            with patch.object(runner,'require_windows'),patch.object(runner.platform,'platform',return_value='unit-platform'),patch.object(runner.platform,'machine',return_value='unit-machine'),patch.object(runner.subprocess,'Popen',return_value=process):
                report=runner.run_probe(root,manifest,output,1)
            self.assertEqual(report['result'],'Failed');process.kill.assert_called_once();self.assertIn('timed out',report['error'])

if __name__=='__main__':unittest.main()
