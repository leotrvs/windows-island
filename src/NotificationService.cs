using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

namespace WindowsIsland
{
    sealed class IslandNotification
    {
        public string App;
        public string Title;
        public string Body;
    }

    // Shows new Windows notifications in the island. The official listener API needs a signed package,
    // so this reads the local database where Windows itself keeps notifications, read-only, through the
    // SQLite that ships with Windows. Off by default; only notifications that arrive while it runs are
    // shown, and nothing is stored.
    sealed class NotificationService
    {
        const int SQLITE_OK = 0, SQLITE_ROW = 100, SQLITE_OPEN_READONLY = 1;
        const int PollMs = 1500;
        const int MaxPayloadBytes = 64 * 1024;
        const int MaxTextLength = 300;

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2")] static extern int Open(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close")] static extern int Close(IntPtr db);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout")] static extern int BusyTimeout(IntPtr db, int ms);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare16_v2", CharSet = CharSet.Unicode)] static extern int Prepare(IntPtr db, string sql, int bytes, out IntPtr stmt, IntPtr tail);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int64")] static extern int BindInt64(IntPtr stmt, int index, long value);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step")] static extern int Step(IntPtr stmt);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize")] static extern int Finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64")] static extern long ColumnInt64(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_blob")] static extern IntPtr ColumnBlob(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_bytes")] static extern int ColumnBytes(IntPtr stmt, int column);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text16")] static extern IntPtr ColumnText16(IntPtr stmt, int column);

        const string NewerThan =
            "SELECT n.Id, n.Payload, h.PrimaryId FROM Notification n " +
            "JOIN NotificationHandler h ON h.RecordId = n.HandlerId " +
            "WHERE n.Type = 'toast' AND n.Id > ?1 ORDER BY n.Id";
        const string Newest = "SELECT IFNULL(MAX(Id), 0) FROM Notification";

        public event Action<IslandNotification> Arrived;

        readonly Dispatcher dispatcher;
        Timer timer;
        long lastId = -1;
        int polling;

        public NotificationService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        static string DatabasePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Notifications", "wpndatabase.db");
            }
        }

        public bool Running
        {
            get { return timer != null; }
        }

        public void Start()
        {
            if (timer != null) return;
            lastId = -1; // Only what arrives from now on
            timer = new Timer(_ => Poll(), null, 0, PollMs);
        }

        public void Stop()
        {
            timer?.Dispose();
            timer = null;
        }

        void Poll()
        {
            if (Interlocked.Exchange(ref polling, 1) == 1) return;
            IntPtr db = IntPtr.Zero;
            try
            {
                if (!File.Exists(DatabasePath)) return;
                if (Open(Encoding.UTF8.GetBytes(DatabasePath + "\0"), out db, SQLITE_OPEN_READONLY, IntPtr.Zero) != SQLITE_OK) return;
                BusyTimeout(db, 200);

                if (lastId < 0)
                {
                    lastId = QueryNewest(db);
                    return;
                }

                foreach (IslandNotification notification in QueryNewer(db))
                {
                    IslandNotification copy = notification;
                    dispatcher.BeginInvoke(new Action(() => Arrived?.Invoke(copy)));
                }
            }
            catch (Exception)
            {
                // The database is busy or changed shape; tried again on the next tick.
            }
            finally
            {
                if (db != IntPtr.Zero) Close(db);
                Interlocked.Exchange(ref polling, 0);
            }
        }

        static long QueryNewest(IntPtr db)
        {
            IntPtr stmt;
            if (Prepare(db, Newest, -1, out stmt, IntPtr.Zero) != SQLITE_OK) return -1;
            try { return Step(stmt) == SQLITE_ROW ? ColumnInt64(stmt, 0) : -1; }
            finally { Finalize(stmt); }
        }

        List<IslandNotification> QueryNewer(IntPtr db)
        {
            List<IslandNotification> found = new List<IslandNotification>();
            IntPtr stmt;
            if (Prepare(db, NewerThan, -1, out stmt, IntPtr.Zero) != SQLITE_OK) return found;
            try
            {
                BindInt64(stmt, 1, lastId);
                while (Step(stmt) == SQLITE_ROW)
                {
                    lastId = Math.Max(lastId, ColumnInt64(stmt, 0));
                    string payload = ReadPayload(stmt, 1);
                    string handler = Marshal.PtrToStringUni(ColumnText16(stmt, 2)) ?? "";
                    IslandNotification notification = Parse(payload, handler);
                    if (notification != null) found.Add(notification);
                }
            }
            finally
            {
                Finalize(stmt);
            }
            return found;
        }

        static string ReadPayload(IntPtr stmt, int column)
        {
            int length = ColumnBytes(stmt, column);
            if (length <= 0 || length > MaxPayloadBytes) return null; // Real toasts are a few KB
            byte[] bytes = new byte[length];
            Marshal.Copy(ColumnBlob(stmt, column), bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        static IslandNotification Parse(string payload, string handler)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            try
            {
                // Toast text can come from any app or website, so the XML is read with DTDs off and a size cap.
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxPayloadBytes,
                };
                XDocument xml;
                using (StringReader text = new StringReader(payload))
                using (XmlReader reader = XmlReader.Create(text, settings))
                    xml = XDocument.Load(reader);
                List<string> texts = xml.Descendants("text")
                    .Where(t => (string)t.Attribute("placement") != "attribution")
                    .Select(t => t.Value.Trim())
                    .Where(t => t.Length > 0)
                    .ToList();
                if (texts.Count == 0) return null;
                return new IslandNotification
                {
                    App = Shorten(FriendlyName(handler), 60),
                    Title = Shorten(texts[0], MaxTextLength),
                    Body = texts.Count > 1 ? Shorten(string.Join(" ", texts.Skip(1)), MaxTextLength) : "",
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string Shorten(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max);
        }

        // "Microsoft.WindowsStore_8wekyb3d8bbwe!App" -> "WindowsStore"; "com.squirrel.Discord.Discord" -> "Discord".
        static string FriendlyName(string handler)
        {
            string name = handler;
            int bang = name.IndexOf('!');
            if (bang > 0) name = name.Substring(0, bang);
            int underscore = name.IndexOf('_');
            if (underscore > 0) name = name.Substring(0, underscore);
            if (name.Contains('\\')) name = Path.GetFileNameWithoutExtension(name);
            int dot = name.LastIndexOf('.');
            if (dot >= 0 && dot < name.Length - 1) name = name.Substring(dot + 1);
            return name;
        }
    }
}
