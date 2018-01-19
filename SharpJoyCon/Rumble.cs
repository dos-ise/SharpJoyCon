using System;
using log4net;

namespace SharpJoyCon
{
  internal struct Rumble
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(Rumble));
    private float h_f, amp, l_f;

    public float t;
    public bool timed_rumble;

    public void set_vals(float low_freq, float high_freq, float amplitude, int time = 0)
    {
      h_f = high_freq;
      amp = amplitude;
      l_f = low_freq;
      timed_rumble = false;
      t = 0;
      if (time != 0)
      {
        t = time / 1000f;
        timed_rumble = true;
      }
    }

    public Rumble(float low_freq, float high_freq, float amplitude, int time = 0)
    {
      h_f = high_freq;
      amp = amplitude;
      l_f = low_freq;
      timed_rumble = false;
      t = 0;
      if (time != 0)
      {
        t = time / 1000f;
        timed_rumble = true;
      }
    }

    private float clamp(float x, float min, float max)
    {
      if (x < min) return min;
      if (x > max) return max;
      return x;
    }

    public byte[] GetData()
    {
      byte[] rumble_data = new byte[8];
      l_f = clamp(l_f, 40.875885f, 626.286133f);
      amp = clamp(amp, 0.0f, 1.0f);
      h_f = clamp(h_f, 81.75177f, 1252.572266f);
      UInt16 hf = (UInt16)((Math.Round(32f * Math.Log(h_f * 0.1f, 2)) - 0x60) * 4);
      byte lf = (byte)(Math.Round(32f * Math.Log(l_f * 0.1f, 2)) - 0x40);
      byte hf_amp;
      if (amp == 0) hf_amp = 0;
      else if (amp < 0.117) hf_amp = (byte)(((Math.Log(amp * 1000, 2) * 32) - 0x60) / (5 - Math.Pow(amp, 2)) - 1);
      else if (amp < 0.23) hf_amp = (byte)(((Math.Log(amp * 1000, 2) * 32) - 0x60) - 0x5c);
      else hf_amp = (byte)((((Math.Log(amp * 1000, 2) * 32) - 0x60) * 2) - 0xf6);

      UInt16 lf_amp = (UInt16)(Math.Round((double)hf_amp) * .5);
      byte parity = (byte)(lf_amp % 2);
      if (parity > 0)
      {
        --lf_amp;
      }

      lf_amp = (UInt16)(lf_amp >> 1);
      lf_amp += 0x40;
      if (parity > 0) lf_amp |= 0x8000;
      rumble_data = new byte[8];
      rumble_data[0] = (byte)(hf & 0xff);
      rumble_data[1] = (byte)((hf >> 8) & 0xff);
      rumble_data[2] = lf;
      rumble_data[1] += hf_amp;
      rumble_data[2] += (byte)((lf_amp >> 8) & 0xff);
      rumble_data[3] += (byte)(lf_amp & 0xff);
      for (int i = 0; i < 4; ++i)
      {
        rumble_data[4 + i] = rumble_data[i];
      }

      //Log.Debug(string.Format("Encoded hex freq: {0:X2}", encoded_hex_freq));
      Log.Debug(string.Format("lf_amp: {0:X4}", lf_amp));
      Log.Debug(string.Format("hf_amp: {0:X2}", hf_amp));
      Log.Debug(string.Format("l_f: {0:F}", l_f));
      Log.Debug(string.Format("hf: {0:X4}", hf));
      Log.Debug(string.Format("lf: {0:X2}", lf));

      return rumble_data;
    }
  }
}
