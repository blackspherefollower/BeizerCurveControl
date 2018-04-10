using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BeizerCurveControl
{
    /// <summary>
    /// Interaction logic for BezierCurveControl.xaml
    /// </summary>
    public partial class BezierCurveControl
    {
        private readonly CurvePointList _points;

        private CurvePoint _activePoint;
        private double _activeMin;
        private double _activeMax;

        public BezierCurveControl()
        {
            _points = new CurvePointList(this);
            _points.AddPoint(new Point(0, 1000));
            _points.AddPoint(new Point(1000, 0));
            
            InitializeComponent();
        }

        private PolyLineSegment GetBezierApproximation(Point[] controlPoints, int outputSegmentCount)
        {
            var points = new Point[outputSegmentCount + 1];
            for (var i = 0; i <= outputSegmentCount; i++)
            {
                var t = (double)i / outputSegmentCount;
                points[i] = GetBezierPoint(t, controlPoints, 0, controlPoints.Length);
            }
            return new PolyLineSegment(points, true);
        }

        private Point GetBezierPoint(double t, Point[] controlPoints, int index, int count)
        {
            if (count == 1)
                return controlPoints[index];
            var p0 = GetBezierPoint(t, controlPoints, index, count - 1);
            var p1 = GetBezierPoint(t, controlPoints, index + 1, count - 1);
            return new Point((1 - t) * p0.X + t * p1.X, (1 - t) * p0.Y + t * p1.Y);
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            var b = GetBezierApproximation(_points.ToPointArray(), 256);
            var pge = new PathGeometry(new List<PathFigure>() {new PathFigure(b.Points[0], new[] { b }, false)});

            Curve.Data = pge;
        }

        public static Point[] GetIntersectionPoints(Geometry g1, Geometry g2)
        {
            var og1 = g1.GetWidenedPathGeometry(new Pen(Brushes.Black, 1.0));
            var og2 = g2.GetWidenedPathGeometry(new Pen(Brushes.Black, 1.0));
            var cg = new CombinedGeometry(GeometryCombineMode.Intersect, og1, og2);
            PathGeometry pg = cg.GetFlattenedPathGeometry();
            Point[] result = new Point[pg.Figures.Count];
            for (int i = 0; i < pg.Figures.Count; i++)
            {
                Rect fig = new PathGeometry(new[] { pg.Figures[i] }).Bounds;
                result[i] = new Point(fig.Left + fig.Width / 2.0, fig.Top + fig.Height / 2.0);
            }
            return result;
        }

        private void Canvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activePoint != null || e.RightButton == MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition((Canvas)sender);
            FloatingPoint.Visibility = Visibility.Collapsed;
            _activePoint = _points.AddPoint(point);
            _activePoint.Dot = new Path
            {
                Fill = Brushes.Blue,
                Stroke = Brushes.Blue,
                Data = new EllipseGeometry(point, 10, 10),
            };
            _activePoint.Dot.MouseDown += _activePoint.OnMouseDown;
            _activeMin = _activePoint.GetMinValue();
            _activeMax = _activePoint.GetMaxValue();
            Graph.Children.Add(_activePoint.Dot);
            Canvas_Loaded(sender, null);
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var point = e.GetPosition((Canvas)sender);
            var lg = new LineGeometry(new Point(point.X, 0), new Point(point.X, 1000));
            var ix = GetIntersectionPoints(Curve.Data, lg);
            if (!ix.Any()) return;
            if (_activePoint != null)
            {
                _activePoint.Point.X = point.X >= _activeMax ? _activeMax - 0.0001
                    : point.X < _activeMin ? _activeMin + 0.0001 : point.X;
                _activePoint.Point.Y = point.Y;
                _activePoint.Dot.Data = new EllipseGeometry(_activePoint.Point, 10, 10);
                Canvas_Loaded(sender, null);
            }
            else
            {
                FloatingPoint.Data = new EllipseGeometry(ix[0], 10, 10);
            }
        }

        private void Graph_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            FloatingPoint.Visibility = Visibility.Visible;
        }

        private void Graph_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_activePoint != null)
            {
                _activePoint.Dot.Fill = Brushes.Red;
                _activePoint.Dot.Stroke = Brushes.Red;
                _activePoint = null;

            }
            FloatingPoint.Visibility = Visibility.Collapsed;
        }

        private void Graph_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_activePoint != null)
            {
                _activePoint.Dot.Fill = Brushes.Red;
                _activePoint.Dot.Stroke = Brushes.Red;
                _activePoint = null;
            }

            FloatingPoint.Visibility = Visibility.Visible;
        }

        public void SetActivePoint(CurvePoint curvePoint)
        {
            _activePoint = curvePoint;
        }

        public void Changed()
        {
            Canvas_Loaded(Graph, null);
        }
    }

    public class CurvePointList : List<CurvePoint>
    {
        public double MaxValue = 1000;

        public double MinValue = 0;

        public CurvePointList(BezierCurveControl control)
        {
            Owner = control;
        }

        public readonly BezierCurveControl Owner;

        public Point[] ToPointArray()
        {
            return this.Select(i => i.Point).ToArray();
        }

        public CurvePoint AddPoint(Point point)
        {
            var cp = new CurvePoint(point.X, point.Y, this);
            var other = Find(i => Math.Abs(cp.Point.X - i.Point.X) < 0.1);
            if(other != null)
            {
                return other;
            }
            Add(cp);

            Sort();

            return cp;
        }
    }

    public class CurvePoint : IComparable<CurvePoint>
    {
        private readonly CurvePointList _list;
        public Point Point;
        public Path Dot;

        public CurvePoint(double x, double y, CurvePointList list)
        {
            _list = list;
            Point = new Point(x, y);
        }

        public double GetMinValue()
        {
            var me = _list.IndexOf(this);
            if (me > 0)
            {
                var prev = _list.ElementAt(me - 1);
                return prev.Point.X;
            }

            return _list.MinValue;
        }

        public double GetMaxValue()
        {
            var me = _list.IndexOf(this);
            if (me < _list.Count() - 1)
            {
                var next = _list.ElementAt(me + 1);
                return next.Point.X;
            }

            return _list.MaxValue;
        }

        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                _list.Owner.Graph.Children.Remove(Dot);
                _list.Remove(this);
                _list.Owner.SetActivePoint(null);
                _list.Owner.Changed();
                return;
            }

            Dot.Fill = Brushes.Blue;
            Dot.Stroke = Brushes.Blue;
            _list.Owner.SetActivePoint(this);
        }

        public int CompareTo(CurvePoint obj)
        {
            return Point.X.CompareTo(obj.Point.X);
        }
    }
}