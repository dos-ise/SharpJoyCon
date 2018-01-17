namespace SharpJoyCon
{
  using System;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Runtime.InteropServices;
  using System.Text;

  using log4net;

  /// <summary>
  /// https://github.com/signal11/hidapi
  /// </summary>
  public class HIDapi
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(HIDapi));

    #region Native Methods

    // On windows for system installed: hidapi.dll
    // On linux for system installed: "libhidapi-hidraw" or "libhidapi-libusb"
    // unfortunately there is no way simple to automatically
    // find the library on all platforms becasue of different
    // naming conventions.
    // Just use hidapi and expect users to supply it in same folder as .exe
    public const string DLL_FILE_NAME = "hidapi";

    /// Return Type: int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_init();


    /// Return Type: int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_exit();

    /// Return Type: hid_device_info*
    ///vendor_id: unsigned short
    ///product_id: unsigned short
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hid_enumerate(ushort vendor_id, ushort product_id);

    /// Return Type: void
    ///devs: struct hid_device_info*
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void hid_free_enumeration(IntPtr devs);

    /// Return Type: hid_device*
    ///vendor_id: unsigned short
    ///product_id: unsigned short
    ///serial_number: wchar_t*
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hid_open(ushort vendor_id, ushort product_id, [In] string serial_number = null);


    /// Return Type: hid_device*
    ///path: char*
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr hid_open_path([In] string path);


    /// Return Type: int
    ///device: hid_device*
    ///data: unsigned char*
    ///length: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_write(IntPtr device, [In] byte[] data, uint length);


    /// Return Type: int
    ///dev: hid_device*
    ///data: unsigned char*
    ///length: size_t->unsigned int
    ///milliseconds: int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_read_timeout(IntPtr device, [Out] byte[] buf_data, uint length, int milliseconds);


    /// Return Type: int
    ///device: hid_device*
    ///data: unsigned char*
    ///length: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_read(IntPtr device, [Out] byte[] buf_data, uint length);


    /// Return Type: int
    ///device: hid_device*
    ///nonblock: int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_set_nonblocking(IntPtr device, int nonblock);


    /// Return Type: int
    ///device: hid_device*
    ///data: char*
    ///length: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_send_feature_report(IntPtr device, [In] byte[] data, uint length);


    /// Return Type: int
    ///device: hid_device*
    ///data: unsigned char*
    ///length: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int hid_get_feature_report(IntPtr device, [Out] byte[] buf_data, uint length);


    /// Return Type: void
    ///device: hid_device*
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void hid_close(IntPtr device);


    /// Return Type: int
    ///device: hid_device*
    ///string: wchar_t*
    ///maxlen: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
    public static extern int hid_get_manufacturer_string(IntPtr device, StringBuilder buf_string, uint length);


    /// Return Type: int
    ///device: hid_device*
    ///string: wchar_t*
    ///maxlen: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
    public static extern int hid_get_product_string(IntPtr device, StringBuilder buf_string, uint length);


    /// Return Type: int
    ///device: hid_device*
    ///string: wchar_t*
    ///maxlen: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
    public static extern int hid_get_serial_number_string(IntPtr device, StringBuilder buf_serial, uint maxlen);


    /// Return Type: int
    ///device: hid_device*
    ///string_index: int
    ///string: wchar_t*
    ///maxlen: size_t->unsigned int
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
    public static extern int hid_get_indexed_string(IntPtr device, int string_index, StringBuilder buf_string, uint maxlen);


    /// Return Type: wchar_t*
    ///device: hid_device*
    [DllImport(DLL_FILE_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
    public static extern IntPtr hid_error(IntPtr device);


    [StructLayout(LayoutKind.Sequential)]
    public struct hid_device_info
    {
      /// char*
      [MarshalAs(UnmanagedType.LPStr)]
      public string path;

      /// unsigned short
      public ushort vendor_id;

      /// unsigned short
      public ushort product_id;

      /// wchar_t*
      [MarshalAs(UnmanagedType.LPWStr)]
      public string serial_number;

      /// unsigned short
      public ushort release_number;

      /// wchar_t*
      [MarshalAs(UnmanagedType.LPWStr)]
      public string manufacturer_string;

      /// wchar_t*
      [MarshalAs(UnmanagedType.LPWStr)]
      public string product_string;

      /// unsigned short
      public ushort usage_page;

      /// unsigned short
      public ushort usage;

      /// int
      public int interface_number;

      /// hid_device_info*
      public System.IntPtr next;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct hid_device_
    {

      /// HANDLE->void*
      public IntPtr device_handle;

      /// BOOL->int
      [MarshalAs(UnmanagedType.Bool)]
      public bool blocking;

      /// size_t->unsigned int
      public uint input_report_length;

      /// void*
      public System.IntPtr last_error_str;

      /// DWORD->unsigned int
      public uint last_error_num;

      /// BOOL->int
      [MarshalAs(UnmanagedType.Bool)]
      public bool read_pending;

      /// char*
      [MarshalAs(UnmanagedType.LPStr)]
      public string read_buf;

      /// OVERLAPPED->_OVERLAPPED
      public OVERLAPPED ol;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OVERLAPPED
    {

      /// ULONG_PTR->unsigned int
      public uint Internal;

      /// ULONG_PTR->unsigned int
      public uint InternalHigh;

      /// Anonymous_7416d31a_1ce9_4e50_b1e1_0f2ad25c0196
      public Anonymous_7416d31a_1ce9_4e50_b1e1_0f2ad25c0196 Union1;

      /// HANDLE->void*
      public System.IntPtr hEvent;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Anonymous_7416d31a_1ce9_4e50_b1e1_0f2ad25c0196
    {

      /// Anonymous_ac6e4301_4438_458f_96dd_e86faeeca2a6
      [FieldOffset(0)]
      public Anonymous_ac6e4301_4438_458f_96dd_e86faeeca2a6 Struct1;

      /// PVOID->void*
      [FieldOffset(0)]
      public System.IntPtr Pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Anonymous_ac6e4301_4438_458f_96dd_e86faeeca2a6
    {

      /// DWORD->unsigned int
      public uint Offset;

      /// DWORD->unsigned int
      public uint OffsetHigh;
    }


    #endregion
    //Overlay structure for KNX USB HID reports
    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]
    internal unsafe struct KnxHidReportWithRawAccess
    {
      [FieldOffset(0)]
      public KnxHidReport HidReport;
      [FieldOffset(0)]
      public KnxHidContinuedReport ContinuedHidReport;
      [FieldOffset(0)]
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
      public fixed byte RawFrame[64];
      public byte[] RawData
      {
        get
        {
          byte[] data = new byte[64];
          fixed (byte* dst = data, src = this.RawFrame)
          {
            CopyUnsafeByteArrays(src, dst, 64);
          }
          return data;
        }
        set
        {
          fixed (byte* src = value, dst = this.RawFrame)
          {
            CopyUnsafeByteArrays(src, dst, Math.Min(value.Length, 64));
          }
        }

      }
    };

    //Overlay structure for KNX USB HID reports
    [StructLayout(LayoutKind.Sequential, Size = 64, Pack = 1)]
    internal unsafe struct KnxHidReport
    {
      public byte ReportID; //The ReportID contains the unique identifier for the HID report. This value is fixed to 0x01 for KNX data exchange.
      private byte PacketTypeAndSequenceNumber;
      public byte DataLength;
      public byte ProtocolVersion; //Shall state the revision of transfer protocol
      public byte HeaderLength; //Contains the length of the header in bytes
      private byte BodyLengthHighByte; //The complete protocol body may exceed a single report
      private byte BodyLengthLowByte; //The complete protocol body may exceed a single report
      public byte ProtocolID; //The protocol ID determins the kind of payload
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
      public fixed byte HidBody[56];

      // Accessors which are NOT elements in the struct!
      public byte PacketType
      {
        get { return (byte)(this.PacketTypeAndSequenceNumber & 0xf); }
        set { this.PacketTypeAndSequenceNumber = (byte)((this.PacketTypeAndSequenceNumber & 0xf0) | value); }
      }
      public byte SequenceNumber
      {
        get { return (byte)(this.PacketTypeAndSequenceNumber >> 4); }
        set { this.PacketTypeAndSequenceNumber = (byte)((this.PacketTypeAndSequenceNumber & 0x0f) | (value << 4)); }
      }
      public ushort BodyLength
      {
        get
        {
          return MakeWord(this.BodyLengthHighByte, this.BodyLengthLowByte);
        }
        set
        {
          this.BodyLengthHighByte = (byte)((value >> 8) & 0xff);
          this.BodyLengthLowByte = (byte)(value & 0xff);
        }
      }

      public FeatureReport FeatureReport
      {
        get
        {
          FeatureReportWithRawAccess report = new FeatureReportWithRawAccess();
          fixed (byte* src = this.HidBody)
          {
            CopyUnsafeByteArrays(src, report.RawFrame, 56);
          }
          return report.Feature;
        }
        set
        {
          FeatureReportWithRawAccess report = new FeatureReportWithRawAccess();
          report.Feature = value;
          fixed (byte* dst = this.HidBody)
          {
            CopyUnsafeByteArrays(report.RawFrame, dst, 56);
          }
        }
      }

      public TelegramReport TelegramReport
      {
        get
        {
          TelegramReportWithRawAccess report = new TelegramReportWithRawAccess();
          fixed (byte* src = this.HidBody)
          {
            CopyUnsafeByteArrays(src, report.RawFrame, 56);
          }
          return report.Telegram;
        }
        set
        {
          TelegramReportWithRawAccess report = new TelegramReportWithRawAccess();
          report.Telegram = value;
          fixed (byte* dst = this.HidBody)
          {
            CopyUnsafeByteArrays(report.RawFrame, dst, 56);
          }
        }
      }

    };

    [StructLayout(LayoutKind.Explicit, Size = 56, Pack = 1)]
    internal unsafe struct FeatureReportWithRawAccess
    {
      [FieldOffset(0)]
      public FeatureReport Feature;
      [FieldOffset(0)]
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
      public fixed byte RawFrame[56];
    }

    [StructLayout(LayoutKind.Sequential, Size = 56, Pack = 1)]
    internal unsafe struct FeatureReport
    {
      public byte FeatureService; //The service type identifier for feature telegrams
      private byte ManufacturerCodeHighByte;
      private byte ManufacturerCodeLowByte;
      public byte FeatureIdentifier;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 52)]
      private fixed byte Body[52];

      public ushort ManufacturerCode
      {
        get
        {
          return MakeWord(this.ManufacturerCodeHighByte, this.ManufacturerCodeLowByte);
        }
        set
        {
          this.ManufacturerCodeHighByte = (byte)((value >> 8) & 0xff);
          this.ManufacturerCodeLowByte = (byte)(value & 0xff);
        }
      }
      public byte[] FeatureData
      {
        get
        {
          byte[] data = new byte[52];
          fixed (byte* dst = data, src = this.Body)
          {
            CopyUnsafeByteArrays(src, dst, 52);
          }
          return data;
        }
        set
        {
          fixed (byte* src = value, dst = this.Body)
          {
            CopyUnsafeByteArrays(src, dst, Math.Min(value.Length, 52));
          }
        }
      }
    };

    [StructLayout(LayoutKind.Explicit, Size = 56, Pack = 1)]
    internal unsafe struct TelegramReportWithRawAccess
    {
      [FieldOffset(0)]
      public TelegramReport Telegram;
      [FieldOffset(0)]
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
      public fixed byte RawFrame[56];
    }

    [StructLayout(LayoutKind.Sequential, Size = 56, Pack = 1)]
    internal unsafe struct TelegramReport
    {
      public byte EmiType; //The EMI type describes how KNX data is formated
      private byte ManufacturerCodeHighByte;
      private byte ManufacturerCodeLowByte;
      public byte MessageCode;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 52)]
      private fixed byte RawData[52];

      public ushort ManufacturerCode
      {
        get
        {
          return MakeWord(this.ManufacturerCodeHighByte, this.ManufacturerCodeLowByte);
        }
        set
        {
          this.ManufacturerCodeHighByte = (byte)((value >> 8) & 0xff);
          this.ManufacturerCodeLowByte = (byte)(value & 0xff);
        }
      }
      public byte[] TelegramData
      {
        get
        {
          byte[] data = new byte[52];
          fixed (byte* dst = data, src = this.RawData)
          {
            CopyUnsafeByteArrays(src, dst, 52);
          }
          return data;
        }
        set
        {
          fixed (byte* src = value, dst = this.RawData)
          {
            CopyUnsafeByteArrays(src, dst, Math.Min(value.Length, 52));
          }
        }
      }
    };

    //Overlay structure for KNX USB HID reports
    [StructLayout(LayoutKind.Sequential, Size = 64, Pack = 1)]
    internal unsafe struct KnxHidContinuedReport
    {
      public byte ReportID; //The ReportID contains the unique identifier for the HID report. This value is fixed to 0x01 for KNX data exchange.
      private byte PacketTypeAndSequenceNumber;
      public byte BodyLength;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 61)]
      public fixed byte HidBody[61];

      // Accessors which are NOT elements in the struct!
      public byte PacketType
      {
        get { return (byte)(this.PacketTypeAndSequenceNumber & 0xf); }
        set { this.PacketTypeAndSequenceNumber = (byte)((this.PacketTypeAndSequenceNumber & 0xf0) | value); }
      }
      public byte SequenceNumber
      {
        get { return (byte)(this.PacketTypeAndSequenceNumber >> 4); }
        set { this.PacketTypeAndSequenceNumber = (byte)((this.PacketTypeAndSequenceNumber & 0x0f) | (value << 4)); }
      }
      public byte[] TelegramData
      {
        get
        {
          byte[] result = new byte[61];
          fixed (byte* src = this.HidBody, dst = result)
          {
            CopyUnsafeByteArrays(src, dst, 61);
          }
          return result;
        }
        set
        {
          fixed (byte* dst = this.HidBody, src = value)
          {
            CopyUnsafeByteArrays(src, dst, 61);
          }
        }
      }

    }

    protected static unsafe void CopyUnsafeByteArrays(byte* src, byte* dst, int size)
    {
      for (int i = 0; i < size; ++i)
      {
        *dst = *src;
        ++src;
        ++dst;
      }
    }

    internal static ushort MakeWord(byte b0, byte b1)
    {
      return (ushort)((b0 << 8) | b1);
    }

    public static string PlattformFolder { get; private set; }

    public static string InitializeDll()
    {
      string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      bool useLibUsb = true;
      PlattformFolder = string.Empty;
      switch (Environment.OSVersion.Platform)
      {
        case PlatformID.Unix:
          switch (RuntimeInformation.ProcessArchitecture)
          {
            case Architecture.Arm:
            case Architecture.Arm64:
              PlattformFolder = "Arm";
              PlattformFolder += ".";
              PlattformFolder += useLibUsb ? "libusb" : "hidraw";
              break;
            case Architecture.X64:
            case Architecture.X86:
              ////TODO at the moment we always use libusb. How can we check if we should use libsub or hidraw?
              PlattformFolder = useLibUsb ? "Linux_libusb" : "Linux_hidraw";
              break;
          }
          break;
        case PlatformID.Win32NT:
          PlattformFolder = Environment.Is64BitOperatingSystem ? "x64" : "x86";
          break;
        case PlatformID.MacOSX:
        case PlatformID.Win32S:
        case PlatformID.Win32Windows:
        case PlatformID.WinCE:
        case PlatformID.Xbox:
          throw new NotSupportedException(Environment.OSVersion.Platform + " is not supported by Knx.Falcon.UsbAccess!");
      }

      string dllfile = Directory.GetFiles(currentPath, DLL_FILE_NAME + ".*").SingleOrDefault();
      if (dllfile == null)
      {
        Log.Info("Unpack native dll");
        Stream dll = GetEmbeddedResourceFile(PlattformFolder + "." + DLL_FILE_NAME);
        string fileExtension = Environment.OSVersion.Platform == PlatformID.Unix ? "so" : "dll";
        using (Stream file = File.Create(Path.Combine(currentPath, (DLL_FILE_NAME + "." + fileExtension))))
        {
          dll.CopyTo(file);
          dllfile = Directory.GetFiles(currentPath, DLL_FILE_NAME + ".*").SingleOrDefault();
        }
      }

      if (dllfile != null)
      {
        Log.Info("Found hidapi file: " + dllfile);
      }
      else
      {
        Log.Error("Could not find hidapi file under " + Directory.GetCurrentDirectory());
      }

      return dllfile;
    }

    public static Stream GetEmbeddedResourceFile(string filename)
    {
      Log.Debug("GetEmbeddedResourceFile " + filename);
      var assembly = Assembly.GetExecutingAssembly();
      foreach (var resourceName in assembly.GetManifestResourceNames())
      {
        Log.Debug(resourceName);
      }
      
      Stream resourceStream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(me => me.Contains(filename)));
      return resourceStream;
    }
  }
}
