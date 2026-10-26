using System.Drawing.Drawing2D;
using System.Numerics;

namespace AsteroidsGame;

public partial class Form1 : Form
{
    private const int PlayerFireCooldownSeconds = 160;
    private readonly Random _random = new();
    private readonly List<Asteroid> _asteroids = [];
    private readonly List<Bullet> _bullets = [];
    private readonly List<Particles> _particles = [];
    private readonly HashSet<Keys> _pressedKeys = [];
    private readonly System.Windows.Forms.Timer _gameTimer;
    private readonly Font _hudFont = new("Segoe UI", 16F, FontStyle.Bold);
    private readonly SolidBrush _hudBrush = new(Color.White);
    private Ship _ship;
    private int _score;
    private int _lives = 3;
    private int _level = 1;
    private bool _gameOver;

    public Form1()
    {
        InitializeComponent();

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        ClientSize = new Size(960, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Asteroids";
        BackColor = Color.Black;
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;
        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;

        _ship = CreateShip();
        _gameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _gameTimer.Tick += (_, _) => UpdateGame();

        StartNewLevel();
        _gameTimer.Start();
    }

    private Ship CreateShip()
    {
        return new Ship
        {
            Position = new Vector2(ClientSize.Width / 2f, ClientSize.Height / 2f),
            Velocity = Vector2.Zero,
            Angle = -MathF.PI / 2f,
            Radius = 18f,
            ThrustStrength = 320f,
            RotationSpeed = 3.2f,
            BulletCooldown = 0f,
            InvulnerableTimer = 2.5f,
        };
    }

    private void StartNewLevel()
    {
        _asteroids.Clear();

        int asteroidCount = Math.Clamp(3 + _level, 3, 9);
        for (int i = 0; i < asteroidCount; i++)
        {
            var asteroid = new Asteroid
            {
                Position = GetRandomAsteroidPosition(),
                Velocity = new Vector2(
                    (float)(_random.NextDouble() * 90 + 30) * (_random.Next(2) == 0 ? -1 : 1),
                    (float)(_random.NextDouble() * 90 + 30) * (_random.Next(2) == 0 ? -1 : 1)),
                Radius = 34f,
                Size = 3,
                Rotation = (float)_random.NextDouble() * MathF.PI * 2,
                Spin = (float)(_random.NextDouble() * 1.4 - 0.7)
            };

            while (Vector2.Distance(asteroid.Position, _ship.Position) < 160f)
            {
                asteroid.Position = GetRandomAsteroidPosition();
            }

            _asteroids.Add(asteroid);
        }
    }

    private Vector2 GetRandomAsteroidPosition()
    {
        return new Vector2(
            _random.Next(50, ClientSize.Width - 50),
            _random.Next(50, ClientSize.Height - 50));
    }

    private void UpdateGame()
    {
        float delta = 1f / 60f;

        if (_gameOver)
        {
            if (_pressedKeys.Contains(Keys.R))
            {
                ResetGame();
            }

            Invalidate();
            return;
        }

        HandleInput(delta);
        UpdateBullets(delta);
        UpdateAsteroids(delta);
        UpdateParticles(delta);
        CheckCollisions();

        if (_asteroids.Count == 0)
        {
            _level++;
            StartNewLevel();
            _ship = CreateShip();
        }

        if (_ship.InvulnerableTimer > 0f)
        {
            _ship.InvulnerableTimer -= delta;
        }

        Invalidate();
    }

    private void HandleInput(float delta)
    {
        if (_pressedKeys.Contains(Keys.Left))
        {
            _ship.Angle -= _ship.RotationSpeed * delta;
        }

        if (_pressedKeys.Contains(Keys.Right))
        {
            _ship.Angle += _ship.RotationSpeed * delta;
        }

        if (_pressedKeys.Contains(Keys.Up))
        {
            var thrustDirection = new Vector2(
                MathF.Cos(_ship.Angle),
                MathF.Sin(_ship.Angle));

            _ship.Velocity += thrustDirection * _ship.ThrustStrength * delta;
        }

        _ship.Velocity *= 0.992f;
        _ship.Position += _ship.Velocity * delta;
        _ship.Position = WrapPosition(_ship.Position, _ship.Radius);

        if (_ship.BulletCooldown > 0f)
        {
            _ship.BulletCooldown -= delta * 1000f;
        }

        if (_pressedKeys.Contains(Keys.Space) && _ship.BulletCooldown <= 0)
        {
            FireBullet();
        }
    }

    private void FireBullet()
    {
        var direction = new Vector2(
            MathF.Cos(_ship.Angle),
            MathF.Sin(_ship.Angle));

        var bulletPosition = _ship.Position + direction * (_ship.Radius + 10f);
        _bullets.Add(new Bullet
        {
            Position = bulletPosition,
            Velocity = _ship.Velocity + direction * 600f,
            Radius = 4f,
            Life = 1.4f,
        });

        _ship.BulletCooldown = PlayerFireCooldownSeconds;
    }

    private void UpdateBullets(float delta)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bullet.Position += bullet.Velocity * delta;
            bullet.Life -= delta;
            bullet.Position = WrapPosition(bullet.Position, bullet.Radius);

            if (bullet.Life <= 0)
            {
                _bullets.RemoveAt(i);
            }
        }
    }

    private void UpdateAsteroids(float delta)
    {
        foreach (var asteroid in _asteroids)
        {
            asteroid.Position += asteroid.Velocity * delta;
            asteroid.Rotation += asteroid.Spin * delta * 60f;
            asteroid.Position = WrapPosition(asteroid.Position, asteroid.Radius);
        }
    }

    private void UpdateParticles(float delta)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Position += particle.Velocity * delta;
            particle.Life -= delta;
            if (particle.Life <= 0)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    private void CheckCollisions()
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            for (int j = _asteroids.Count - 1; j >= 0; j--)
            {
                var asteroid = _asteroids[j];

                if (Vector2.DistanceSquared(bullet.Position, asteroid.Position) <= (asteroid.Radius + bullet.Radius) * (asteroid.Radius + bullet.Radius))
                {
                    SpawnExplosion(asteroid.Position, Color.FromArgb(255, 255, 180, 60), 18);
                    _bullets.RemoveAt(i);
                    SplitAsteroid(j);
                    break;
                }
            }
        }

        for (int i = _asteroids.Count - 1; i >= 0; i--)
        {
            var asteroid = _asteroids[i];
            if (Vector2.DistanceSquared(_ship.Position, asteroid.Position) <= (_ship.Radius + asteroid.Radius) * (_ship.Radius + asteroid.Radius))
            {
                if (_ship.InvulnerableTimer <= 0f)
                {
                    SpawnExplosion(_ship.Position, Color.FromArgb(255, 90, 180, 255), 26);
                    _lives--;
                    if (_lives <= 0)
                    {
                        _gameOver = true;
                        _ship = CreateShip();
                        return;
                    }

                    _ship = CreateShip();
                    _ship.InvulnerableTimer = 2f;
                    _asteroids.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void SplitAsteroid(int index)
    {
        var asteroid = _asteroids[index];
        _asteroids.RemoveAt(index);

        if (asteroid.Size > 1)
        {
            int pieces = 2;
            for (int i = 0; i < pieces; i++)
            {
                var smaller = new Asteroid
                {
                    Position = asteroid.Position,
                    Velocity = new Vector2(
                        asteroid.Velocity.X + (float)(_random.NextDouble() * 120 - 60),
                        asteroid.Velocity.Y + (float)(_random.NextDouble() * 120 - 60)),
                    Radius = asteroid.Radius * 0.58f,
                    Size = asteroid.Size - 1,
                    Rotation = (float)_random.NextDouble() * MathF.PI * 2,
                    Spin = (float)(_random.NextDouble() * 2.1 - 1.05),
                };

                _asteroids.Add(smaller);
            }
        }

        _score += asteroid.Size * 100;
    }

    private void SpawnExplosion(Vector2 center, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var angle = (float)(_random.NextDouble() * MathF.PI * 2);
            var speed = (float)(_random.NextDouble() * 120 + 40);
            _particles.Add(new Particles
            {
                Position = center,
                Velocity = new Vector2(
                    MathF.Cos(angle) * speed,
                    MathF.Sin(angle) * speed),
                Radius = (float)_random.NextDouble() * 3f + 1.5f,
                Life = (float)_random.NextDouble() * 0.7f + 0.3f,
                Color = color,
            });
        }
    }

    private void ResetGame()
    {
        _ship = CreateShip();
        _asteroids.Clear();
        _bullets.Clear();
        _particles.Clear();
        _score = 0;
        _lives = 3;
        _level = 1;
        _gameOver = false;
        StartNewLevel();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Add(e.KeyCode);
        if (e.KeyCode == Keys.Space)
        {
            e.SuppressKeyPress = true;
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(e.KeyCode);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.Black);

        DrawStars(g);
        DrawAsteroids(g);
        DrawBullets(g);
        DrawShip(g);
        DrawParticles(g);

        g.DrawString($"Score: {_score}   Lives: {_lives}   Level: {_level}", _hudFont, _hudBrush, 20, 20);

        if (_gameOver)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(180, 0, 0, 0)), new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            var text = "Game Over";
            var size = g.MeasureString(text, new Font("Segoe UI", 32F, FontStyle.Bold));
            g.DrawString(text, new Font("Segoe UI", 32F, FontStyle.Bold), _hudBrush, (ClientSize.Width - size.Width) / 2f, ClientSize.Height / 2f - 30f);
            var restart = "Press R to restart";
            var restartSize = g.MeasureString(restart, new Font("Segoe UI", 22F, FontStyle.Bold));
            g.DrawString(restart, new Font("Segoe UI", 22F, FontStyle.Bold), _hudBrush, (ClientSize.Width - restartSize.Width) / 2f, ClientSize.Height / 2f + 30f);
        }
    }

    private void DrawStars(Graphics g)
    {
        for (int i = 0; i < 80; i++)
        {
            var x = (i * 37 + 17) % ClientSize.Width;
            var y = (i * 53 + 91) % ClientSize.Height;
            var size = (i % 3) + 1;
            g.FillEllipse(Brushes.White, x, y, size, size);
        }
    }

    private void DrawShip(Graphics g)
    {
        if (_ship == null)
        {
            return;
        }

        if (_ship.InvulnerableTimer > 0f && (int)(_ship.InvulnerableTimer * 10) % 2 == 0)
        {
            return;
        }

        var nose = new Vector2(
            MathF.Cos(_ship.Angle),
            MathF.Sin(_ship.Angle));

        var leftWing = new Vector2(
            MathF.Cos(_ship.Angle + 2.5f),
            MathF.Sin(_ship.Angle + 2.5f));

        var rightWing = new Vector2(
            MathF.Cos(_ship.Angle - 2.5f),
            MathF.Sin(_ship.Angle - 2.5f));

        var points = new[]
        {
            _ship.Position + nose * _ship.Radius,
            _ship.Position + leftWing * (_ship.Radius * 0.7f),
            _ship.Position + rightWing * (_ship.Radius * 0.7f),
        };

        using var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(points[0].X, points[0].Y),
            new PointF(points[1].X, points[1].Y),
            new PointF(points[2].X, points[2].Y),
        });

        using var pen = new Pen(Color.Cyan, 2f);
        g.FillPath(Brushes.Cyan, path);
        g.DrawPath(pen, path);

        var thrusterGlow = _pressedKeys.Contains(Keys.Up)
            ? new[]
            {
                _ship.Position - nose * (_ship.Radius * 0.8f),
                _ship.Position + leftWing * (_ship.Radius * 0.3f),
                _ship.Position + rightWing * (_ship.Radius * 0.3f),
            }
            : null;

        if (thrusterGlow != null)
        {
            using var glowPath = new GraphicsPath();
            glowPath.AddPolygon(new[]
            {
                new PointF(thrusterGlow[0].X, thrusterGlow[0].Y),
                new PointF(thrusterGlow[1].X, thrusterGlow[1].Y),
                new PointF(thrusterGlow[2].X, thrusterGlow[2].Y),
            });
            g.FillPath(Brushes.Orange, glowPath);
        }
    }

    private void DrawBullets(Graphics g)
    {
        foreach (var bullet in _bullets)
        {
            g.FillEllipse(Brushes.Yellow, bullet.Position.X - bullet.Radius, bullet.Position.Y - bullet.Radius, bullet.Radius * 2, bullet.Radius * 2);
        }
    }

    private void DrawAsteroids(Graphics g)
    {
        foreach (var asteroid in _asteroids)
        {
            var points = new PointF[8];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (i / (float)points.Length) * MathF.PI * 2f + asteroid.Rotation;
                float radius = asteroid.Radius * (0.8f + (float)(_random.NextDouble() * 0.4f));
                points[i] = new PointF(
                    asteroid.Position.X + MathF.Cos(angle) * radius,
                    asteroid.Position.Y + MathF.Sin(angle) * radius);
            }

            using var path = new GraphicsPath();
            path.AddPolygon(points);

            using var brush = new SolidBrush(Color.DarkGray);
            using var pen = new Pen(Color.LightGray, 2f);
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }
    }

    private void DrawParticles(Graphics g)
    {
        foreach (var particle in _particles)
        {
            using var brush = new SolidBrush(Color.FromArgb((int)(particle.Life * 255), particle.Color));
            g.FillEllipse(brush, particle.Position.X - particle.Radius, particle.Position.Y - particle.Radius, particle.Radius * 2, particle.Radius * 2);
        }
    }

    private Vector2 WrapPosition(Vector2 position, float radius)
    {
        if (position.X < -radius)
        {
            position.X = ClientSize.Width + radius;
        }
        else if (position.X > ClientSize.Width + radius)
        {
            position.X = -radius;
        }

        if (position.Y < -radius)
        {
            position.Y = ClientSize.Height + radius;
        }
        else if (position.Y > ClientSize.Height + radius)
        {
            position.Y = -radius;
        }

        return position;
    }

    private sealed class Ship
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Angle { get; set; }
        public float Radius { get; set; }
        public float ThrustStrength { get; set; }
        public float RotationSpeed { get; set; }
        public float BulletCooldown { get; set; }
        public float InvulnerableTimer { get; set; }
    }

    private sealed class Asteroid
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
        public int Size { get; set; }
        public float Rotation { get; set; }
        public float Spin { get; set; }
    }

    private sealed class Bullet
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
        public float Life { get; set; }
    }

    private sealed class Particles
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; set; }
        public float Life { get; set; }
        public Color Color { get; set; }
    }
}

