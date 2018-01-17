using System;
using SharpJoyCon;
using System.Linq;
using System.Threading;

namespace SharpJoyConConsoleDemo
{
  public class Program
  {
    static void Main(string[] args)
    {
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
  }
}
