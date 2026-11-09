"""Pack the repository's 21 model folders for local verification only, never the UPM distribution."""
from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[2];output=root/'unity/TestResults/model-soak-models';output.mkdir(exist_ok=True)
models=sorted(p.parent for p in (root/'characters').glob('*/model.json'));assert len(models)==21
manifest={}
for model in models:
    archive=output/(model.name+'.livliv');sources={}
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for file in sorted(model.rglob('*')):
            if not file.is_file():continue
            assert not file.is_symlink()
            relative=file.relative_to(model).as_posix();data=file.read_bytes()
            entry=zipfile.ZipInfo(relative,date_time=(2026,1,1,0,0,0));entry.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(entry,data);sources[relative]=hashlib.sha256(data).hexdigest()
    manifest[model.name]={'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'bytes':archive.stat().st_size,'sources':sources}
(output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(str(output))
