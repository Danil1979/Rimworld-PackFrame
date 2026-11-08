using PackFrame;

internal static class DiagnosticLogChecks
{
    internal static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"packframe-log-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path=Path.Combine(root,"sample.log");
            Check(DiagnosticLogReader.Read(path,5) is {Missing:true,Failed:false},"missing diagnostic log is distinct from read failure");
            Check(!File.Exists(path),"reading a missing diagnostic log does not create it");
            File.WriteAllText(path,"1234567890");
            Check(DiagnosticLogReader.Read(path,5) is {Text:"67890",Missing:false,Failed:false},"diagnostic view retains bounded newest entries");
            File.WriteAllText(path,"");
            Check(DiagnosticLogReader.Read(path,5) is {Text:"",Missing:false,Failed:false},"empty existing diagnostic log remains readable");
            Check(DiagnosticLogReader.Read(root,5) is {Missing:false,Failed:true},"unreadable diagnostic source is not reported as empty");
        }
        finally { Directory.Delete(root,true); }
    }
    private static void Check(bool success,string message)
    {
        if(!success)throw new InvalidOperationException(message);
        Console.WriteLine("PASS: "+message);
    }
}
