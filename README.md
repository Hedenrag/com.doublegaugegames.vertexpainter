# Vertex Painter

**Vertex Painter** is a lightweight Unity Editor tool for painting vertex colors directly onto meshes.

## Installation

1. Open the Unity Package Manager located in Window > Package Manager
2. Press the "+" symbol and select "Install Package from git URL"
3. paste the following Git URL: `https://github.com/Hedenrag/com.doublegaugegames.vertexpainter.git`

## Features

- Paint vertex colors in the Scene View using a brush.
- Adjustable brush radius and color.
- GPU-accelerated painting via Compute Shader for better.
- Automatic initialization of vertex colors if absent.

## Usage

1. Add a `MeshFilter` component to your GameObject.
2. Attach the `VertexPainter` component.
3. In the Inspector:
   - Enable the "Paint" toggle.
   - Set the desired paint color and radius.
4. If Asset is a file make a copy for changes to be saved.
5. In the Scene View:
   - Left-click and drag to paint.
   - Hold `Shift` to resize the brush.

## Limitations

- Only supports `MeshFilter` components (no `SkinnedMeshRenderer` support).
- Painted changes are not saved to the mesh asset automatically; manual saving is required.
- Requires GPU support for compute shaders.
