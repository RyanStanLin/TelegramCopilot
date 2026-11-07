"""Exercise the built CLI against a disposable API, without external account login."""
import json
import os
import pathlib
import subprocess
import tempfile
from datetime import datetime, timedelta, timezone

assembly = os.environ.get('TEST_CLI_ASSEMBLY', 'src/TelegramCopilot.Cli/bin/Release/net10.0/telegramcopilot.dll')


def run(*args, code=0):
    process = subprocess.run(['dotnet', assembly, *args], capture_output=True, text=True, timeout=30)
    assert process.returncode == code, (args, process.returncode, process.stderr)
    output = process.stdout.strip()
    return json.loads(output) if output.startswith(('{', '[')) else output


assert 'CLI' in run('help')
assert run('health')['status'] in ['Healthy', 'Degraded']
assert 'nativeAvailable' in run('request', 'GET', '/api/health')
with tempfile.TemporaryDirectory(prefix='telegramcopilot-cli-') as directory:
    payload = pathlib.Path(directory) / 'config.json'
    payload.write_text(json.dumps({'name': 'CLI smoke', 'enabled': False,
                                  'settings': {'apiId': 12345, 'apiHash': '0123456789abcdef0123456789abcdef',
                                               'phone': '+886912345678'}}))
    account = run('create', 'accounts', '--file', str(payload))
    assert account['settings']['apiHash'] == '***'
    assert run('get', 'accounts', account['id'])['name'] == 'CLI smoke'
    assert any(item['id'] == account['id'] for item in run('list', 'accounts')['items'])
    account.update(name='CLI changed')
    payload.write_text(json.dumps({key: account[key] for key in ['name', 'enabled', 'settings', 'revision']}))
    updated = run('update', 'accounts', account['id'], '--file', str(payload))
    assert updated['revision'] == 2
    run('delete', 'accounts', account['id'], '--revision', '1', code=1)
    payload.write_text(json.dumps({'name': 'CLI target', 'enabled': False,
                                  'settings': {'accountId': account['id'], 'identifier': '@alice'}}))
    target = run('create', 'targets', '--file', str(payload))
    now = datetime.now(timezone.utc)
    bounds = ['--from', (now - timedelta(days=1)).isoformat(), '--to', now.isoformat()]
    assert run('timeline', target['id'], *bounds)['total'] >= 1
    stats = run('statistics', target['id'], *bounds, '--period', 'year')
    assert stats['totals']['unknownSeconds'] > 0 and stats['totals']['onlineSeconds'] == 0
    output = pathlib.Path(directory) / 'records.csv'
    run('export', target['id'], *bounds, '--output', str(output))
    assert output.read_bytes().startswith(b'\xef\xbb\xbf') and b'Unknown' in output.read_bytes()
    run('delete', 'targets', target['id'], '--revision', '1')
    run('delete', 'accounts', account['id'], '--revision', '2')
print('PASS: CLI help, health, raw request, CRUD, masked secrets, conflict exit code, timeline, statistics and CSV')
