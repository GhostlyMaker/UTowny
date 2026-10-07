namespace UTowny.Persistence.Database;
public sealed class ExtendedSchema
{
 private readonly IDatabaseConnectionFactory m_Db;
 public ExtendedSchema(IDatabaseConnectionFactory db) => m_Db = db;
 public async Task InitializeAsync()
 {
  using var db = await m_Db.OpenAsync(); using var s = new SqlSession(db);
  if (s.Number("SELECT version FROM schema_version") < 2)
  {
   s.Execute("ALTER TABLE towns ADD COLUMN spawn_map TEXT NULL; ALTER TABLE towns ADD COLUMN protection_flags INTEGER NOT NULL DEFAULT 0;");
   s.Execute(@"
CREATE UNIQUE INDEX one_mayor ON town_members(town_id) WHERE role=2;
CREATE TABLE nations(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL COLLATE NOCASE UNIQUE,capital_town_id INTEGER NOT NULL UNIQUE REFERENCES towns(id) ON DELETE CASCADE,created_utc TEXT NOT NULL);
CREATE TABLE nation_members(town_id INTEGER PRIMARY KEY REFERENCES towns(id) ON DELETE CASCADE,nation_id INTEGER NOT NULL REFERENCES nations(id) ON DELETE CASCADE);
CREATE INDEX ix_nation_members_nation ON nation_members(nation_id);
CREATE TABLE nation_invites(nation_id INTEGER NOT NULL REFERENCES nations(id) ON DELETE CASCADE,town_id INTEGER NOT NULL REFERENCES towns(id) ON DELETE CASCADE,expires_utc TEXT NOT NULL,PRIMARY KEY(nation_id,town_id));
CREATE TABLE alliances(a INTEGER NOT NULL REFERENCES nations(id) ON DELETE CASCADE,b INTEGER NOT NULL REFERENCES nations(id) ON DELETE CASCADE,requested_by INTEGER NOT NULL REFERENCES nations(id) ON DELETE CASCADE,status INTEGER NOT NULL CHECK(status IN (0,1)),created_utc TEXT NOT NULL,PRIMARY KEY(a,b),CHECK(a<b));
CREATE TABLE wars(id INTEGER PRIMARY KEY AUTOINCREMENT,a INTEGER NOT NULL REFERENCES towns(id) ON DELETE CASCADE,b INTEGER NOT NULL REFERENCES towns(id) ON DELETE CASCADE,requested_by INTEGER NOT NULL REFERENCES towns(id) ON DELETE CASCADE,requested_utc TEXT NOT NULL,accepted_utc TEXT NULL,start_utc TEXT NULL,end_utc TEXT NULL,status INTEGER NOT NULL CHECK(status BETWEEN 0 AND 4),CHECK(a<b));
CREATE UNIQUE INDEX ix_wars_live ON wars(a,b) WHERE status IN (0,1,2);
CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE shop_trades(id TEXT PRIMARY KEY,player INTEGER NOT NULL REFERENCES players(steam64),kind TEXT NOT NULL,amount INTEGER NOT NULL,asset INTEGER NOT NULL,count INTEGER NOT NULL,status TEXT NOT NULL,created_utc TEXT NOT NULL);
UPDATE schema_version SET version=2;");
  }
  s.Commit();
 }
}
