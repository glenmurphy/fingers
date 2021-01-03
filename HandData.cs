using System.Numerics; // Vector

public struct HandData
{
  public bool isLeft;
  public bool isActive;
  public bool isPinching;

  // position relative to leap
  // X+ is right
  // Y+ is up
  // Z+ is distance
  public Vector3 pos;

  public float angle;
}