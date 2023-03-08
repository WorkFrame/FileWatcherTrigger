using System.Reactive.Linq;
using System.Reactive;
using NetEti.Globals;
using NetEti.ApplicationControl;
using System.Text.RegularExpressions;

namespace NetEti.FileTools
{
    /// <summary>
    /// Ruft bei Änderung an einer Datei(triggerParameters) eine Action&lt;TriggerEvent> auf,
    /// die der öffentlichen Methode Start als Aufrufparameter mitgegeben werden kann.
    /// Zusätzlich kann dem FileWatcherTrigger ein optionaler Zusatzparameter mitgegeben werden,
    /// der den FileWatcherTrigger nach Ablauf einer bestimmten Zeit
    /// (MS= Millisekunden, S= Sekunden, M= Minuten, H= Stunden, D= Tage) unabhängig von Ereignissen
    /// auf die beobachtete Datei feuern lässt.
    /// Ein typischer Aufrufparameter wäre etwa @".\Testdatei.txt|Initial|S:30|d:\tmp,e:\other".
    /// Hier bedeutet Initial, dass der Trigger direkt beim Start einmal feuern soll.
    /// Als letzter Parameter kann eine durch Komma separierte Liste von Verzeichnissen mitgegeben
    /// werden, die zusätzlich durchsucht werden sollen.
    /// Der FileWatcherTrigger ist eine Shell um System.IO.FileSystemWatcher.
    /// Es werden Fehler abgefangen, die typischerweise bei längerem Betrieb von System.IO.FileSystemWatcher
    /// auftreten können, z.B. 'Watched directory not accessible', so dass der FileWatcherTrigger auch über
    /// längere Zeit zuverlässig arbeitet.
    /// </summary>
    /// <remarks>
    /// File: FileWatcherTrigger.cs
    /// Autor: Erik Nagel
    ///
    /// 11.08.2019 Erik Nagel: erstellt.
    /// </remarks>
    public class FileWatcherTrigger : IDisposable
    {
        #region public members

        #region IDisposable Member

        private bool _disposed = false;

