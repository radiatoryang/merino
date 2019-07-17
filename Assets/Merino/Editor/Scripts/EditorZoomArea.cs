﻿// http://martinecker.com/martincodes/unity-editor-window-zooming/

using UnityEngine;

namespace Merino
{
    public class EditorZoomArea
    {
        private static Matrix4x4 _prevGuiMatrix;
        private static Vector2 offset = new Vector2(2.0f, 19.0f);
        public static Vector2 Offset { get { return offset; } set { offset = value; } }
        
        public static Rect Begin(float zoomScale, Rect screenCoordsArea)
        {
            GUI.EndGroup();        // End the group Unity begins automatically for an EditorWindow to clip out the window tab. This allows us to draw outside of the size of the EditorWindow.
            
            Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
            clippedArea.position += offset;
            GUI.BeginGroup(clippedArea);
            
            _prevGuiMatrix = GUI.matrix;

            Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;
            

            return clippedArea;
        }
        
        public static void End()
        {
            GUI.matrix = _prevGuiMatrix;
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(offset.x, offset.y, Screen.width, Screen.height));
        }
    }
    
    // Helper Rect extension methods
    public static class RectExtensions
    {
        public static Vector2 TopLeft(this Rect rect)
        {
            return new Vector2(rect.xMin, rect.yMin);
        }

        public static Rect ScaleSizeBy(this Rect rect, float scale)
        {
            return rect.ScaleSizeBy(scale, rect.center);
        }

        public static Rect ScaleSizeBy(this Rect rect, float scale, Vector2 pivotPoint)
        {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale;
            result.xMax *= scale;
            result.yMin *= scale;
            result.yMax *= scale;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }

        public static Rect ScaleSizeBy(this Rect rect, Vector2 scale)
        {
            return rect.ScaleSizeBy(scale, rect.center);
        }

        public static Rect ScaleSizeBy(this Rect rect, Vector2 scale, Vector2 pivotPoint)
        {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale.x;
            result.xMax *= scale.x;
            result.yMin *= scale.y;
            result.yMax *= scale.y;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }

        // from https://stackoverflow.com/questions/10657128/finding-line-segment-rectangle-intersection-point
        public static Vector2 Abs(this Vector2 vector) {
            for (int i = 0; i < 2; ++i) vector[i] = Mathf.Abs(vector[i]);
            return vector;
        }   

        public static Vector2 DividedBy(this Vector2 vector, Vector2 divisor) {
            for (int i = 0; i < 2; ++i) vector[i] /= divisor[i];
            return vector;
        }

        public static Vector2 Max(this Rect rect) {
            return new Vector2(rect.xMax, rect.yMax);
        }

        public static Vector2 RayIntersectToCenter(this Rect rect, Vector2 pointOnRay, float extrude=0f) {
            Vector2 pointOnRay_local = pointOnRay - rect.center;
            Vector2 edgeToRayRatios = (rect.Max() - rect.center).DividedBy(pointOnRay_local.Abs());
            Vector2 result = (edgeToRayRatios.x < edgeToRayRatios.y) ?
                new Vector2(pointOnRay_local.x > 0 ? rect.xMax : rect.xMin, 
                    pointOnRay_local.y * edgeToRayRatios.x + rect.center.y) :
                new Vector2(pointOnRay_local.x * edgeToRayRatios.y + rect.center.x, 
                    pointOnRay_local.y > 0 ? rect.yMax : rect.yMin);
            return result + (result - rect.center).normalized * extrude;
        }
    }
}