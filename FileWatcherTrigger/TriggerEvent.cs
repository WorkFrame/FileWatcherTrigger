using System.Text;

namespace NetEti.FileTools
{
    /// <summary>
    /// Klasse mit diversen Informationen für Ereignisse im LogicalTaskTree.
    /// </summary>
    /// <remarks>
    /// File: TreeEvent.cs
    /// Autor: Erik Nagel
    ///
    /// 11.08.2019 Erik Nagel: erstellt
    /// </remarks>
    public class TriggerEvent
    {
        #region public members

        /// <summary>Der vollständige Pfad der beobachteten Datei.</summary>
        public string FullPath { get; set; }

        /// <summary>Die Art der Änderung der Datei als String.</summary>
        public string ChangeInfo { get; set; }

        /// <summary>Datum und Uhrzeit des Ereignisses</summary>
        public DateTime Timestamp { get; internal set; }

        /// <summary>
        /// Konstruktor: übernimmt und erzeugt diverse Informationen für das TreeEvent.
        /// </summary>
        /// <param name="fullPath">Der vollständige Pfad der beobachteten Datei.</param>
        /// <param name="changeInfo">Die Art der Änderung der Datei als String.</param>
        public TriggerEvent(string fullPath, string changeInfo)
        {
            this.FullPath = fullPath;
            this.ChangeInfo = changeInfo;
            this.Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Überschriebene ToString()-Methode.
        /// </summary>
        /// <returns>String-Repräsentation des TriggerEvents.</returns>
        public override string ToString()
        {
            StringBuilder rtn = new StringBuilder(this.FullPath);
            rtn.Append(", ");
            rtn.Append(this.ChangeInfo);
            rtn.Append(", ");
            rtn.Append(this.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff")); ;
            return rtn.ToString();
        }

        /// <summary>
        /// Vergleicht Dieses Result mit einem übergebenen Result nach Inhalt.
        /// Der Timestamp wird bewusst nicht in den Vergleich einbezogen.
        /// </summary>
        /// <param name="obj">Vergleichs-TriggerEvent.</param>
        /// <returns>True, wenn das übergebene TriggerEvent inhaltlich (ohne Timestamp) gleich diesem TriggerEvent ist.</returns>
        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }
            TriggerEvent res = (TriggerEvent)obj;
            if (res.FullPath == this.FullPath && res.ChangeInfo == this.ChangeInfo)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Erzeugt einen eindeutigen Hashcode für dieses Result.
        /// Der Timestamp wird bewusst nicht in den Vergleich einbezogen.
        /// </summary>
        /// <returns>Ein möglichst eindeutiger Integer für alle Properties einer Instanz zusammen.</returns>
        public override int GetHashCode()
        {
            return (this.ToString()).GetHashCode();
        }

        #endregion public members

    }
}
