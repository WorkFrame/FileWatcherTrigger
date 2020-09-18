using System;
using NetEti.ApplicationControl;
using NetEti.FileTools;

namespace NetEti.DemoApplications
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string logFilePathName = "FileWatcherTriggerDemo.log";
            Logger logger = new Logger(logFilePathName, "", false);
            InfoController.GetInfoSource().RegisterInfoReceiver(logger, InfoTypes.Collection2InfoTypeArray(InfoTypes.All));

            string triggerParameters = @".\Testdatei.txt|Initial|S:30|d:\tmp,e:\other";
            FileWatcherTrigger trigger = new FileWatcherTrigger();
            Console.WriteLine("trigger.Start " + triggerParameters);
            trigger.Start(null, triggerParameters, trigger_TriggerIt);

            Console.WriteLine("stop trigger mit enter");
            Console.ReadLine();

            // Aufräumarbeiten durchführen und dann beenden.
            trigger?.Dispose();
            Console.WriteLine("Trigger stopped");
            Console.ReadLine();
            logger.Dispose();
        }

        private static void trigger_TriggerIt(TriggerEvent source)
        {
            Console.WriteLine("{0:HH:mm:ss} Trigger feuert: {1}",
                DateTime.Now, source.FullPath);
        }
    }
}
