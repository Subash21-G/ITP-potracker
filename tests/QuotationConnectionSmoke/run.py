from pathlib import Path
import os
import secrets
import subprocess
import tempfile
import time

root = Path(__file__).resolve().parent
env = os.environ.copy()
env['SMOKE_KEY'] = secrets.token_urlsafe(32)
flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
with tempfile.TemporaryDirectory(prefix='quotation-smoke-') as temp:
    url_file = Path(temp) / 'url.txt'
    env['SMOKE_URL_FILE'] = str(url_file)
    with (Path(temp) / 'receiver.log').open('w') as log:
        receiver = subprocess.Popen(['dotnet', str(root / 'Receiver/out/Receiver.dll')], env=env,
            stdout=log, stderr=subprocess.STDOUT, creationflags=flags)
        try:
            deadline = time.monotonic() + 30
            while not url_file.exists():
                if receiver.poll() is not None or time.monotonic() > deadline:
                    raise RuntimeError('Isolated receiver failed to start')
                time.sleep(0.1)
            env['SMOKE_URL'] = url_file.read_text().strip()
            result = subprocess.run(['dotnet', str(root / 'Sender/bin/Debug/net10.0/Sender.dll')], env=env,
                check=True, creationflags=flags, capture_output=True, text=True)
            print(result.stdout.strip())
        finally:
            receiver.terminate()
            receiver.wait(timeout=10)
