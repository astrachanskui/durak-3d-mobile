using Godot;
using System.Collections.Generic;

namespace Durak.Godot;

public partial class Camera : Camera3D
{
    [Signal]
    public delegate void MovedEventHandler();

    [Export]
    private float _lookAroundSpeed = 0.005f; // Скорость вращения пальцем

    [Export]
    private float _zoomSpeed = 0.002f;       // Скорость приближения щипком

    // Храним активные тачи на экране для поддержки мультитача (зума)
    private readonly Dictionary<int, Vector2> _touchPositions = new();
    private float _initialZoomDistance = 0f;

    private float _rotationX = 0f;
    private float _rotationY = 0f;

    public override void _Ready()
    {
        // Базовая инициализация углов на старте игры на основе текущего положения
        _rotationX = Rotation.Y;
        _rotationY = Rotation.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        var isMoving = false;

        // 1. Отслеживаем нажатия (Touch) и отпускания пальцев
        if (@event is InputEventScreenTouch screenTouch)
        {
            if (screenTouch.Pressed)
            {
                _touchPositions[screenTouch.Index] = screenTouch.Position;
                
                // Если коснулись вторым пальцем — замеряем стартовую дистанцию для зума
                if (_touchPositions.Count == 2)
                {
                    var keys = new List<int>(_touchPositions.Keys);
                    _initialZoomDistance = _touchPositions[keys[0]].DistanceTo(_touchPositions[keys[1]]);
                }
            }
            else
            {
                _touchPositions.Remove(screenTouch.Index);
                if (_touchPositions.Count < 2)
                {
                    _initialZoomDistance = 0f;
                }
            }
        }

        // 2. Отслеживаем движение пальцев по экрану (Drag)
        if (@event is InputEventScreenDrag screenDrag)
        {
            // Обновляем позицию текущего пальца в словаре
            if (_touchPositions.ContainsKey(screenDrag.Index))
            {
                _touchPositions[screenDrag.Index] = screenDrag.Position;
            }

            // РЕЖИМ 1: Движение ОДНИМ пальцем — Вращение камеры
            if (_touchPositions.Count == 1)
            {
                _rotationX -= screenDrag.Relative.X * _lookAroundSpeed;
                _rotationY -= screenDrag.Relative.Y * _lookAroundSpeed;

                // Ограничиваем вращение по вертикали, чтобы камера не переворачивалась вверх ногами
                _rotationY = Mathf.Clamp(_rotationY, Mathf.DegToRad(-85f), Mathf.DegToRad(-5f));

                ResetRotation();
                RotateObjectLocal(Vector3.Up, _rotationX);
                RotateObjectLocal(Vector3.Right, _rotationY);
                isMoving = true;
            }
            // РЕЖИМ 2: Движение ДВУМЯ пальцами — Pinch-to-Zoom (Приближение/Удаление)
            else if (_touchPositions.Count == 2 && _initialZoomDistance > 0f)
            {
                var keys = new List<int>(_touchPositions.Keys);
                float currentDistance = _touchPositions[keys[0]].DistanceTo(_touchPositions[keys[1]]);
                float dragFactor = currentDistance - _initialZoomDistance;

                // Перемещаем камеру вперед/назад вдоль вектора её взгляда
                var inFrontOfCamera = -Transform.Basis.Z;
                Vector3 zoomMovement = inFrontOfCamera * dragFactor * _zoomSpeed;

                // Ограничиваем высоту камеры, чтобы не улететь под стол или слишком высоко
                Vector3 nextPosition = Position + zoomMovement;
                if (nextPosition.Y >= 0.3f && nextPosition.Y <= 1.5f)
                {
                    Position = nextPosition;
                    isMoving = true;
                }

                // Запоминаем текущую дистанцию как базовую для следующего шага драга
                _initialZoomDistance = currentDistance;
            }
        }

        if (isMoving)
        {
            EmitSignal(SignalName.Moved);
        }
    }

    private void ResetRotation()
    {
        var transform = Transform;
        transform.Basis = Basis.Identity;
        Transform = transform;
    }
}
