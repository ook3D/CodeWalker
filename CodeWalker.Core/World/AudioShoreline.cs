using CodeWalker.GameFiles;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeWalker.World
{
    public partial class AudioPlacement
    {
        public Dat151RelData? Shoreline { get; private set; }
        public AudioPlacement? ShorelineParent { get; private set; }
        public int ShorelinePointIndex { get; private set; } = -1;
        public AudioPlacement[] ShorelinePoints { get; private set; } = [];
        private List<WaterQuad> shorelineWaterQuads = new();

        public static Vector2 GetOceanDirectionVector(byte direction)
        {
            // audShoreLineOcean::CalculateDetectionPoints: north, then clockwise in 45 degree steps.
            return direction switch
            {
                0 => new(0, 1), 1 => new(0.70710678f, 0.70710678f),
                2 => new(1, 0), 3 => new(0.70710678f, -0.70710678f),
                4 => new(0, -1), 5 => new(-0.70710678f, -0.70710678f),
                6 => new(-1, 0), 7 => new(-0.70710678f, 0.70710678f),
                _ => Vector2.Zero
            };
        }

        public static byte CalculateOceanDirection(Vector2[] points, bool waterOnLeft)
        {
            if (points.Length < 2) throw new InvalidOperationException("At least two distinct points are needed.");
            var run = points[points.Length - 1] - points[0];
            if (run.LengthSquared() < 0.000001f)
                throw new InvalidOperationException("The shoreline ends coincide. Split the shoreline into open sections before calculating its direction.");
            var normal = new Vector2(-run.Y, run.X) * (waterOnLeft ? 1 : -1);
            double bestScore = double.MaxValue;
            byte best = 0;
            for (byte direction = 0; direction < 8; direction++)
            {
                var vector = GetOceanDirectionVector(direction);
                if (Vector2.Dot(vector, normal) <= 0) continue;
                double score = 0;
                for (int i = 1; i < points.Length; i++)
                {
                    var segment = points[i] - points[i - 1];
                    float length = segment.Length();
                    if (length < 0.000001f) continue;
                    double along = Vector2.Dot(vector, segment);
                    score += along * along / length;
                }
                if (score >= bestScore) continue;
                bestScore = score;
                best = direction;
            }
            return best;
        }

        public static int CountParallelOceanSegments(Vector2[] points, byte direction)
        {
            var vector = GetOceanDirectionVector(direction);
            int count = 0;
            for (int i = 1; i < points.Length; i++)
            {
                var segment = points[i] - points[i - 1];
                float length = segment.Length();
                if (length > 0.000001f && Math.Abs(Vector2.Dot(vector, segment)) >= length * 0.8660254f) count++;
            }
            return count;
        }

        public static bool IsShoreline(RelData data) => data is Dat151ShoreLinePoolAudioSettings
            or Dat151ShoreLineLakeAudioSettings or Dat151ShoreLineRiverAudioSettings or Dat151ShoreLineOceanAudioSettings;

        private Vector2[] ShorelinePoints2D => Shoreline switch
        {
            Dat151ShoreLinePoolAudioSettings pool => pool.Points,
            Dat151ShoreLineLakeAudioSettings lake => lake.Points,
            Dat151ShoreLineOceanAudioSettings ocean => ocean.Points,
            _ => []
        };

        public AudioPlacement(RelFile rel, Dat151RelData shoreline)
        {
            if (!IsShoreline(shoreline)) throw new ArgumentException("Expected shoreline settings.", nameof(shoreline));
            RelFile = rel;
            Shoreline = shoreline;
            ShortTypeName = "Shoreline";
            FullTypeName = "Shoreline";
            int count = shoreline is Dat151ShoreLineRiverAudioSettings river ? river.Points.Length : ShorelinePoints2D.Length;
            ShorelinePoints = new AudioPlacement[count];
            for (int i = 0; i < count; i++) ShorelinePoints[i] = new AudioPlacement(this, i);
            UpdateFromShoreline();
        }

        private AudioPlacement(AudioPlacement parent, int index)
        {
            RelFile = parent.RelFile;
            Shoreline = parent.Shoreline;
            ShorelineParent = parent;
            ShorelinePointIndex = index;
            ShortTypeName = "ShorelinePoint";
            FullTypeName = "Shoreline Point";
        }

        public AudioPlacement DuplicateShorelinePoint()
        {
            if (ShorelineParent == null) throw new InvalidOperationException("Select a shoreline point to duplicate.");
            var parent = ShorelineParent;
            int index = ShorelinePointIndex + 1;
            var points = parent.ShorelinePoints2D;
            if (Shoreline is Dat151ShoreLinePoolAudioSettings && points.Length > 2 && points[0] == points[points.Length - 1])
                index = Math.Min(index, points.Length - 1);
            return parent.InsertShorelinePoint(index, Position);
        }

        public AudioPlacement InsertShorelinePoint(int index, Vector3 position, AudioPlacement? point = null)
        {
            if (Shoreline == null || ShorelineParent != null) throw new InvalidOperationException("Expected the whole shoreline.");
            int limit = Shoreline is Dat151ShoreLineLakeAudioSettings ? byte.MaxValue
                : Shoreline is Dat151ShoreLinePoolAudioSettings ? ushort.MaxValue : int.MaxValue;
            if (ShorelinePoints.Length >= limit) throw new InvalidOperationException($"This shoreline supports at most {limit} points.");
            if (index < 0 || index > ShorelinePoints.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var oldBounds = ShorelinePoints.Length > 0 ? GetShorelineBounds() : Vector4.Zero;
            var positions = ShorelinePoints.Select(p => p.Position).ToList();
            positions.Insert(index, position);
            var handles = ShorelinePoints.ToList();
            point ??= new AudioPlacement(this, index);
            handles.Insert(index, point);
            SetShorelinePoints(positions, handles);
            AdjustShorelineBounds(oldBounds);
            return point;
        }

        public void RemoveShorelinePoint(AudioPlacement point)
        {
            if (point.ShorelineParent != this || !ShorelinePoints.Contains(point)) throw new ArgumentException("Point does not belong to this shoreline.", nameof(point));
            if (ShorelinePoints.Length <= 1) throw new InvalidOperationException("Cannot remove the last shoreline point.");
            var oldBounds = GetShorelineBounds();
            var positions = ShorelinePoints.Select(p => p.Position).ToList();
            var handles = ShorelinePoints.ToList();
            positions.RemoveAt(point.ShorelinePointIndex);
            handles.Remove(point);
            SetShorelinePoints(positions, handles);
            AdjustShorelineBounds(oldBounds);
        }

        public bool RemoveShoreline()
        {
            if (Shoreline == null || !RelFile.RemoveRelData(Shoreline)) return false;
            foreach (var data in RelFile.RelDatas)
            {
                if (data is Dat151ShoreLineList list)
                {
                    list.ShoreLines = list.ShoreLines.Where(hash => hash != Shoreline.NameHash).ToArray();
                    list.ShoreLineCount = (uint)list.ShoreLines.Length;
                }
                if (data is Dat151ShoreLineLakeAudioSettings lake && lake.NextShoreline == Shoreline.NameHash) lake.NextShoreline = 0;
                if (data is Dat151ShoreLineRiverAudioSettings river && river.NextShoreline == Shoreline.NameHash) river.NextShoreline = 0;
                if (data is Dat151ShoreLineOceanAudioSettings ocean && ocean.NextShoreline == Shoreline.NameHash) ocean.NextShoreline = 0;
            }
            return true;
        }

        private void SetShorelinePoints(List<Vector3> positions, List<AudioPlacement> handles)
        {
            var points = positions.Select(p => new Vector2(p.X, p.Y)).ToArray();
            switch (Shoreline)
            {
                case Dat151ShoreLinePoolAudioSettings pool: pool.Points = points; pool.PointsCount = (ushort)points.Length; break;
                case Dat151ShoreLineLakeAudioSettings lake: lake.Points = points; lake.NumShorelinePoints = (byte)points.Length; break;
                case Dat151ShoreLineOceanAudioSettings ocean: ocean.Points = points; ocean.PointsCount = (uint)points.Length; break;
                case Dat151ShoreLineRiverAudioSettings river: river.Points = positions.ToArray(); river.PointsCount = (uint)points.Length; break;
            }
            ShorelinePoints = handles.ToArray();
            for (int i = 0; i < ShorelinePoints.Length; i++) ShorelinePoints[i].ShorelinePointIndex = i;
            UpdateFromShoreline();
        }

        public void UpdateFromShoreline(List<WaterQuad>? waterQuads = null)
        {
            if (Shoreline == null) return;
            if (ShorelineParent != null)
            {
                ShorelineParent.UpdateFromShoreline(waterQuads);
                return;
            }
            if (waterQuads != null) shorelineWaterQuads = waterQuads;
            Name = Shoreline.GetNameString();
            NameHash = Shoreline.NameHash;
            Vector3 center = Vector3.Zero;
            for (int i = 0; i < ShorelinePoints.Length; i++)
            {
                var position = Shoreline is Dat151ShoreLineRiverAudioSettings river ? river.Points[i]
                    : AudioZones.GetShorelinePosition(ShorelinePoints2D[i], shorelineWaterQuads, Shoreline is Dat151ShoreLineOceanAudioSettings);
                var point = ShorelinePoints[i];
                point.Name = Name + " / Point " + i;
                point.NameHash = NameHash;
                point.SetShorelineHandle(position, 0.5f);
                center += position;
            }
            if (ShorelinePoints.Length > 0) center /= ShorelinePoints.Length;
            SetShorelineHandle(center, 1.0f);
        }

        private void SetShorelineHandle(Vector3 position, float radius)
        {
            Position = InnerPos = OuterPos = position;
            Orientation = OrientationInv = InnerOri = OuterOri = Quaternion.Identity;
            Shape = Dat151ZoneShape.Sphere;
            InnerRadius = OuterRadius = HitSphereRad = radius;
            HitboxMin = new Vector3(-radius);
            HitboxMax = new Vector3(radius);
        }

        private void SetShorelinePosition(Vector3 position)
        {
            var parent = ShorelineParent ?? this;
            parent.UpdateFromShoreline();
            if (parent.ShorelinePoints.Length == 0) return;
            var oldBounds = parent.GetShorelineBounds();
            var delta = position - Position;
            int start = ShorelinePointIndex < 0 ? 0 : ShorelinePointIndex;
            int end = ShorelinePointIndex < 0 ? parent.ShorelinePoints.Length : start + 1;
            if (Shoreline is Dat151ShoreLineRiverAudioSettings river)
            {
                for (int i = start; i < end; i++) river.Points[i] += delta;
                if (ShorelinePointIndex < 0) river.DefaultHeight += delta.Z;
            }
            else
            {
                var points = ShorelinePoints2D;
                bool closed = Shoreline is Dat151ShoreLinePoolAudioSettings && points.Length > 2 && points[0] == points[points.Length - 1];
                var offset = new Vector2(delta.X, delta.Y);
                for (int i = start; i < end; i++) points[i] += offset;
                // Keep an explicitly repeated closing vertex attached to its partner.
                if (closed && ShorelinePointIndex == 0) points[points.Length - 1] = points[0];
                else if (closed && ShorelinePointIndex == points.Length - 1) points[0] = points[points.Length - 1];
            }
            parent.UpdateFromShoreline();
            parent.AdjustShorelineBounds(oldBounds);
        }

        private void AdjustShorelineBounds(Vector4 oldBounds)
        {
            // Preserve the activation margin when either the whole outline or an extremal point moves.
            var boundsDelta = GetShorelineBounds() - oldBounds;
            // ActivationBox is XY centre followed by full XY dimensions, not min/max coordinates.
            var offsetBounds = new Vector4((boundsDelta.X + boundsDelta.Z) * 0.5f,
                (boundsDelta.Y + boundsDelta.W) * 0.5f, boundsDelta.Z - boundsDelta.X, boundsDelta.W - boundsDelta.Y);
            var worldOffset = RotateShorelineXY(new Vector2(offsetBounds.X, offsetBounds.Y), ShorelineRotation);
            offsetBounds.X = worldOffset.X;
            offsetBounds.Y = worldOffset.Y;
            switch (Shoreline)
            {
                case Dat151ShoreLinePoolAudioSettings pool: pool.ActivationBox += offsetBounds; break;
                case Dat151ShoreLineLakeAudioSettings lake: lake.ActivationBox += offsetBounds; break;
                case Dat151ShoreLineRiverAudioSettings riverSettings: riverSettings.ActivationBox += offsetBounds; break;
                case Dat151ShoreLineOceanAudioSettings ocean: ocean.ActivationBox += offsetBounds; break;
            }
        }

        private Vector4 GetShorelineBounds()
        {
            var min = RotateShorelineXY(new Vector2(ShorelinePoints[0].Position.X, ShorelinePoints[0].Position.Y), -ShorelineRotation);
            var max = min;
            foreach (var point in ShorelinePoints)
            {
                var local = RotateShorelineXY(new Vector2(point.Position.X, point.Position.Y), -ShorelineRotation);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return new Vector4(min.X, min.Y, max.X, max.Y);
        }

        private float ShorelineRotation => Shoreline switch
        {
            Dat151ShoreLinePoolAudioSettings pool => pool.RotationAngle,
            Dat151ShoreLineLakeAudioSettings lake => lake.RotationAngle,
            Dat151ShoreLineRiverAudioSettings river => river.RotationAngle,
            Dat151ShoreLineOceanAudioSettings ocean => ocean.RotationAngle,
            _ => 0
        };

        private static Vector2 RotateShorelineXY(Vector2 point, float degrees)
        {
            double radians = degrees * Math.PI / 180;
            double cos = Math.Cos(radians), sin = Math.Sin(radians);
            return new Vector2((float)(point.X * cos - point.Y * sin), (float)(point.X * sin + point.Y * cos));
        }

        public void RecalculateShorelineBox(float padding)
        {
            if (!float.IsFinite(padding) || padding < 0) throw new ArgumentOutOfRangeException(nameof(padding));
            var parent = ShorelineParent ?? this;
            if (parent.ShorelinePoints.Length == 0) throw new InvalidOperationException("Add a shoreline point before calculating its box.");
            parent.UpdateFromShoreline();
            var bounds = parent.GetShorelineBounds();
            var center = RotateShorelineXY(new Vector2((bounds.X + bounds.Z) * 0.5f, (bounds.Y + bounds.W) * 0.5f), parent.ShorelineRotation);
            var box = new Vector4(center.X, center.Y, bounds.Z - bounds.X + 2 * padding, bounds.W - bounds.Y + 2 * padding);
            switch (Shoreline)
            {
                case Dat151ShoreLinePoolAudioSettings pool: pool.ActivationBox = box; break;
                case Dat151ShoreLineLakeAudioSettings lake: lake.ActivationBox = box; break;
                case Dat151ShoreLineRiverAudioSettings river: river.ActivationBox = box; break;
                case Dat151ShoreLineOceanAudioSettings ocean: ocean.ActivationBox = box; break;
            }
        }
    }
}
