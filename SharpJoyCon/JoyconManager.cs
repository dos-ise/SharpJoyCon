namespace SharpJoyCon
{
  using System.Collections.Generic;
  using System.Runtime.InteropServices;
  using System;

  using log4net;

  public class JoyconManager
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(JoyconManager));

    // Settings accessible via Unity
    public bool EnableIMU = true;
    public bool EnableLocalize = true;

    // Different operating systems either do or don't like the trailing zero
    private const ushort vendor_id = 0x57e;
    private const ushort vendor_id_ = 0x057e;

    private const ushort product_l = 0x2006;
    private const ushort product_r = 0x2007;

    public List<Joycon> ConnectedJoyCons { get; private set; }

    public void ConnectJoyCons()
    {
      HIDapi.InitializeDll();
      ConnectedJoyCons = new List<Joycon>();
     
      HIDapi.hid_init();

      IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
      IntPtr top_ptr = ptr;

      if (ptr == IntPtr.Zero)
      {
        ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
        if (ptr == IntPtr.Zero)
        {
          HIDapi.hid_free_enumeration(ptr);
          Log.Debug("No Joy-Cons found!");
        }
      }
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
          this.ConnectedJoyCons.Add(new Joycon(hid_device, EnableIMU, EnableLocalize & EnableIMU, 0.04f, isLeft));
        }
        ptr = hidDeviceInfo.next;
      }
      HIDapi.hid_free_enumeration(top_ptr);
    }

    public void Start()
    {
      for (int i = 0; i < this.ConnectedJoyCons.Count; ++i)
      {
        Log.Debug(i);
        Joycon jc = this.ConnectedJoyCons[i];
        byte LEDs = 0x0;
        LEDs |= (byte)(0x1 << i);
        jc.Attach(LEDs);
        jc.Begin();
      }
    }

    public void Update()
    {
      foreach (var jc in ConnectedJoyCons)
      {
        jc.Update();
      }
    }

    public void DisconnectJoyCons()
    {
      for (int i = 0; i < this.ConnectedJoyCons.Count; ++i)
      {
        Joycon jc = this.ConnectedJoyCons[i];
        jc.Detach();
      }
    }

    private bool IsJoyCon(ushort product_id)
    {
      return product_id == product_l || product_id == product_r;
    }
  }
}
