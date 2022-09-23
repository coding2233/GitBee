using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.App.Service
{
    public interface IDatabaseService
    {
        LiteDatabase GetLiteDb(string name);

        void ReleaseDb(string name);
    }
    public class DatabaseService : IDatabaseService
    {
        private Dictionary<string, LiteDatabase> m_dbs = new Dictionary<string, LiteDatabase>();
        public LiteDatabase GetLiteDb(string name)
        {
            LiteDatabase database = null;
            if (!m_dbs.TryGetValue(name, out database))
            {
                string dbPath = Path.Combine(Application.UserPath, $".{name}.db");
                database = new LiteDatabase(dbPath);
                m_dbs.Add(name, database);
            }
            return database;
        }

        public void ReleaseDb(string name)
        {
            if (m_dbs.TryGetValue(name, out LiteDatabase database))
            {
                m_dbs.Remove(name);

                database.Dispose();
                database = null;
            }
        }
    }
}
