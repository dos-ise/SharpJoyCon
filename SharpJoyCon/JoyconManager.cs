namespace SharpJoyCon
{
  using System.Collections.Generic;
  using System.Runtime.InteropServices;
  using System;

  using log4net;

  public class JoyconManager
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(JoyconManager));
    
    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;

    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;
    
    public bool EnableIMU { get; set; } = true;
    public bool EnableLocalize { get; set; } = true;
    public List<JoyCon> ConnectedJoyCons { get; private set; }

    /// <summary>
    /// Searches for JoyCon and initalizes ConnectedJoyCons
    /// </summary>
    /// <returns>Number of connected joycons</returns>
    public int ConnectJoyCons()
    {
      HIDapi.InitializeDll();
      ConnectedJoyCons = new List<JoyCon>();
     
      HIDapi.hid_init();

      IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
      IntPtr top_ptr = ptr;

      while (ptr != IntPtr.Zero)
      {
        var hidDeviceInfo = (HIDapi.hid_device_info)Marshal.PtrToStructure(ptr, typeof(HIDapi.hid_device_info));
        if (IsJoyCon(hidDeviceInfo.product_id))
        {
          bool isLeft = hidDeviceInfo.product_id == product_l;
          Log.Debug(hidDeviceInfo.product_id);
          Log.Debug(isLeft ? "Left Joy-Con connected." : "Right Joy-Con connected.");
          IntPtr hid_device = HIDapi.hid_open_path(hidDeviceInfo.path);
          HIDapi.hid_set_nonblocking(hid_device, 1);
          ConnectedJoyCons.Add(new JoyCon(hid_device, EnableIMU, EnableLocalize & EnableIMU, 0.04f, isLeft));
        }
        ptr = hidDeviceInfo.next;
      }
      HIDapi.hid_free_enumeration(top_ptr);

      if (ConnectedJoyCons.Count == 0)
      {
        Log.Debug("No Joy-Cons found!");
      }

      return ConnectedJoyCons.Count;
    }

    /// <summary>
    /// Attaches JoyCons and sets LEDs on Joy Cons
    /// </summary>
    public void Start()
    {
      foreach (JoyCon jc in ConnectedJoyCons)
      {
        int i = ConnectedJoyCons.IndexOf(jc);
        Log.Debug("Attached JoyCon Nr." + i);
        byte LEDs = 0x0;
        LEDs |= (byte)(0x1 << i);
        jc.Attach(LEDs);
        jc.Begin();
      }
    }

    public void Update()
    {
      ConnectedJoyCons.ForEach(jc => jc.Update());
    }

    public void DisconnectJoyCons()
    {
      ConnectedJoyCons.ForEach(jc => jc.Detach());
    }

    private bool IsJoyCon(ushort product_id)
    {
      return product_id == product_l || product_id == product_r;
    }
  }
}
