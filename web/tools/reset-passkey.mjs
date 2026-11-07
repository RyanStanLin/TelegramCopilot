import { DatabaseSync } from 'node:sqlite';
import { resolve, join } from 'node:path';
if (!process.argv.includes('--confirm')) {
  console.error('Back up the data first. Run with --confirm to remove the passkey and invalidate every session.');
  process.exit(1);
}
const db = new DatabaseSync(join(resolve(process.env.WEB_DATA_PATH ?? './data'), 'web.db'));
db.exec("BEGIN IMMEDIATE; DELETE FROM passkey; DELETE FROM challenges; DELETE FROM meta WHERE key='session-key'; COMMIT;");
console.log('Passkey removed. The next visitor can register. Keep access private until registration.');
db.close();
