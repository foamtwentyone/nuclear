"""
Готовит модель из нейросети (Meshy, Tripo и т. п., формат .glb) для игры.

Такие модели весят сотни тысяч треугольников, а текстура нарезана на сотни мелких кусков.
Скрипт упрощает сетку до заданного числа треугольников, заново раскладывает её на развёртку
и перепекает цвет с исходной модели на новую текстуру. На выходе:
  <name>.mesh.bytes — сетка в координатах Unity (метры, Y вверх, морда к +Z, низ на y = 0);
  <name>.png        — текстура цвета.
Префаб из них собирает PrototypeSetup (Nuclear → Собрать сцену прототипа).

Запуск: python Tools/bake_model.py "Assets/Art/Models/Frog Source.glb" Assets/Art/Models/Frog --triangles 16000 --width 1.3
Нужно: pip install trimesh fast-simplification xatlas scipy pillow numpy
"""
import argparse
import struct

import numpy as np
import trimesh
import fast_simplification
import xatlas
from PIL import Image
from scipy.spatial import cKDTree


def load(path):
    scene = trimesh.load(path)
    mesh = scene.to_geometry() if isinstance(scene, trimesh.Scene) else scene
    texture = np.asarray(mesh.visual.material.baseColorTexture.convert("RGB"), dtype=np.float32)
    return mesh, texture


