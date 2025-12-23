# Quick Fixes - Code Examples

## 1. Fix Static Texture Memory Leak

### Before (Laser.cs):
```csharp
private static Texture2D? _pixelTexture;

public Laser(...)
{
    if (_pixelTexture == null)
    {
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }
}
```

### After:
```csharp
private static Texture2D? _pixelTexture;
private static int _referenceCount = 0;

public Laser(...)
{
    if (_pixelTexture == null)
    {
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }
    _referenceCount++;
}

public static void DisposeSharedTexture()
{
    if (_pixelTexture != null && _referenceCount == 0)
    {
        _pixelTexture.Dispose();
        _pixelTexture = null;
    }
}
```

Or better - use a TextureManager singleton.

---

## 2. Consolidate Ship Dictionaries

### Before:
```csharp
Dictionary<FriendlyShip, FriendlyShipBehavior> _friendlyShipBehaviors;
Dictionary<FriendlyShip, float> _friendlyShipBehaviorTimer;
Dictionary<FriendlyShip, Vector2> _friendlyShipLastAvoidanceTarget;
// ... 10+ more dictionaries
```

### After:
```csharp
public class ShipState
{
    public FriendlyShipBehavior Behavior { get; set; }
    public float BehaviorTimer { get; set; }
    public Vector2 LastAvoidanceTarget { get; set; }
    public Vector2 OriginalDestination { get; set; }
    public Vector2 LastPosition { get; set; }
    public float StuckTimer { get; set; }
    public List<Vector2> Path { get; set; } = new List<Vector2>();
    public List<Vector2> PatrolPoints { get; set; } = new List<Vector2>();
    public List<Vector2> AStarPath { get; set; } = new List<Vector2>();
    public int CurrentWaypointIndex { get; set; }
    public float ClosestDistanceToTarget { get; set; }
    public float NoProgressTimer { get; set; }
    public Vector2 LastTarget { get; set; }
    public Vector2 LookAheadTarget { get; set; }
    public Vector2 LastDirection { get; set; }
}

Dictionary<FriendlyShip, ShipState> _shipStates = new Dictionary<FriendlyShip, ShipState>();

// Usage:
var state = _shipStates[friendlyShip];
state.Behavior = FriendlyShipBehavior.Patrol;
state.BehaviorTimer = 10f;
```

---

## 3. Extract Angle Normalization Helper

### Create MathHelperExtensions.cs:
```csharp
public static class MathHelperExtensions
{
    public static float NormalizeAngle(float angle)
    {
        while (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
        while (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
        return angle;
    }
    
    public static float AngleDifference(float from, float to)
    {
        float diff = to - from;
        return NormalizeAngle(diff);
    }
}
```

### Usage:
```csharp
// Before:
float angleDiff = targetRotation - Rotation;
while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;

// After:
float angleDiff = MathHelperExtensions.AngleDifference(Rotation, targetRotation);
```

---

## 4. Object Pooling for Lasers

### Create ObjectPool.cs:
```csharp
public class ObjectPool<T> where T : class, new()
{
    private readonly Queue<T> _pool = new Queue<T>();
    private readonly Func<T> _factory;
    private readonly Action<T> _reset;
    
    public ObjectPool(Func<T> factory, Action<T> reset, int initialSize = 10)
    {
        _factory = factory;
        _reset = reset;
        
        for (int i = 0; i < initialSize; i++)
        {
            _pool.Enqueue(factory());
        }
    }
    
    public T Get()
    {
        return _pool.Count > 0 ? _pool.Dequeue() : _factory();
    }
    
    public void Return(T item)
    {
        _reset(item);
        _pool.Enqueue(item);
    }
}
```

### Usage in GameScene:
```csharp
private ObjectPool<Laser> _laserPool;

// In LoadContent:
_laserPool = new ObjectPool<Laser>(
    () => new Laser(Vector2.Zero, 0f, GraphicsDevice),
    laser => { laser.IsActive = false; laser.Position = Vector2.Zero; }
);

// When firing:
var laser = _laserPool.Get();
laser.Position = startPos;
laser.Rotation = direction;
laser.IsActive = true;
laser.Damage = damage;
_lasers.Add(laser);

// When laser becomes inactive:
_laserPool.Return(laser);
_lasers.Remove(laser);
```

---

## 5. Shared Random Instance

