"""Read-only HTTPS checks by default; optional disabled volume-proof fixture."""
import argparse
import json
import re
import urllib.error
import urllib.request
import uuid
from pathlib import Path


def request(base, method, path, body=None, expected=200):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data, method=method, headers={
        'Content-Type': 'application/json', 'User-Agent': 'TelegramCopilot-DeploymentCheck/1.0'})
    try:
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    assert response.status == expected, (method, path, response.status)
    return payload, response.headers


def json_request(base, method, path, body=None, expected=200):
    payload, headers = request(base, method, path, body, expected)
    assert 'json' in headers.get('Content-Type', '')
    return json.loads(payload)


def verify_live_api(base):
    health = json_request(base, 'GET', '/health/ready')
    assert health['status'] == 'Healthy' and health['nativeAvailable'] is True
    spec = json_request(base, 'GET', '/openapi/v1.json')
    assert {'/health/ready', '/api/accounts', '/api/targets', '/api/deliveries'} <= set(spec['paths'])
    payload, headers = request(base, 'GET', '/api/targets?pageSize=501', expected=400)
    assert 'application/problem+json' in headers.get('Content-Type', '')
    assert json.loads(payload)['status'] == 400
    print('PASS verify_live_api: HTTPS, native readiness, OpenAPI and Problem Details')


def verify_console(base):
    status = json_request(base, 'GET', '/api/auth/status')
    assert isinstance(status['registered'], bool) and status['authenticated'] is False
    json_request(base, 'GET', '/api/backend/health', expected=401)
    html, _ = request(base, 'GET', '/login')
    assert b'TelegramCopilot' in html
    styles = re.findall(r'href="([^"<>]+\.css(?:\?[^"<>]*)?)"', html.decode())
    assert styles, 'No production stylesheets'
    for path in styles:
        if path.startswith('/'):
            css, headers = request(base, 'GET', path.replace('&amp;', '&'))
            assert css and 'text/css' in headers.get('Content-Type', '')
    print('PASS verify_console: HTTPS SSR, static CSS, auth status and unauthenticated proxy rejection')


def record_persistence(base, file):
    assert not file.exists(), 'Existing proof file; verify or clean it up first'
    name = 'Deployment volume proof ' + str(uuid.uuid4())
    created = json_request(base, 'POST', '/api/destinations', {
        'name': name, 'enabled': False,
        'settings': {'serverUrl': 'https://api.day.app', 'deviceKeys': ['disabled-proof-not-a-device']}}, 201)
    assert created['enabled'] is False and created['settings']['deviceKeys'] == ['***']
    file.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    with file.open('x') as stream:
        file.chmod(0o600)
        json.dump({'id': created['id'], 'name': name, 'revision': created['revision']}, stream)
    print('PASS record_persistence: one disabled destination fixture saved; no message is sent')


def verify_persistence(base, file):
    proof = json.loads(file.read_text())
    value = json_request(base, 'GET', '/api/destinations/' + proof['id'])
    assert value['name'] == proof['name'] and value['revision'] == proof['revision']
    assert value['enabled'] is False and value['settings']['deviceKeys'] == ['***']
    print('PASS verify_persistence: same encrypted configuration readable after deployment')


def cleanup_persistence(base, file):
    proof = json.loads(file.read_text())
    request(base, 'DELETE', '/api/destinations/' + proof['id'] + '?revision=' + str(proof['revision']), expected=204)
    print('PASS cleanup_persistence: disabled fixture logically deleted; history retained')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--api', default='https://telegramcopilot-api.ryanl.in')
    parser.add_argument('--web', default='https://telegramcopilot.ryanl.in')
    parser.add_argument('--skip-web', action='store_true')
    parser.add_argument('--proof-file', type=Path, default=Path('.paas/production-proof.json'))
    parser.add_argument('--proof', choices=['record', 'verify', 'cleanup'])
    args = parser.parse_args()
    verify_live_api(args.api)
    if not args.skip_web:
        verify_console(args.web)
    if args.proof:
        {'record': record_persistence, 'verify': verify_persistence, 'cleanup': cleanup_persistence}[args.proof](args.api, args.proof_file)
