using System;
using System.Numerics;

class Util {
  public static Vector3 RotatePosition(Vector3 pos, Vector3 rot)
  {
    float x = pos.X;
    float y = pos.Y;
    float z = pos.Z;

    Vector3 result = new Vector3(pos.X, pos.Y, pos.Z);

    if (rot.Z != 0)
    {
      float cosZ = (float)Math.Cos(rot.Z);
      float sinZ = (float)Math.Sin(rot.Z);
      result.X = x * cosZ - y * sinZ;
      result.Y = y * cosZ + x * sinZ;
    }

    if (rot.X != 0)
    {
      float cosX = (float)Math.Cos(rot.X);
      float sinX = (float)Math.Sin(rot.X);
      result.Y = y * cosX - z * sinX;
      result.Z = z * cosX + y * sinX;
    }

    if (rot.Y != 0)
    {
      float cosY = (float)Math.Cos(rot.Y);
      float sinY = (float)Math.Sin(rot.Y);
      result.Z = z * cosY - x * sinY;
      result.X = x * cosY + z * sinY;
    }

    return result;
  }
}