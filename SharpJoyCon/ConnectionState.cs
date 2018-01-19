namespace SharpJoyCon
{
  public enum ConnectionState : uint
  {
    NOT_ATTACHED,
    DROPPED,
    NO_JOYCONS,
    ATTACHED,
    INPUT_MODE_0x30,
    IMU_DATA_OK,
  };
}
