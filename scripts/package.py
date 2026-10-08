from pathlib import Path
import os, shutil, zipfile, hashlib
root = Path(__file__).resolve().parents[1]
output = root / 'dist'
output.mkdir(exist_ok=True)
build = root / 'bin/Release/netstandard2.1'
shutil.copy2(build / 'UTowny.dll', output / 'UTowny.dll')
packages = Path(os.environ.get('NUGET_PACKAGES', str(Path.home()/'.nuget/packages')))
managed = [
 ('microsoft.data.sqlite.core','8.0.10','lib/netstandard2.0/Microsoft.Data.Sqlite.dll'),
 ('sqlitepclraw.core','2.1.6','lib/netstandard2.0/SQLitePCLRaw.core.dll'),
 ('sqlitepclraw.provider.e_sqlite3','2.1.6','lib/netstandard2.0/SQLitePCLRaw.provider.e_sqlite3.dll'),
 ('sqlitepclraw.bundle_e_sqlite3','2.1.6','lib/netstandard2.0/SQLitePCLRaw.batteries_v2.dll'),
]
for rid, native in [('linux-x64','libe_sqlite3.so'),('win-x64','e_sqlite3.dll')]:
 with zipfile.ZipFile(output/f'UTowny-{rid}.zip','w',zipfile.ZIP_DEFLATED) as archive:
  archive.write(output/'UTowny.dll','plugins/UTowny.dll')
  for package,version,relative in managed:
   source=packages/package/version/relative
   if not source.is_file(): raise FileNotFoundError(source)
   archive.write(source,'plugins/'+source.name)
  source=packages/'sqlitepclraw.lib.e_sqlite3/2.1.6/runtimes'/rid/'native'/native
  archive.write(source,'plugins/'+native)
  for name in ['README.md','COMMANDS.md','VERIFICATION.md','permissions.md','config.yaml','translations.yaml']:
   archive.write(root/name,name)
  archive.writestr('BUILD.txt',f"Commit: {os.environ.get('GITHUB_SHA','local')}\nFramework: netstandard2.1\nOpenMod.Unturned: 3.8.10\nUnturned reference: 3.26.1.1\n")
with (output/'SHA256SUMS.txt').open('w') as out:
 for path in sorted(output.iterdir()):
  if path.name!='SHA256SUMS.txt': out.write(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.name+'\n')