def sample_texture(texture, uv):
    """Билинейная выборка; uv в соглашении glTF (v сверху вниз после переворота)."""
    h, w = texture.shape[:2]
    x = (uv[:, 0] % 1.0) * w - 0.5
    y = (1.0 - uv[:, 1] % 1.0) * h - 0.5
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = (x - x0)[:, None]
    fy = (y - y0)[:, None]
    x0 %= w
    y0 %= h
    x1 = (x0 + 1) % w
    y1 = (y0 + 1) % h
    top = texture[y0, x0] * (1 - fx) + texture[y0, x1] * fx
    bottom = texture[y1, x0] * (1 - fx) + texture[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def surface_samples(mesh, per_face, rng):
    """Плотные точки по исходной поверхности с их цветом — по ним перепекается текстура."""
    faces = mesh.faces
    uv = mesh.visual.uv
    r = rng.random((len(faces), per_face, 2))
    flip = r.sum(-1) > 1
    r[flip] = 1 - r[flip]
    bary = np.concatenate([1 - r.sum(-1, keepdims=True), r], -1)  # (F, k, 3)
    points = np.einsum("fkc,fcd->fkd", bary, mesh.vertices[faces]).reshape(-1, 3)
    uvs = np.einsum("fkc,fcd->fkd", bary, uv[faces]).reshape(-1, 2)
    return points, uvs


def rasterize_atlas(uvs, faces, size):
    """Для каждого текселя развёртки: номер треугольника и барицентрические координаты."""
    face_of = np.full((size, size), -1, np.int32)
    bary_of = np.zeros((size, size, 3), np.float32)
    pixels = uvs * size - 0.5
    for f, (a, b, c) in enumerate(faces):
        pa, pb, pc = pixels[a], pixels[b], pixels[c]
        lo = np.floor(np.minimum(np.minimum(pa, pb), pc)).astype(int)
        hi = np.ceil(np.maximum(np.maximum(pa, pb), pc)).astype(int)
        lo = np.clip(lo, 0, size - 1)
        hi = np.clip(hi, 0, size - 1)
        xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        p = np.stack([xs.ravel(), ys.ravel()], -1).astype(np.float64)
        v0, v1 = pb - pa, pc - pa
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        d = p - pa
        u = (d[:, 0] * v1[1] - v1[0] * d[:, 1]) / den
        v = (v0[0] * d[:, 1] - d[:, 0] * v0[1]) / den
        w = 1 - u - v
        # Небольшой допуск, чтобы швы между кусками развёртки не оставались пустыми.
        inside = (u >= -0.02) & (v >= -0.02) & (w >= -0.02)
        xi, yi = xs.ravel()[inside], ys.ravel()[inside]
        face_of[yi, xi] = f
        bary_of[yi, xi] = np.stack([w[inside], u[inside], v[inside]], -1)
    return face_of, bary_of


def dilate(image, filled, steps):
    """Растягивает края кусков развёртки наружу — без тёмных щелей при мипмапах."""
    image = image.copy()
    filled = filled.copy()
    for _ in range(steps):
        total = np.zeros_like(image)
        count = np.zeros(filled.shape, np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)):
            shifted = np.roll(np.roll(image, dy, 0), dx, 1)
            mask = np.roll(np.roll(filled, dy, 0), dx, 1)
            total += shifted * mask[..., None]
            count += mask
        grow = (~filled) & (count > 0)
        image[grow] = total[grow] / count[grow][:, None]
        filled |= grow
    return image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input")
    parser.add_argument("output", help="путь без расширения, напр. Assets/Art/Models/Frog")
    parser.add_argument("--triangles", type=int, default=16000)
    parser.add_argument("--width", type=float, default=1.3, help="ширина модели в игре, м (по X)")
    parser.add_argument("--texture", type=int, default=1024)
    args = parser.parse_args()
    rng = np.random.default_rng(1)

    high, texture = load(args.input)
    welded = trimesh.Trimesh(high.vertices, high.faces, process=False)
    welded.merge_vertices(merge_tex=True, merge_norm=True)
    vertices, faces = fast_simplification.simplify(
        welded.vertices.astype(np.float32), welded.faces.astype(np.int32),
        target_reduction=1 - args.triangles / len(welded.faces), agg=7)
    low = trimesh.Trimesh(vertices, faces, process=False)
    print(f"{len(high.faces)} → {len(low.faces)} треугольников")

    # Масштаб и положение: ширина по X = --width, низ на нуле, центр по XZ.
    scale = args.width / np.ptp(low.vertices[:, 0])
    offset = -np.array([
        (low.vertices[:, 0].min() + low.vertices[:, 0].max()) / 2,
        low.vertices[:, 1].min(),
        (low.vertices[:, 2].min() + low.vertices[:, 2].max()) / 2])
    normals = low.vertex_normals.copy()

    mapping, atlas_faces, atlas_uvs = xatlas.parametrize(low.vertices, low.faces)
    size = args.texture
    face_of, bary_of = rasterize_atlas(atlas_uvs, atlas_faces, size)
    filled = face_of >= 0
    positions = low.vertices[mapping]
    texel_points = np.einsum("nc,ncd->nd", bary_of[filled], positions[atlas_faces[face_of[filled]]])

    sample_points, sample_uvs = surface_samples(high, 6, rng)
    tree = cKDTree(sample_points)
    _, nearest = tree.query(texel_points, k=4)
    colors = sample_texture(texture, sample_uvs[nearest.ravel()]).reshape(-1, 4, 3).mean(1)

    image = np.zeros((size, size, 3), np.float32)
    image[filled] = colors
    image = dilate(image, filled, 8)
    # Строка 0 растеризации — v = 0, а у Unity v = 0 — нижний край картинки: переворачиваем по вертикали.
    Image.fromarray(np.clip(image[::-1], 0, 255).astype(np.uint8)).save(args.output + ".png")

    # Unity левосторонняя: X зеркалится, порядок обхода треугольников меняется.
    out_positions = (positions + offset) * scale
    out_positions[:, 0] *= -1
    out_normals = normals[mapping].copy()
    out_normals[:, 0] *= -1
    out_uvs = atlas_uvs
    out_indices = atlas_faces[:, [0, 2, 1]].astype(np.int32)
    with open(args.output + ".mesh.bytes", "wb") as f:
        f.write(b"NMSH")
        f.write(struct.pack("<iii", 1, len(out_positions), out_indices.size))
        f.write(out_positions.astype("<f4").tobytes())
        f.write(out_normals.astype("<f4").tobytes())
        f.write(out_uvs.astype("<f4").tobytes())
        f.write(out_indices.astype("<i4").tobytes())
    print(f"{len(out_positions)} вершин, текстура {size}², масштаб {scale:.3f}, "
          f"размер {np.ptp(out_positions, 0).round(3)} м")


if __name__ == "__main__":
    main()