        /// <summary>
        /// Öffentliche Methode zum Aufräumen.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Abschlussarbeiten, ggf. Timer zurücksetzen.
        /// </summary>
        /// <param name="disposing">False, wenn vom eigenen Destruktor aufgerufen.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    // Aufräumarbeiten durchführen und dann beenden.
                    try
                    {
                        this.DisposeFileSystemWatchers();
                    }
                    catch { }
                }
                this._disposed = true;
            }
        }

        private void DisposeFileSystemWatchers()
        {
            if (this._fileSystemWatchers != null)
            {
                lock (FileWatcherTrigger._lockMe)
                {
                    foreach (FileSystemWatcher fileSystemWatcher in this._fileSystemWatchers.Keys)
                    {
                        fileSystemWatcher?.Dispose();
                    }
                }
            }
            this._fileSystemWatchers?.Clear();
        }

        /// <summary>
        /// Destruktor
        /// </summary>
        ~FileWatcherTrigger()
        {
            this.Dispose(false);
        }

        #endregion IDisposable Member

        /// <summary>
        /// Ein String mit Trigger-Eigenschaften zu Logging-Zwecken.
        /// </summary>
        public string TriggerInfo {
            get
            {
                string info = this._triggerController?? " " + "beobachtet ";
                string delimiter = "";
                if (this._fileSystemWatchers != null)
                {
                    foreach (FileSystemWatcherTriggerControl triggerControl
                      in DictionaryThreadSafeCopy<FileSystemWatcher, FileSystemWatcherTriggerControl>
                        .GetDictionaryValuesThreadSafeCopy(this._fileSystemWatchers))
                    {
                        if (triggerControl != null)
                        {
                            info = info + delimiter + Path.Combine(triggerControl.WatchedDirectory, triggerControl.WatchedFileName);
                            delimiter = " oder ";
                        }
                    }
                }
                if (this._eventTimer != null && this._eventTimer.Enabled)
                {
                    info = info + " oder: " + this._nextTimerStart.ToString();
                }
                return info;
            }
            set
            {
            }

        }

        /// <summary>
        /// Startet den Trigger und übergibt eine Callback-Action, die aufgerufen wird,
        /// wenn sich die beobachtete Datei ändert. Diese Action liefert ein TriggerEvent
        /// zurück, welches Zusatzinformationen über das auslösende Ereignis enthält.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger aufruft.</param>
        /// <param name="triggerParameters">Pfad der zu beobachtenden Datei.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        /// <returns>True, wenn der Trigger durch diesen Aufruf tatsächlich gestartet wurde.</returns>
        public bool Start(object? triggerController, object triggerParameters, Action<TriggerEvent> triggerIt)
        {
            this._triggerController = (triggerController ?? "").ToString();

            string triggerParametersString = triggerParameters.ToString() ?? "";

            this._eventTimer = null;
            this._lastTimerStart = DateTime.MinValue;
            this._nextTimerStart = DateTime.MinValue;
            this._textPattern = @"(?:MS|S|M|H|D):\d+";
            this._compiledPattern = new Regex(_textPattern);

            MatchCollection alleTreffer;
            alleTreffer = _compiledPattern.Matches(triggerParametersString);
            this._timerInterval = 0;
            if (alleTreffer.Count > 0)
            {
                string subKey = alleTreffer[0].Groups[0].Value;
                triggerParametersString = triggerParametersString.Replace(subKey, "");
                switch (subKey.Split(':')[0])
                {
                    case "MS": this._timerInterval = Convert.ToInt32(subKey.Split(':')[1]); break;
                    case "S": this._timerInterval = Convert.ToInt32(subKey.Split(':')[1]) * 1000; break; ;
                    case "M": this._timerInterval = Convert.ToInt32(subKey.Split(':')[1]) * 1000 * 60; break; ;
                    case "H": this._timerInterval = Convert.ToInt32(subKey.Split(':')[1]) * 1000 * 60 * 60; break; ;
                    case "D": this._timerInterval = Convert.ToInt32(subKey.Split(':')[1]) * 1000 * 60 * 60 * 24; break; ;
                    default:
                        throw new ArgumentException("Falsche Einheit, zulässig sind: MS=Millisekunden, S=Sekunden, M=Minuten, H=Stunden, D=Tage.");
                }
                this._eventTimer = new System.Timers.Timer(this._timerInterval);
                this._eventTimer.Elapsed += new System.Timers.ElapsedEventHandler(eventTimer_Elapsed);
                this._eventTimer.Stop();
            }

            string[] para = (triggerParametersString + "|").Split('|');
            this._initialFire = false;
            string? firstValidFile = null;
            this._fileName = "";
            for (int i = 0; i < para.Count(); i++)
            {
                string paraString = para[i].Trim();
                if (paraString != "")
                {
                    if (paraString.ToUpper().StartsWith("INITIAL"))
                    {
                        this._initialFire = true;
                    }
                    else
                    {
                        if (firstValidFile == null)
                        {
                            if (String.IsNullOrEmpty(_fileName)) // der erste Parameter ist inclusive Dateiname
                            {
                                _fileName = Path.GetFileName(paraString);
                                paraString = Path.GetDirectoryName(paraString) ?? "";
                            }
                            foreach (string path in paraString.Split(','))
                            {
                                string workerPath = path.Trim(' ');
                                if (File.Exists(Path.Combine(workerPath, _fileName)))
                                {
                                    firstValidFile = Path.Combine(workerPath, _fileName);
                                    this._validDirectories.Clear();
                                    this._validDirectories.Add(Path.GetDirectoryName(firstValidFile) ?? ".");
                                    break;
                                }
                                if (Directory.Exists(workerPath))
                                {
                                    this._validDirectories.Add(workerPath);
                                }
                            }
                        }
                    }
                }
            }
            if (this._validDirectories.Count == 0)
            {
                throw new DirectoryNotFoundException(String.Format("Es wurde kein gültiges Verzeichnis gefunden ({0}).", triggerParameters.ToString()));
            }
            this._triggerIt += triggerIt;
            if (this._eventTimer != null)
            {
                this._lastTimerStart = DateTime.Now;
                this._nextTimerStart = this._lastTimerStart.AddMilliseconds(this._timerInterval);
                this._eventTimer.Start();
            }
            return this.setupTriggers();
        }

        /// <summary>
        /// Stoppt den Trigger.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger aufruft.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        public void Stop(object triggerController, Action<TriggerEvent> triggerIt)
        {
            if (this._eventTimer != null)
            {
                this._eventTimer.Stop();
            }
            foreach (FileSystemWatcherTriggerControl triggerControl
              in DictionaryThreadSafeCopy<FileSystemWatcher, FileSystemWatcherTriggerControl>
                .GetDictionaryValuesThreadSafeCopy(this._fileSystemWatchers))
            {
                if (triggerControl.WatcherTerminator != null)
                {
                    triggerControl.WatcherTerminator.Cancel();
                }
            }
            this._triggerIt -= triggerIt;
            this.Log("Watcher stopped!");
        }

        /// <summary>
        /// Konstruktor - initialisiert die Liste von FileWatchern.
        /// </summary>
        public FileWatcherTrigger()
        {
            this._validDirectories = new List<string>();
            this._fileSystemWatchers = new Dictionary<FileSystemWatcher, FileSystemWatcherTriggerControl>();
            this._fileName = "";
        }

        #endregion public members

        #region protected members

        /// <summary>
        /// Löst das Trigger-Event aus.
        /// </summary>
        /// <param name="fileSystemWatcher">Der beobachtende FileSystemWatcher.</param>
        /// <param name="ep">EventPattern, enthält EventArgs und dort Informationen über die beobachtete Datei.</param>
        protected void OnTriggerFired(FileSystemWatcher fileSystemWatcher, EventPattern<FileSystemEventArgs> ep)
        {
            string info = fileSystemWatcher.Path ?? "";

            this.Log("OnTriggerFired: " + info);
            if (this._eventTimer != null)
            {
                this._eventTimer.Stop();
            }
            try
            {
                if (fileSystemWatcher != null)
                {
                    fileSystemWatcher.EnableRaisingEvents = false;
                }
                Thread.Sleep(300); // Doppel-Events aussitzen. 15.07.2022 Nagel+- hierhin verschoben, damit evetuell
                                   // die Dateisystem-Operation noch abgeschlossen werden kann, bevor der Trigger feuert.
                if (this._triggerIt != null)
                {
                    this._triggerIt(new TriggerEvent(ep.EventArgs.FullPath, ep.EventArgs.ChangeType.ToString()));
                }
                else
                {
                    this.Log("OnTriggerFired this._triggerIt == null");
                }
                // 15.07.2022 Nagel+- auskommentiert: Thread.Sleep(300); // Doppel-Events aussitzen.
                if (fileSystemWatcher != null)
                {
                    fileSystemWatcher.EnableRaisingEvents = true;
                }
                if (this._eventTimer != null)
                {
                    this._lastTimerStart = DateTime.Now;
                    this._nextTimerStart = this._lastTimerStart.AddMilliseconds(this._timerInterval);
                    this._eventTimer.Start();
                }
            }
            catch (Exception ex)
            {
                this.Log(String.Format("OnTriggerFired Exception: {0}", ex.Message));
                NotAccessibleError(fileSystemWatcher, new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// Wird ausgeführt, wenn im FileSystemWatcher eine Exception aufgetreten ist, z.B.
        /// Error: Watched directory not accessible at 21.06.2016 18:16:29
        /// Nicht genügend Systemressourcen, um den angeforderten Dienst auszuführen
        /// </summary>
        /// <param name="source">Der FileSystemWatcher</param>
        /// <param name="e">Zusatzinformationen zum Fehler (enthält die Exception).</param>
        protected void OnError(object source, ErrorEventArgs e)
        {
            if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                this.Log("Error: File System Watcher internal buffer overflow at "
                  + DateTime.Now + Environment.NewLine + e.GetException().Message);
            }
            else
            {
                this.Log("Error: Watched directory not accessible at "
                  + DateTime.Now + Environment.NewLine + e.GetException().Message);
            }
            NotAccessibleError((FileSystemWatcher)source, e);
        }

        #endregion protected members

        #region private members

        private Dictionary<FileSystemWatcher, FileSystemWatcherTriggerControl> _fileSystemWatchers;
        private List<string> _validDirectories;
        private static object _lockMe = new object();
        private bool _initialFire;
        private string _fileName;

        private System.Timers.Timer? _eventTimer;
        private int _timerInterval;
        private string? _textPattern;
        private Regex? _compiledPattern;
        private DateTime _lastTimerStart;
        private DateTime _nextTimerStart;
        private string? _triggerController;

        /// <summary>
        /// Wird ausgelöst, wenn das Trigger-Ereignis (z.B. Dateiänderung) eintritt. 
        /// </summary>
        private event Action<TriggerEvent>? _triggerIt;

        private class FileSystemWatcherTriggerControl
        {
            public IObservable<EventPattern<FileSystemEventArgs>>? WatcherTask { get; set; }
            public CancellationTokenSource? WatcherTerminator { get; set; }
            public string WatchedDirectory { get; set; } = "";
            public string WatchedFileName { get; set; } = "";
        }

        private bool setupTriggers()
        {
            lock (FileWatcherTrigger._lockMe)
            {
                foreach (string watchedDirectory in EnumerableThreadSafeCopy<string>
                          .GetEnumerableThreadSafeCopy(this._validDirectories))
                {
                    FileSystemWatcher? fileSystemWatcher = null;
                    FileSystemWatcherTriggerControl? control = null;
                    fileSystemWatcher
                      = new FileSystemWatcher
                      {
                          Path = watchedDirectory,
                          NotifyFilter = NotifyFilters.LastWrite,
                          Filter = this._fileName,
                          IncludeSubdirectories = false,
                          EnableRaisingEvents = false
                      };
                    fileSystemWatcher.Error += new ErrorEventHandler(OnError);
                    IObservable<EventPattern<FileSystemEventArgs>> watcherTask =
                      Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => fileSystemWatcher.Changed += h,
                        h => fileSystemWatcher.Changed -= h);
                    CancellationTokenSource watcherTerminator = new CancellationTokenSource();
                    CancellationToken token = watcherTerminator.Token;
                    control = new FileSystemWatcherTriggerControl()
                    {
                        WatcherTask = watcherTask,
                        WatcherTerminator = watcherTerminator,
                        WatchedDirectory = watchedDirectory,
                        WatchedFileName = this._fileName
                    };
                    this._fileSystemWatchers.Add(fileSystemWatcher, control);
                    token.Register(() => cancelNotification());
                    token.Register(watcherTask.Subscribe(ep => this.OnTriggerFired(fileSystemWatcher, ep)).Dispose);
                    fileSystemWatcher.EnableRaisingEvents = true;
                }
                if (this._initialFire && this._fileSystemWatchers.Count > 0)
                {
                    // TODO: Fehlerquelle beheben - this._fileSystemWatchers war leer
                    this.OnTriggerFired(this._fileSystemWatchers.First().Key, new EventPattern<FileSystemEventArgs>(this,
                      new FileSystemEventArgs(WatcherChangeTypes.Changed,
                        this._fileSystemWatchers.First().Value.WatchedDirectory,
                        this._fileSystemWatchers.First().Value.WatchedFileName)));
                }
            }
            this.Log("Watcher started");
            return true;
        }

        private void eventTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            this.OnTriggerFired(this._fileSystemWatchers.First().Key, new EventPattern<FileSystemEventArgs>(this,
              new FileSystemEventArgs(WatcherChangeTypes.Changed,
                this._fileSystemWatchers.First().Value.WatchedDirectory,
                this._fileSystemWatchers.First().Value.WatchedFileName)));
        }

        // Informiert über den Abbruch der Verarbeitung.
        private void cancelNotification()
        {
            this.Log("cancelNotification!");
        }

        private void NotAccessibleError(FileSystemWatcher source, ErrorEventArgs e)
        {
            this.Log("stopping triggers!");
            foreach (FileSystemWatcherTriggerControl triggerControl
              in DictionaryThreadSafeCopy<FileSystemWatcher, FileSystemWatcherTriggerControl>
                .GetDictionaryValuesThreadSafeCopy(this._fileSystemWatchers))
            {
                if (triggerControl.WatcherTerminator != null)
                {
                    triggerControl.WatcherTerminator.Cancel();
                    Thread.Sleep(100);
                }
            }
            this.DisposeFileSystemWatchers();
            this.Log("restarting triggers!");
            this.setupTriggers();
        }

        private void Log(string message)
        {
            InfoController.Say(String.Format($"{this.TriggerInfo} {message}"));
        }

        #endregion private members

    }
}
