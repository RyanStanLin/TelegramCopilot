#!/usr/bin/env python3
"""Read the configured official Primer MCP over JSON-RPC stdio."""
import json, pathlib, subprocess, sys
root=pathlib.Path(__file__).resolve().parents[1]
out=root/'work'/'primer'; out.mkdir(parents=True,exist_ok=True)
p=subprocess.Popen(['npx','--yes','@primer/mcp@1.1.2'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=open(out/'server.log','a'),text=True,bufsize=1)
seq=0
def call(method,params):
 global seq
 seq+=1
 p.stdin.write(json.dumps({'jsonrpc':'2.0','id':seq,'method':method,'params':params})+'\n'); p.stdin.flush()
 while True:
  line=p.stdout.readline()
  if not line: raise RuntimeError('MCP server exited')
  try: value=json.loads(line)
  except ValueError: continue
  if value.get('id')==seq:
   if 'error' in value: raise RuntimeError(value['error'])
   return value.get('result')
try:
 call('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'TelegramCopilot-doc-research','version':'1.0'}})
 p.stdin.write(json.dumps({'jsonrpc':'2.0','method':'notifications/initialized'})+'\n');p.stdin.flush()
 requests=json.loads(sys.argv[1])
 for req in requests:
  name=req['name']; args=req.get('arguments',{})
  res=call('tools/list',{}) if name=='tools/list' else call('tools/call',{'name':name,'arguments':args})
  slug=req.get('file',name.replace('/','-'))
  (out/(slug+'.json')).write_text(json.dumps(res,ensure_ascii=False,indent=2))
  body='\n'.join(c.get('text','') for c in res.get('content',[])) if 'content' in res else json.dumps(res,ensure_ascii=False,indent=2)
  (out/(slug+'.md')).write_text(body)
  print(f'{slug}: {len(body)} chars, error={res.get("isError",False)}',flush=True)
finally:
 p.terminate()
