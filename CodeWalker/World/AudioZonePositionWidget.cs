using SharpDX;

namespace CodeWalker.World
{
    public class AudioZonePositionWidget : TransformWidget
    {
        public AudioPlacement Audio { get; }
        public AudioZoneMoveMode MoveMode { get; }
        public Vector3 TargetPosition => MoveMode == AudioZoneMoveMode.Activation ? Audio.OuterPos : Audio.InnerPos;

        public AudioZonePositionWidget(AudioPlacement audio, AudioZoneMoveMode moveMode)
        {
            Audio = audio;
            MoveMode = moveMode;
            Mode = WidgetMode.Position;
            PositionWidget.Size = moveMode == AudioZoneMoveMode.Activation ? 115 : 70;
            Position = TargetPosition;
        }

        public void MoveTo(Vector3 position)
        {
            Audio.SetPosition(position, MoveMode);
            Position = TargetPosition;
        }

        public override void Update(Camera camera)
        {
            if (!IsDragging) Position = TargetPosition;
            Rotation = MoveMode == AudioZoneMoveMode.Activation ? Audio.OuterOri : Audio.InnerOri;
            base.Update(camera);
        }
    }
}
