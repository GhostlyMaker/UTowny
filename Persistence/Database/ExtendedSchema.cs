using Microsoft.Extensions.Options;using UTowny.Configuration;
namespace UTowny.Persistence.Database;
public sealed class ExtendedSchema
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly IOptions<UTownyOptions> m_Options;
 public ExtendedSchema(IDatabaseConnectionFactory db,IOptions<UTownyOptions> options){m_Db=db;m_Options=options;}
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
  if(s.Number("SELECT version FROM schema_version")<3)
  {
   s.Execute(@"
CREATE TRIGGER player_money_insert BEFORE INSERT ON players WHEN typeof(NEW.balance)<>'integer' OR NEW.balance<0 BEGIN SELECT RAISE(ABORT,'invalid player balance'); END;
CREATE TRIGGER player_money_update BEFORE UPDATE OF balance ON players WHEN typeof(NEW.balance)<>'integer' OR NEW.balance<0 BEGIN SELECT RAISE(ABORT,'invalid player balance'); END;
CREATE TRIGGER town_money_insert BEFORE INSERT ON towns WHEN typeof(NEW.bank_balance)<>'integer' OR NEW.bank_balance<0 BEGIN SELECT RAISE(ABORT,'invalid town balance'); END;
CREATE TRIGGER town_money_update BEFORE UPDATE OF bank_balance ON towns WHEN typeof(NEW.bank_balance)<>'integer' OR NEW.bank_balance<0 BEGIN SELECT RAISE(ABORT,'invalid town balance'); END;
UPDATE schema_version SET version=3;");
  }
  if(s.Number("SELECT version FROM schema_version")<4)
  {
   s.Execute(@"
CREATE TABLE rect_plots(
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 town_id INTEGER NOT NULL REFERENCES towns(id) ON DELETE CASCADE,
 name TEXT NOT NULL COLLATE NOCASE,
 map_id TEXT NOT NULL,
 min_x INTEGER NOT NULL,min_z INTEGER NOT NULL,max_x INTEGER NOT NULL,max_z INTEGER NOT NULL,
 owner_steam64 INTEGER NULL REFERENCES players(steam64) ON DELETE SET NULL,
 for_sale INTEGER NOT NULL DEFAULT 0 CHECK(for_sale IN (0,1)),
 price INTEGER NOT NULL DEFAULT 0 CHECK(typeof(price)='integer' AND price>=0),
 protection_flags INTEGER NOT NULL DEFAULT 0 CHECK(protection_flags BETWEEN 0 AND 31),
 UNIQUE(town_id,name),CHECK(min_x<max_x AND min_z<max_z),CHECK(owner_steam64 IS NULL OR for_sale=0)
);
CREATE INDEX ix_rect_plots_town ON rect_plots(town_id,map_id);
CREATE TRIGGER rect_plot_overlap_insert BEFORE INSERT ON rect_plots
WHEN EXISTS(SELECT 1 FROM rect_plots p WHERE p.map_id=NEW.map_id AND p.min_x<NEW.max_x AND p.max_x>NEW.min_x AND p.min_z<NEW.max_z AND p.max_z>NEW.min_z)
BEGIN SELECT RAISE(ABORT,'overlapping plot'); END;
CREATE TRIGGER rect_plot_overlap_update BEFORE UPDATE OF min_x,min_z,max_x,max_z,map_id ON rect_plots
WHEN EXISTS(SELECT 1 FROM rect_plots p WHERE p.id<>NEW.id AND p.map_id=NEW.map_id AND p.min_x<NEW.max_x AND p.max_x>NEW.min_x AND p.min_z<NEW.max_z AND p.max_z>NEW.min_z)
BEGIN SELECT RAISE(ABORT,'overlapping plot'); END;
CREATE TRIGGER rect_plots_member_removed AFTER DELETE ON town_members
BEGIN UPDATE rect_plots SET owner_steam64=NULL,for_sale=0,price=0,protection_flags=0 WHERE town_id=OLD.town_id AND owner_steam64=OLD.player_steam64; END;
UPDATE schema_version SET version=4;");
  }
  if(s.Number("SELECT version FROM schema_version")<5)
  {
   s.Execute(@"ALTER TABLE rect_plots ADD COLUMN draft INTEGER NOT NULL DEFAULT 0 CHECK(draft IN (0,1) AND (draft=0 OR (owner_steam64 IS NULL AND for_sale=0)));
UPDATE schema_version SET version=5;");
  }
  var fingerprint=m_Options.Value.Claims.GridSizeMeters+":"+m_Options.Value.Claims.MapIdOverride;
  var saved=s.Scalar("SELECT value FROM metadata WHERE key='grid_configuration'") as string;
  if(saved!=null&&saved!=fingerprint&&s.Number("SELECT COUNT(*) FROM claims")>0)throw new InvalidOperationException("Changing grid size/map identity requires migrating or removing existing claims first");
  s.Execute("INSERT INTO metadata VALUES('grid_configuration',$0) ON CONFLICT(key) DO UPDATE SET value=excluded.value",fingerprint);
  s.Commit();
 }
}