### Create RandomProvider.cs:
```csharp
public static class RandomProvider
{
    private static readonly Random _random = new Random();
    private static readonly object _lock = new object();
    
    public static int Next(int maxValue)
    {
        lock (_lock)
        {
            return _random.Next(maxValue);
        }
    }
    
    public static int Next(int minValue, int maxValue)
    {
        lock (_lock)
        {
            return _random.Next(minValue, maxValue);
        }
    }
    
    public static double NextDouble()
    {
        lock (_lock)
        {
            return _random.NextDouble();
        }
    }
}
```

### Usage:
```csharp
// Before:
var random = new Random();
float angle = (float)(random.NextDouble() * MathHelper.TwoPi);

// After:
float angle = (float)(RandomProvider.NextDouble() * MathHelper.TwoPi);
```

---

## 6. Remove Debug Console.WriteLine

### Create Logger.cs:
```csharp
public static class Logger
{
    public static void Debug(string message)
    {
        #if DEBUG
        System.Console.WriteLine($"[DEBUG] {message}");
        #endif
    }
    
    public static void Info(string message)
    {
        #if DEBUG
        System.Console.WriteLine($"[INFO] {message}");
        #endif
    }
    
    public static void Error(string message, Exception? ex = null)
    {
        System.Console.WriteLine($"[ERROR] {message}");
        if (ex != null)
        {
            System.Console.WriteLine($"Exception: {ex}");
        }
    }
}
```

### Usage:
```csharp
// Before:
System.Console.WriteLine($"[MUSIC] Main menu music loaded...");

// After:
Logger.Info("Main menu music loaded");
```

---

## 7. Fix Empty Catch Blocks

### Before:
```csharp
catch { }
```

### After:
```csharp
catch (Exception ex)
{
    Logger.Error("Failed to dispose music instance", ex);
}
```

---

## 8. Use Null-Conditional Operators

### Before:
```csharp
var spriteBatch = (SpriteBatch)Game.Services.GetService(typeof(SpriteBatch));
if (spriteBatch != null)
{
    _currentScene?.Draw(gameTime, spriteBatch);
}
```

### After:
```csharp
var spriteBatch = Game.Services.GetService(typeof(SpriteBatch)) as SpriteBatch;
_currentScene?.Draw(gameTime, spriteBatch);
```

Or better, pass spriteBatch as parameter to avoid service locator.

---

## 9. Extract Magic Numbers

### Before:
```csharp
if (distance > 1f)
{
    // Move
}
float scale = Math.Max(scaleX, scaleY);
```

### After:
```csharp
private const float MinimumMovementDistance = 1f;
private const float Epsilon = 0.1f;

if (distance > MinimumMovementDistance)
{
    // Move
}
```

---

## 10. Batch Remove Inactive Entities

### Before:
```csharp
foreach (var laser in _lasers)
{
    if (!laser.IsActive)
    {
        _lasers.Remove(laser); // Inefficient
    }
}
```

### After:
```csharp
_lasers.RemoveAll(l => !l.IsActive);
_friendlyShips.RemoveAll(s => s.Health <= 0f);
```

---

## 11. Proper Using Statements

### Before:
```csharp
private System.Collections.Generic.List<FriendlyShip> _friendlyShips;
```

### After:
```csharp
using System.Collections.Generic;

private List<FriendlyShip> _friendlyShips;
```

---

## 12. Scene Content Management

### Update Scene.cs:
```csharp
public abstract class Scene : IDisposable
{
    protected Game Game { get; }
    protected ContentManager Content { get; }
    // ...
    
    public Scene(Game game)
    {
        Game = game;
        Content = new ContentManager(game.Services, "Content/xnb");
        GraphicsDevice = game.GraphicsDevice;
        SpriteBatch = (SpriteBatch?)game.Services.GetService(typeof(SpriteBatch));
    }
    
    public virtual void UnloadContent()
    {
        Content?.Unload();
    }
    
    public void Dispose()
    {
        UnloadContent();
        Content?.Dispose();
    }
}
```

---

## Priority Order for Implementation:

1. **High Priority (Do First):**
   - Fix static texture disposal (#1)
   - Consolidate ship dictionaries (#2)
   - Remove debug Console.WriteLine (#6)
   - Fix empty catch blocks (#7)

2. **Medium Priority:**
   - Extract angle normalization (#3)
   - Object pooling (#4)
   - Shared Random (#5)
   - Batch remove entities (#10)

3. **Low Priority (Nice to Have):**
   - Null-conditional operators (#8)
   - Extract magic numbers (#9)
   - Proper using statements (#11)
   - Scene content management (#12)

---

*These fixes will significantly improve performance and code quality!*

