namespace SharpJoyCon
{
  using System.Collections.Generic;
  using System;
  using System.Numerics;
  using System.Threading;

  using log4net;

  public class JoyCon
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(JoyCon));
    public bool isLeft;

    public ConnectionState currentConnectionState;

    private bool[] buttons = new bool[13];

    private float[] stick = { 0, 0 };
    private IntPtr handle;
    byte[] default_buf = { 0x0, 0x1, 0x40, 0x40, 0x0, 0x1, 0x40, 0x40 };
    private byte[] stick_raw = { 0, 0, 0 };

    private UInt16[] stick_cal = { 0, 0, 0, 0, 0, 0 };
    private UInt16 deadzone;
    private UInt16[] stick_precal = { 0, 0 };

    private bool stop_polling = false;
    private int timestamp;
    private bool first_imu_packet = true;
    private bool imu_enabled = false;
    private Int16[] acc_r = { 0, 0, 0 };
    private Vector3 acc_g;
    private Int16[] gyr_r = { 0, 0, 0 };
    private Int16[] gyr_neutral = { 0, 0, 0 };
    private Vector3 gyr_g;
    private bool do_localize;
    private float filterweight;
    private const uint report_len = 49;



    private Queue<Report> reports = new Queue<Report>();

    private Rumble rumble_obj;

    private byte global_count = 0;

    public JoyCon(IntPtr handle_, bool imu, bool localize, float alpha, bool left)
    {
      handle = handle_;
      imu_enabled = imu;
      do_localize = localize;
      rumble_obj = new Rumble(160, 320, 0);
      filterweight = alpha;
      isLeft = left;
    }

    public void DebugPrint(string s)
    {
      Log.Debug(s);
    }

    public bool GetButton(Button b)
    {
      return buttons[(int)b];
    }

    public float[] GetStick()
    {
      return stick;
    }

    public Vector3 GetGyro()
    {
      return gyr_g;
    }

    public Vector3 GetAccel()
    {
      return acc_g;
    }

    public Quaternion GetVector()
    {
      var zAxis = -(new Vector3(j_b.Z, i_b.Z, k_b.Z));
      var xAxis = new Vector3(j_b.X, i_b.X, k_b.X);
      //TODO DOS
      return new Quaternion();
      //return Quaternion.LookRotation(xAxis, zAxis));
    }

    internal int Attach(byte leds_ = 0x0)
    {
      currentConnectionState = ConnectionState.ATTACHED;
      byte[] a = { 0x0 };
      // Input report mode
      Subcommand(0x3, new byte[] { 0x3f }, 1, false);
      a[0] = 0x1;
      dump_calibration_data();
      // Connect
      a[0] = 0x01;
      Subcommand(0x1, a, 1);
      a[0] = 0x02;
      Subcommand(0x1, a, 1);
      a[0] = 0x03;
      Subcommand(0x1, a, 1);
      a[0] = leds_;
      Subcommand(0x30, a, 1);
      Subcommand(0x40, new byte[] { (imu_enabled ? (byte)0x1 : (byte)0x0) }, 1, true);
      Subcommand(0x3, new byte[] { 0x30 }, 1, true);
      Subcommand(0x48, new byte[] { 0x1 }, 1, true);
      DebugPrint("Done with init.");
      return 0;
    }

    public void SetFilterCoeff(float a)
    {
      filterweight = a;
    }

    public void Detach()
    {
      stop_polling = true;
      PrintArray(max, format: "Max {0:S}");
      PrintArray(sum, format: "Sum {0:S}");
      if (this.currentConnectionState > ConnectionState.NO_JOYCONS)
      {
        Subcommand(0x30, new byte[] { 0x0 }, 1);
        Subcommand(0x40, new byte[] { 0x0 }, 1);
        Subcommand(0x48, new byte[] { 0x0 }, 1);
        Subcommand(0x3, new byte[] { 0x3f }, 1);
      }
      if (this.currentConnectionState > ConnectionState.DROPPED)
      {
        HIDapi.hid_close(handle);
      }
      this.currentConnectionState = ConnectionState.NOT_ATTACHED;
    }

    private byte ts_en;

    private byte ts_de;

    private System.DateTime ts_prev;

    private int ReceiveRaw()
    {
      if (handle == IntPtr.Zero) return -2;
      HIDapi.hid_set_nonblocking(handle, 0);
      byte[] buffer = new byte[report_len];
      int bytesRead = HIDapi.hid_read(handle, buffer, report_len);
      if (bytesRead > 0)
      {
        lock (reports)
        {
          reports.Enqueue(new Report(buffer));
        }

        if (ts_en == buffer[1])
        {
          DebugPrint(string.Format("Duplicate timestamp enqueued. TS: {0:X2}", ts_en));
        }

        ts_en = buffer[1];
        DebugPrint(string.Format("Enqueue. Bytes read: {0:D}. Timestamp: {1:X2}", bytesRead, buffer[1]));
      }
      return bytesRead;
    }

    private Thread PollThreadObj;

    private void Poll()
    {
      int attempts = 0;
      while (!stop_polling & this.currentConnectionState > ConnectionState.NO_JOYCONS)
      {
        SendRumble(rumble_obj.GetData());
        int a = ReceiveRaw();

        if (a > 0)
        {
          this.currentConnectionState = ConnectionState.IMU_DATA_OK;
          attempts = 0;
        }
        else if (attempts > 1000)
        {
          this.currentConnectionState = ConnectionState.DROPPED;
          DebugPrint("Connection lost. Is the Joy-Con connected?");
          break;
        }
        else
        {
          DebugPrint("Pause 5ms");
          Thread.Sleep((Int32)5);
        }
        ++attempts;
      }
      DebugPrint("End poll loop.");
    }

    float[] max = { 0, 0, 0 };
    float[] sum = { 0, 0, 0 };

    public void Update()
    {
      if (this.currentConnectionState > ConnectionState.NO_JOYCONS)
      {
        byte[] report_buf = null;
        while (reports.Count > 0)
        {
          Report rep;
          lock (reports)
          {
            rep = reports.Dequeue();
            report_buf = rep.Data;
          }
          if (imu_enabled)
          {
            if (do_localize)
            {
              ProcessIMU(report_buf);
            }
            else
            {
              ExtractIMUValues(report_buf);
            }
          }
          if (ts_de == report_buf[1])
          {
            DebugPrint(string.Format("Duplicate timestamp dequeued. TS: {0:X2}", ts_de));
          }
          ts_de = report_buf[1];
          DebugPrint(
            string.Format(
              "Dequeue. Queue length: {0:d}. Packet ID: {1:X2}. Timestamp: {2:X2}. Lag to dequeue: {3:s}. Lag between packets (expect 15ms): {4:s}",
              reports.Count,
              report_buf[0],
              report_buf[1],
              DateTime.Now.Subtract(rep.GetTime),
              rep.GetTime.Subtract(ts_prev)));
          ts_prev = rep.GetTime;
        }

        ProcessButtonsAndStick(report_buf);

      }
    }

    private void ProcessButtonsAndStick(byte[] report_buf)
    {
      stick_raw[0] = report_buf[6 + (isLeft ? 0 : 3)];
      stick_raw[1] = report_buf[7 + (isLeft ? 0 : 3)];
      stick_raw[2] = report_buf[8 + (isLeft ? 0 : 3)];

      stick_precal[0] = (UInt16)(stick_raw[0] | ((stick_raw[1] & 0xf) << 8));
      stick_precal[1] = (UInt16)((stick_raw[1] >> 4) | (stick_raw[2] << 4));
      stick = CenterSticks(stick_precal);

      lock (buttons)
      {
        buttons[(int)Button.DPAD_DOWN] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x01 : 0x04)) != 0;
        buttons[(int)Button.DPAD_RIGHT] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x04 : 0x08)) != 0;
        buttons[(int)Button.DPAD_UP] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x02 : 0x02)) != 0;
        buttons[(int)Button.DPAD_LEFT] = (report_buf[3 + (isLeft ? 2 : 0)] & (isLeft ? 0x08 : 0x01)) != 0;
        buttons[(int)Button.HOME] = ((report_buf[4] & 0x10) != 0);
        buttons[(int)Button.MINUS] = ((report_buf[4] & 0x01) != 0);
        buttons[(int)Button.PLUS] = ((report_buf[4] & 0x02) != 0);
        buttons[(int)Button.STICK] = ((report_buf[4] & (isLeft ? 0x08 : 0x04)) != 0);
        buttons[(int)Button.SHOULDER_1] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x40) != 0;
        buttons[(int)Button.SHOULDER_2] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x80) != 0;
        buttons[(int)Button.SR] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x10) != 0;
        buttons[(int)Button.SL] = (report_buf[3 + (isLeft ? 2 : 0)] & 0x20) != 0;
      }
    }

    private void ExtractIMUValues(byte[] report_buf, int n = 0)
    {
      gyr_r[0] = (Int16)(report_buf[19 + n * 12] | ((report_buf[20 + n * 12] << 8) & 0xff00));
      gyr_r[1] = (Int16)(report_buf[21 + n * 12] | ((report_buf[22 + n * 12] << 8) & 0xff00));
      gyr_r[2] = (Int16)(report_buf[23 + n * 12] | ((report_buf[24 + n * 12] << 8) & 0xff00));
      acc_r[0] = (Int16)(report_buf[13 + n * 12] | ((report_buf[14 + n * 12] << 8) & 0xff00));
      acc_r[1] = (Int16)(report_buf[15 + n * 12] | ((report_buf[16 + n * 12] << 8) & 0xff00));
      acc_r[2] = (Int16)(report_buf[17 + n * 12] | ((report_buf[18 + n * 12] << 8) & 0xff00));

      for (int i = 0; i < 3; ++i)
      {
        //TODO Check this
        if (i == 0)
        {
          acc_g.X = acc_r[i] * 0.00025f;
          gyr_g.X = (gyr_r[i] - gyr_neutral[i]) * 0.00122187695f;
          if (Math.Abs(acc_g.X) > Math.Abs(max[i])) max[i] = acc_g.X;
        }

        if (i == 1)
        {
          acc_g.Y = acc_r[i] * 0.00025f;
          gyr_g.Y = (gyr_r[i] - gyr_neutral[i]) * 0.00122187695f;
          if (Math.Abs(acc_g.Y) > Math.Abs(max[i])) max[i] = acc_g.Y;
        }

        if (i == 2)
        {
          acc_g.Z = acc_r[i] * 0.00025f;
          gyr_g.Z = (gyr_r[i] - gyr_neutral[i]) * 0.00122187695f;
          if (Math.Abs(acc_g.Z) > Math.Abs(max[i])) max[i] = acc_g.Z;
        }
      }
    }

    private float err;

    public Vector3 i_b, j_b, k_b, k_acc;

    private Vector3 d_theta;

    private Vector3 i_b_;

    private Vector3 w_a, w_g;

    private Quaternion vec;

    private int ProcessIMU(byte[] report_buf)
    {
      // Direction Cosine Matrix method
      // http://www.starlino.com/dcm_tutorial.html

      if (!imu_enabled | this.currentConnectionState < ConnectionState.IMU_DATA_OK) return -1;

      if (report_buf[0] != 0x30) return -1; // no gyro data

      // read raw IMU values
      int dt = (report_buf[1] - timestamp);
      if (report_buf[1] < timestamp) dt += 0x100;

      for (int n = 0; n < 3; ++n)
      {
        ExtractIMUValues(report_buf, n);

        float dt_sec = 0.005f * dt;
        sum[0] += gyr_g.X * dt_sec;
        sum[1] += gyr_g.Y * dt_sec;
        sum[2] += gyr_g.Z * dt_sec;

        if (isLeft)
        {
          gyr_g.Y *= -1;
          gyr_g.Z *= -1;
          acc_g.Y *= -1;
          acc_g.Z *= -1;
        }

        if (first_imu_packet)
        {
          i_b = new Vector3(1, 0, 0);
          j_b = new Vector3(0, 1, 0);
          k_b = new Vector3(0, 0, 1);
          first_imu_packet = false;
        }
        else
        {
          k_acc = -Vector3.Normalize(acc_g);
          w_a = Vector3.Cross(k_b, k_acc);
          w_g = -gyr_g * dt_sec;
          d_theta = (filterweight * w_a + w_g) / (1f + filterweight);
          k_b += Vector3.Cross(d_theta, k_b);
          i_b += Vector3.Cross(d_theta, i_b);
          j_b += Vector3.Cross(d_theta, j_b);
          //Correction, ensure new axes are orthogonal
          err = Vector3.Dot(i_b, j_b) * 0.5f;
          i_b_ = Vector3.Normalize(i_b - err * j_b);
          j_b = Vector3.Normalize(j_b - err * i_b);
          i_b = i_b_;
          k_b = Vector3.Cross(i_b, j_b);
        }

        dt = 1;
      }
      timestamp = report_buf[1] + 2;
      return 0;
    }

    internal void Begin()
    {
      if (PollThreadObj == null)
      {
        PollThreadObj = new Thread(Poll);
        PollThreadObj.Start();
      }
    }

    public void Recenter()
    {
      first_imu_packet = true;
    }

    private float[] CenterSticks(UInt16[] vals)
    {
      float[] s = { 0, 0 };
      for (uint i = 0; i < 2; ++i)
      {
        float diff = vals[i] - stick_cal[2 + i];
        if (Math.Abs(diff) < deadzone) vals[i] = 0;
        else if (diff > 0) // if axis is above center
        {
          s[i] = diff / stick_cal[i];
        }
        else
        {
          s[i] = diff / stick_cal[4 + i];
        }
      }
      return s;
    }

    public void SetRumble(float low_freq, float high_freq, float amp)
    {
      if (currentConnectionState <= ConnectionState.ATTACHED) return;
      rumble_obj = new Rumble(low_freq, high_freq, amp);
    }

    private void SendRumble(byte[] buf)
    {
      byte[] buf_ = new byte[report_len];
      buf_[0] = 0x10;
      buf_[1] = global_count;
      if (global_count == 0xf) global_count = 0;
      else ++global_count;
      Array.Copy(buf, 0, buf_, 2, 8);
      PrintArray(buf_, format: "Rumble data sent: {0:S}");
      HIDapi.hid_write(handle, buf_, report_len);
    }

    private byte[] Subcommand(byte sc, byte[] buf, uint len, bool print = true)
    {
      byte[] buf_ = new byte[report_len];
      byte[] response = new byte[report_len];
      Array.Copy(default_buf, 0, buf_, 2, 8);
      Array.Copy(buf, 0, buf_, 11, len);
      buf_[10] = sc;
      buf_[1] = global_count;
      buf_[0] = 0x1;
      if (global_count == 0xf) global_count = 0;
      else ++global_count;
      if (print)
      {
        PrintArray(
          buf_,
          len,
          11,
          "Subcommand 0x" + string.Format("{0:X2}", sc) + " sent. Data: 0x{0:S}");
      }
      ;
      HIDapi.hid_write(handle, buf_, len + 11);
      int res = HIDapi.hid_read_timeout(handle, response, report_len, 50);
      if (res < 1) DebugPrint("No response.");
      else if (print)
      {
        PrintArray(
          response,
          report_len - 1,
          1,
          "Response ID 0x" + string.Format("{0:X2}", response[0]) + ". Data: 0x{0:S}");
      }
      return response;
    }

    private void dump_calibration_data()
    {
      byte[] buf_ = ReadSPI(0x80, (isLeft ? (byte)0x12 : (byte)0x1d), 9); // get user calibration data if possible
      bool found = false;
      for (int i = 0; i < 9; ++i)
      {
        if (buf_[i] != 0xff)
        {
          //Debug.Log("Using user stick calibration data.");
          found = true;
          break;
        }
      }
      if (!found)
      {
        //Debug.Log("Using factory stick calibration data.");
        buf_ = ReadSPI(0x60, (isLeft ? (byte)0x3d : (byte)0x46), 9); // get user calibration data if possible
      }
      stick_cal[isLeft ? 0 : 2] = (UInt16)((buf_[1] << 8) & 0xF00 | buf_[0]); // X Axis Max above center
      stick_cal[isLeft ? 1 : 3] = (UInt16)((buf_[2] << 4) | (buf_[1] >> 4)); // Y Axis Max above center
      stick_cal[isLeft ? 2 : 4] = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]); // X Axis Center
      stick_cal[isLeft ? 3 : 5] = (UInt16)((buf_[5] << 4) | (buf_[4] >> 4)); // Y Axis Center
      stick_cal[isLeft ? 4 : 0] = (UInt16)((buf_[7] << 8) & 0xF00 | buf_[6]); // X Axis Min below center
      stick_cal[isLeft ? 5 : 1] = (UInt16)((buf_[8] << 4) | (buf_[7] >> 4)); // Y Axis Min below center

      PrintArray(stick_cal, len: 6, start: 0, format: "Stick calibration data: {0:S}");

      buf_ = ReadSPI(0x60, (isLeft ? (byte)0x86 : (byte)0x98), 16);
      deadzone = (UInt16)((buf_[4] << 8) & 0xF00 | buf_[3]);

      buf_ = ReadSPI(0x80, 0x34, 10);
      gyr_neutral[0] = (Int16)(buf_[0] | ((buf_[1] << 8) & 0xff00));
      gyr_neutral[1] = (Int16)(buf_[2] | ((buf_[3] << 8) & 0xff00));
      gyr_neutral[2] = (Int16)(buf_[4] | ((buf_[5] << 8) & 0xff00));
      PrintArray(gyr_neutral, len: 3, format: "User gyro neutral position: {0:S}");

      // This is an extremely messy way of checking to see whether there is user stick calibration data present, but I've seen conflicting user calibration data on blank Joy-Cons. Worth another look eventually.
      if (gyr_neutral[0] + gyr_neutral[1] + gyr_neutral[2] == -3 || Math.Abs(gyr_neutral[0]) > 100
          || Math.Abs(gyr_neutral[1]) > 100 || Math.Abs(gyr_neutral[2]) > 100)
      {
        buf_ = ReadSPI(0x60, 0x29, 10);
        gyr_neutral[0] = (Int16)(buf_[3] | ((buf_[4] << 8) & 0xff00));
        gyr_neutral[1] = (Int16)(buf_[5] | ((buf_[6] << 8) & 0xff00));
        gyr_neutral[2] = (Int16)(buf_[7] | ((buf_[8] << 8) & 0xff00));
        PrintArray(gyr_neutral, len: 3, format: "Factory gyro neutral position: {0:S}");
      }
    }

    private byte[] ReadSPI(byte addr1, byte addr2, uint len, bool print = false)
    {
      byte[] buf = { addr2, addr1, 0x00, 0x00, (byte)len };
      byte[] read_buf = new byte[len];
      byte[] buf_ = new byte[len + 20];

      for (int i = 0; i < 100; ++i)
      {
        buf_ = Subcommand(0x10, buf, 5, false);
        if (buf_[15] == addr2 && buf_[16] == addr1)
        {
          break;
        }
      }
      Array.Copy(buf_, 20, read_buf, 0, len);
      if (print) PrintArray(read_buf, len);
      return read_buf;
    }

    private void PrintArray<T>(
      T[] arr,
      uint len = 0,
      uint start = 0,
      string format = "{0:S}")
    {

      if (len == 0) len = (uint)arr.Length;
      string tostr = "";
      for (int i = 0; i < len; ++i)
      {
        tostr += string.Format(
          (arr[0] is byte) ? "{0:X2} " : ((arr[0] is float) ? "{0:F} " : "{0:D} "),
          arr[i + start]);
      }
      DebugPrint(string.Format(format, tostr));
    }
  }
}
