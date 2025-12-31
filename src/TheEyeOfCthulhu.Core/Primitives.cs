namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Point avec coordonnées flottantes (double précision).
/// </summary>
public readonly struct PointF : IEquatable<PointF>
{
    public double X { get; }
    public double Y { get; }
    
    public PointF(double x, double y)
    {
        X = x;
        Y = y;
    }
    
    // Constructeur avec float pour compatibilité
    public PointF(float x, float y) : this((double)x, (double)y) { }
    
    public static PointF Empty => new(0, 0);
    public static PointF Zero => new(0, 0);
    
    public Point ToPoint() => new((int)X, (int)Y);
    
    public bool Equals(PointF other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is PointF other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:F2}, {Y:F2})";
    
    public static bool operator ==(PointF left, PointF right) => left.Equals(right);
    public static bool operator !=(PointF left, PointF right) => !left.Equals(right);
    
    public static PointF operator +(PointF a, PointF b) => new(a.X + b.X, a.Y + b.Y);
    public static PointF operator -(PointF a, PointF b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>
/// Point avec coordonnées entières.
/// </summary>
public readonly struct Point : IEquatable<Point>
{
    public int X { get; }
    public int Y { get; }
    
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
    
    public static Point Empty => new(0, 0);
    public static Point Zero => new(0, 0);
    
    public bool Equals(Point other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
    
    public static bool operator ==(Point left, Point right) => left.Equals(right);
    public static bool operator !=(Point left, Point right) => !left.Equals(right);
    
    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    
    public static implicit operator PointF(Point p) => new(p.X, p.Y);
}

/// <summary>
/// Taille (largeur x hauteur).
/// </summary>
public readonly struct Size : IEquatable<Size>
{
    public int Width { get; }
    public int Height { get; }
    
    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }
    
    public static Size Empty => new(0, 0);
    
    public bool IsEmpty => Width == 0 && Height == 0;
    
    public bool Equals(Size other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Size other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"{Width}x{Height}";
    
    public static bool operator ==(Size left, Size right) => left.Equals(right);
    public static bool operator !=(Size left, Size right) => !left.Equals(right);
}

/// <summary>
/// Rectangle (position + taille).
/// </summary>
public readonly struct Rectangle : IEquatable<Rectangle>
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    
    public Rectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    
    public static Rectangle Empty => new(0, 0, 0, 0);
    
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    
    public Point Location => new(X, Y);
    public Size Size => new(Width, Height);
    public PointF Center => new(X + Width / 2.0, Y + Height / 2.0);
    
    public bool IsEmpty => Width == 0 && Height == 0;
    
    public bool Contains(int x, int y) => 
        x >= X && x < Right && y >= Y && y < Bottom;
    
    public bool Contains(Point p) => Contains(p.X, p.Y);
    
    public bool Contains(PointF p) => 
        p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
    
    public bool IntersectsWith(Rectangle other) =>
        other.X < Right && X < other.Right &&
        other.Y < Bottom && Y < other.Bottom;
    
    public bool Equals(Rectangle other) => 
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    
    public override bool Equals(object? obj) => obj is Rectangle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"[{X},{Y} {Width}x{Height}]";
    
    public static bool operator ==(Rectangle left, Rectangle right) => left.Equals(right);
    public static bool operator !=(Rectangle left, Rectangle right) => !left.Equals(right);
}
