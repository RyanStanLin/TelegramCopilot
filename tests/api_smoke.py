"""Run only against a disposable API instance; all external integrations stay disabled."""
import json
import os
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from urllib.parse import urlencode

base = os.environ.get('TEST_API_URL', 'http://127.0.0.1:5080')


def call(method, path, body=None, expected=200):
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(base + path, data=data, method=method,
                                     headers={'Content-Type': 'application/json'})
    try:
        response = urllib.request.urlopen(request, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    assert response.status == expected, (method, path, response.status, response.read().decode())
    if expected >= 400:
        assert "application/problem+json" in response.headers.get("Content-Type", "")
    payload = response.read()
    return json.loads(payload) if payload and 'json' in response.headers.get('Content-Type', '') else payload


resources = []


def roundtrip(kind, settings):
    created = call('POST', '/api/' + kind,
                   {'name': 'Smoke ' + kind, 'enabled': False, 'settings': settings}, 201)
    path = f"/api/{kind}/{created['id']}"
    assert call('GET', path)['revision'] == 1
    assert any(item['id'] == created['id'] for item in call('GET', f'/api/{kind}?pageSize=500')['items'])
    updated = call('PUT', path, {'name': 'Updated ' + kind, 'enabled': False,
                                'settings': created['settings'], 'revision': 1})
    assert updated['revision'] == 2 and updated['name'] == 'Updated ' + kind
    resources.append((kind, updated))
    return updated


assert call('GET', '/health/live')['status'] == 'Alive'
assert len(call('GET', '/openapi/v1.json')['paths']) >= 20
account = roundtrip('accounts', {'apiId': 12345, 'apiHash': '0123456789abcdef0123456789abcdef',
                                  'phone': '+886912345678'})
assert account['settings']['apiHash'] == '***'
target = roundtrip('targets', {'accountId': account['id'], 'identifier': '@alice', 'refreshSeconds': 30})
roundtrip('schedules', {'accountId': account['id'], 'recipient': '@test_bot', 'message': 'Disabled fixture',
                        'frequency': 'Weekly', 'timeZone': 'Asia/Taipei', 'localTime': '07:59:03', 'dayOfWeek': 1})
destination = roundtrip('destinations', {'serverUrl': 'https://api.day.app', 'deviceKeys': ['fake-key'],
                                        'encryption': {'key': '0123456789abcdef', 'iv': '0123456789abcdef', 'mode': 'CBC'}})
assert destination['settings']['deviceKeys'] == ['***']
assert destination['settings']['encryption']['key'] == '***'
roundtrip('rules', {'destinationId': destination['id'], 'targetId': target['id'], 'event': 'Offline',
                   'title': '{NAME}', 'body': '{TIME_OF_ONLINE}', 'subtitle': 'Test', 'group': 'test',
                   'parameters': {'level': 'timeSensitive', 'volume': 5}})
roundtrip('settings', {'timeZone': 'Asia/Taipei', 'heartbeatSeconds': 1,
                      'staleAfterSeconds': 5, 'schedulerGraceSeconds': 60})
call('POST', '/api/settings', {'name': 'duplicate', 'settings': resources[-1][1]['settings']}, 409)
call('DELETE', f"/api/accounts/{account['id']}?revision=2", expected=409)
call('PUT', f"/api/targets/{target['id']}", {'name': 'Stale', 'enabled': False,
                                         'settings': target['settings'], 'revision': 1}, 409)
call('POST', f"/api/accounts/{account['id']}/login", {'type': 'code', 'value': '00000'}, 409)
call('GET', '/api/targets?pageSize=501', expected=400)
now = datetime.now(timezone.utc)
query = urlencode({'from': (now - timedelta(days=1)).isoformat(), 'to': now.isoformat(), 'pageSize': 500})
path = f"/api/targets/{target['id']}"
page = call('GET', path + '/timeline?' + query)
assert page['total'] >= 1 and all(item['state'] == 'Unknown' for item in page['items'])
for period in ['day', 'week', 'month', 'year']:
    stats = call('GET', path + '/statistics?' + query + '&period=' + period)
    assert stats['totals']['onlineSeconds'] == 0 and stats['totals']['unknownSeconds'] > 0
call('GET', path + '/statistics?' + query + '&period=invalid', expected=400)
csv = call('GET', path + '/export.csv?' + query)
assert csv.startswith(b'\xef\xbb\xbf') and b'start_utc' in csv and b'Unknown' in csv
for kind, resource in reversed(resources):
    call('DELETE', f"/api/{kind}/{resource['id']}?revision=2", expected=204)
call('GET', path + '/timeline?' + query)
print('PASS: all six configuration CRUD flows, secret retention, references/revisions, login state, paging, four periods, CSV and archived history')
