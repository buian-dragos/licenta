using System.Numerics;


namespace code.Models
{
    public class Camera
    {
        public Vector3 Position {get; set;}
        public Vector3 Direction {get; set;}
        public Vector3 Up {get; set;}
        
        public float ViewPlaneDistance {get; set;}
        public float ViewPlaneWidth {get; set;}
        public float ViewPlaneHeight {get; set;}
        
        public float FrontPlaneDistance {get; set;}
        public float BackPlaneDistance {get; set;}

        public Camera(Vector3 position, Vector3 direction, Vector3 up, float viewPlaneDistance, float viewPlaneWidth, float viewPlaneHeight, float frontPlaneDistance, float backPlaneDistance)
        {
            Position = position;
            Direction = direction;
            Up = up;
            ViewPlaneDistance = viewPlaneDistance;
            ViewPlaneWidth = viewPlaneWidth;
            ViewPlaneHeight = viewPlaneHeight;
            FrontPlaneDistance = frontPlaneDistance;
            BackPlaneDistance = backPlaneDistance;
        }

        public void Normalize()
        {
            Direction = Vector3.Normalize(Direction);
            Up = Vector3.Normalize(Up);
            Up = Vector3.Cross(Vector3.Cross(Direction, Up), Direction);
        }
    }   
}
