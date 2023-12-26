using System;
using UnityEngine;

public class MeshOrthoRenderer {
    private static Material _material;

    public static RenderResult Render(GameObject objectToRender, Color renderColor, Vector3 position, Vector2 size, Quaternion rotation, int outputWidth) {
        var mesh = objectToRender.GetComponent<MeshFilter>().sharedMesh;
        int renderWidth = outputWidth;
        int renderHeight = (int)(outputWidth / size.x * size.y);

        RenderTexture renderTexture = new RenderTexture(renderWidth, renderHeight, 24);
        Graphics.SetRenderTarget(renderTexture);
        GL.Clear(true, true, Color.white);

        _material ??= new Material(Shader.Find("Hidden/FMB/SolidColor"));
        _material.color = renderColor;

        _material.SetPass(0);
        GL.PushMatrix();
        GL.LoadIdentity();
        GL.LoadProjectionMatrix(Matrix4x4.Ortho(0, size.x, 0, size.y, -1, 100));
        var viewMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);

        // apply z-flip. Unity has it on view matrices. I don't know why.
        viewMatrix.m02 *= -1;
        viewMatrix.m12 *= -1;
        viewMatrix.m22 *= -1;
        viewMatrix.m32 *= -1;

        Graphics.DrawMeshNow(mesh, viewMatrix.inverse * objectToRender.transform.localToWorldMatrix);
        GL.PopMatrix();

        Graphics.SetRenderTarget(null);

        return new RenderResult(renderTexture);
    }

    public class RenderResult : IDisposable {
        public readonly RenderTexture renderTexture;

        public RenderResult(RenderTexture renderTexture) {
            this.renderTexture = renderTexture;
        }

        public void Dispose() {
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }
}