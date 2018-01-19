namespace SharpJoyCon
{
  using System;

  internal struct Report
  {
    private const uint report_len = 49;
    byte[] r;
    
    public Report(byte[] data)
    {
      r = data;
      GetTime = DateTime.Now;
    }

    public DateTime GetTime { get; }

    public byte[] Data
    {
      get
      {
        //Copy array and return copy
        byte[] report_buf = new byte[report_len];
        for (int i = 0; i < report_len; ++i)
        {
          report_buf[i] = r[i];
        }
        return report_buf;
      }
    }

    public void CopyBuffer(byte[] b)
    {
      for (int i = 0; i < report_len; ++i)
      {
        b[i] = r[i];
      }
    }
  };
}
