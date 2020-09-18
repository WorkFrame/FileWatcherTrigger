# FileWatcherTrigger
Ruft bei Änderung an einer Datei (triggerParameters) eine Action&lt;TriggerEvent> auf, die der öffentlichen Methode Start als Aufrufparameter mitgegeben werden kann.
Zusätzlich kann dem FileWatcherTrigger ein optionaler Zusatzparameter mitgegeben werden, der den FileWatcherTrigger nach Ablauf einer bestimmten Zeit
(MS=Millisekunden, S=Sekunden, M=Minuten, H=Stunden, D=Tage) unabhängig von Ereignissen auf die beobachtete Datei feuern lässt.
Ein typischer Aufrufparameter wäre etwa @".\Testdatei.txt|Initial|S:30|d:\tmp,e:\other". Hier bedeutet Initial, dass der Trigger direkt beim Start einmal feuern soll.
Als letzter Parameter kann eine durch Komma separierte Liste von Verzeichnissen mitgegeben werden, die zusätzlich durchsucht werden sollen.
Der FileWatcherTrigger ist eine Shell um System.IO.FileSystemWatcher. Es werden Fehler abgefangen, die typischerweise bei längerem Betrieb von System.IO.FileSystemWatcher
auftreten können, z.B. 'Watched directory not accessible', so dass der FileWatcherTrigger auch über längere Zeit zuverlässig arbeitet.
Die Parameterübergabe als ein Pipe-separierter String stammt aus einem Produktiv-Umfeld, wo sich dieses Format als praktikabel erwiesen hat. Wer möchte, kann hier gerne
eine passende, klassische Aufruf-Variante hinzufügen (bitte aber die Ein-String-Variante leben lassen).
Siehe auch das enthaltene Projekt FileWatcherTriggerDemo.
