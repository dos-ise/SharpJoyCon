using System;
using SharpJoyCon;

namespace SharpJoyConConsoleDemo
{
  class Program
  {
    static void Main(string[] args)
    {
      new JoyconManager().Awake();
    }
  }
}
