using System;
using SharpJoyCon;
using System.Linq;
using System.Threading;

namespace SharpJoyConConsoleDemo
{
  using System.IO;
  using System.Reflection;
  using System.Text;
  using System.Xml;
  using System.Xml.Linq;

  using log4net;
  using log4net.Config;

  public class Program
  {
    static void Main(string[] args)
    {
      SetupLog4Net();
      var manager = new JoyconManager();
      manager.ConnectJoyCons();
      manager.Start();

      var firstJoyCon = manager.ConnectedJoyCons.FirstOrDefault();
      while (true)
      {
        manager.Update();
        Thread.Sleep(500);
        foreach (Joycon.Button buttonType in Enum.GetValues(typeof(Joycon.Button)))
        {
          var isPressed = firstJoyCon.GetButton(buttonType);
          Console.WriteLine(buttonType + (isPressed ? "isPressed" : "notPressed"));
        }
      }

      manager.DisconnectJoyCons();
    }

    private static void SetupLog4Net()
    {
      var assembly = Assembly.GetExecutingAssembly();
      var logConfigName = assembly.GetManifestResourceNames().SingleOrDefault(me => me.Contains("log4net"));
      if (logConfigName != null)
      {
        Stream resourceStream = assembly.GetManifestResourceStream(logConfigName);
        using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
        {
          string logConfig = reader.ReadToEnd();
          var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
          XmlConfigurator.Configure(logRepository, ToXmlElement(XElement.Parse(logConfig)));
        }
      }
    }

    private static XmlElement ToXmlElement(XElement el)
    {
      var doc = new XmlDocument();
      doc.Load(el.CreateReader());
      return doc.DocumentElement;
    }
  }
}
