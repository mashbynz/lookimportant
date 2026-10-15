"""Build the workbench scene procedurally and export it for Unity.

Run headless from the repo root:
    blender -b -P blender/build_scene.py

Writes:
    blender/workbench.blend                 editable source
    game/Assets/Art/Models/Workbench.fbx    what Unity imports

Conventions (Blender world space, metres):
    +X right, +Y away from the player (towards the wall), +Z up.
    The bench front edge is y = 0, the wall is y = WALL_Y.
Interactive parts are separate objects whose origin is the pivot they move
about; Unity scripts find them by name, so renaming here means renaming in
Assets/Editor/SceneBuilder.cs too. Empties named *_Point / *_Target mark where
Unity should place lights and the camera.
"""
import json
import math
import pathlib

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = pathlib.Path(__file__).resolve().parent.parent
ART = ROOT / "game" / "Assets" / "Art"
BLEND_OUT = ROOT / "blender" / "workbench.blend"
FBX_OUT = ART / "Models" / "Workbench.fbx"

# --- layout constants -------------------------------------------------------
BENCH_W, BENCH_D = 1.8, 0.7
TOP_Z = 0.86            # bench top surface
WALL_Y = 0.78           # back wall surface
PB_Y = WALL_Y - 0.02    # pegboard front face (on 20 mm battens)
PB_COLS, PB_ROWS, PB_PITCH = 53, 31, 0.03
PB_Z0 = 0.98            # pegboard bottom edge
PB_X0 = -PB_COLS * PB_PITCH / 2
ROOM_HALF_W, ROOM_H, ROOM_FRONT = 1.25, 2.6, -2.0
SHARP_DEG = 50.0
WIRE_R = 0.0022         # pegboard hook wire radius


def hole(col, row):
    """Pegboard-local (x, z) of a hole centre."""
    return PB_X0 + (col + 0.5) * PB_PITCH, PB_Z0 + (row + 0.5) * PB_PITCH


# --- scene / materials ------------------------------------------------------
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.unit_settings.system = "METRIC"
    s.unit_settings.scale_length = 1.0


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def make_materials():
    spec = json.loads((ART / "materials.json").read_text())
    mats = {}
    for name, m in spec.items():
        if name.startswith("_"):
            continue
        mat = bpy.data.materials.new(name)
        if mat.node_tree is None:
            mat.use_nodes = True
        nt = mat.node_tree
        nodes, links = nt.nodes, nt.links
        bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            nodes.clear()
            bsdf = nodes.new("ShaderNodeBsdfPrincipled")
            out = nodes.new("ShaderNodeOutputMaterial")
            links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        lin = [srgb_to_linear(c) for c in m["color"]]
        bsdf.inputs["Metallic"].default_value = m.get("metallic", 0.0)
        bsdf.inputs["Roughness"].default_value = 1.0 - m.get("smoothness", 0.5)
        bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
        mat.diffuse_color = (*lin, 1.0)
        if "texture" in m:
            tdir = ART / "Textures" / m["texture"]
            coord = nodes.new("ShaderNodeTexCoord")
            mapping = nodes.new("ShaderNodeMapping")
            s = 1.0 / m.get("tile_m", 1.0)
            mapping.inputs["Scale"].default_value = (s, s, 1.0)
            links.new(coord.outputs["UV"], mapping.inputs["Vector"])
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(str(tdir / f"{m['texture']}_albedo.png"))
            tex.image.alpha_mode = "CHANNEL_PACKED"
            links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
            tint = nodes.new("ShaderNodeVectorMath")
            tint.operation = "MULTIPLY"
            tint.inputs[1].default_value = lin
            links.new(tex.outputs["Color"], tint.inputs[0])
            links.new(tint.outputs["Vector"], bsdf.inputs["Base Color"])
            sm = nodes.new("ShaderNodeMath")
            sm.operation = "MULTIPLY"
            sm.inputs[1].default_value = m.get("smoothness", 0.5)
            links.new(tex.outputs["Alpha"], sm.inputs[0])
            rough = nodes.new("ShaderNodeMath")
            rough.operation = "SUBTRACT"
            rough.inputs[0].default_value = 1.0
            links.new(sm.outputs["Value"], rough.inputs[1])
            links.new(rough.outputs["Value"], bsdf.inputs["Roughness"])
            npath = tdir / f"{m['texture']}_normal.png"
            if npath.exists():
                ntex = nodes.new("ShaderNodeTexImage")
                ntex.image = bpy.data.images.load(str(npath))
                ntex.image.colorspace_settings.name = "Non-Color"
                links.new(mapping.outputs["Vector"], ntex.inputs["Vector"])
                nmap = nodes.new("ShaderNodeNormalMap")
                nmap.inputs["Strength"].default_value = m.get("normal_scale", 1.0)
                links.new(ntex.outputs["Color"], nmap.inputs["Color"])
                links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
        if "emission" in m and m.get("lit"):
            bsdf.inputs["Emission Color"].default_value = (*[srgb_to_linear(c) for c in m["emission"]], 1.0)
            bsdf.inputs["Emission Strength"].default_value = m.get("emission_on", 1.0)
        mats[name] = mat
    return mats


MATS = {}
COLL = None


# --- geometry helpers -------------------------------------------------------
def mat4(loc=(0, 0, 0), rot=(0, 0, 0), scale=None):
    m = Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
    if scale is not None:
        m = m @ Matrix.Diagonal((*scale, 1.0))
    return m


def look_matrix(origin, direction):
    """Matrix placing local +Z along `direction` at `origin`."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized())
    return Matrix.Translation(Vector(origin)) @ q.to_matrix().to_4x4()


def mark_sharp(t, bevel_faces=()):
    bevel_faces = set(bevel_faces)
    limit = math.radians(SHARP_DEG)
    for e in t.edges:
        if len(e.link_faces) != 2:
            continue
        a, b = e.link_faces
        sharp = e.calc_face_angle(0.0) > limit
        if bevel_faces and ((a in bevel_faces) != (b in bevel_faces)):
            sharp = True
        e.smooth = not sharp


def bevel_all(t, amount, segments=2):
    if amount <= 0:
        mark_sharp(t)
        return
    r = bmesh.ops.bevel(t, geom=list(t.edges), offset=amount, offset_type="OFFSET",
                        segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)
    mark_sharp(t, r["faces"])


class MB:
    """Accumulates primitives into one mesh. Each primitive is built in its own
    temporary bmesh, transformed, and appended with a material and UVs."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.mat_names = []

    def mi(self, mname):
        if mname not in MATS:
            raise KeyError(mname)
        if mname not in self.mat_names:
            self.mat_names.append(mname)
        return self.mat_names.index(mname)

    def merge(self, t, matrix=Matrix(), mat="M_Steel", mats=None, keep_uv=False, recalc=True, uv_swap=False):
        """Append temp bmesh t. `mats` maps a face's material_index in t to a name."""
        if recalc:
            bmesh.ops.recalc_face_normals(t, faces=t.faces)
        if not any(not e.smooth for e in t.edges):
            mark_sharp(t)
        src_uv = t.loops.layers.uv.active
        vmap = {v: self.bm.verts.new(matrix @ v.co) for v in t.verts}
        for f in t.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts])
            name = mats[f.material_index] if mats else mat
            nf.material_index = self.mi(name)
            nf.smooth = True
            nf.normal_update()
            if keep_uv and src_uv is not None:
                for ls, ld in zip(f.loops, nf.loops):
                    ld[self.uv].uv = ls[src_uv].uv
            else:
                n = nf.normal
                ax = max(range(3), key=lambda i: abs(n[i]))
                for ld in nf.loops:
                    c = ld.vert.co
                    ld[self.uv].uv = (c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else \
                        ((c.y, c.x) if uv_swap else (c.x, c.y))
        for e in t.edges:
            ne = self.bm.edges.get([vmap[v] for v in e.verts])
            if ne is not None:
                ne.smooth = e.smooth
        t.free()
        return self

    # primitives -----------------------------------------------------------
    def box(self, size, center=(0, 0, 0), rot=(0, 0, 0), mat="M_Steel", bevel=0.0, segs=2, matrix=None,
            uv_swap=False):
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        bmesh.ops.scale(t, vec=Vector(size), verts=t.verts)
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        bevel_all(t, bevel, segs)
        return self.merge(t, (matrix or Matrix()) @ mat4(center, rot), mat, uv_swap=uv_swap)

    def cyl(self, r, h, center=(0, 0, 0), rot=(0, 0, 0), mat="M_Steel", segs=16, r2=None,
            bevel=0.0, matrix=None):
        """Cylinder (or cone with r2) along local Z, centred on `center`."""
        t = bmesh.new()
        bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r, radius2=r if r2 is None else r2, depth=h)
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        bevel_all(t, bevel, 2)
        return self.merge(t, (matrix or Matrix()) @ mat4(center, rot), mat)

    def rod(self, a, b, r, r2=None, mat="M_Steel", segs=12, matrix=None):
        a, b = Vector(a), Vector(b)
        d = b - a
        t = bmesh.new()
        bmesh.ops.create_cone(t, cap_ends=True, segments=segs, radius1=r,
                              radius2=r if r2 is None else r2, depth=d.length)
        bmesh.ops.translate(t, vec=(0, 0, d.length / 2), verts=t.verts)
        return self.merge(t, (matrix or Matrix()) @ look_matrix(a, d), mat)

    def sphere(self, r, center=(0, 0, 0), scale=(1, 1, 1), mat="M_Steel", segs=16, rings=8,
               hemi=False, matrix=None, rot=(0, 0, 0)):
        t = bmesh.new()
        bmesh.ops.create_uvsphere(t, u_segments=segs, v_segments=rings, radius=r)
        if hemi:
            bmesh.ops.delete(t, geom=[v for v in t.verts if v.co.z < -1e-6], context="VERTS")
        bmesh.ops.scale(t, vec=Vector(scale), verts=t.verts)
        return self.merge(t, (matrix or Matrix()) @ mat4(center, rot), mat, recalc=not hemi)

    def lathe(self, profile, segs=24, mats=None, mat="M_Steel", matrix=Matrix()):
        """Revolve [(r, z), ...] about Z. Profiles start and end on the axis
        (r = 0) so the result is closed. mats[i] names segment i's material."""
        t = bmesh.new()
        rings = []
        for r, z in profile:
            if r < 1e-9:
                rings.append([t.verts.new((0, 0, z))])
            else:
                rings.append([t.verts.new((r * math.cos(2 * math.pi * k / segs),
                                           r * math.sin(2 * math.pi * k / segs), z))
                              for k in range(segs)])
        for i in range(len(rings) - 1):
            a, b = rings[i], rings[i + 1]
            for k in range(segs):
                k2 = (k + 1) % segs
                if len(a) == 1 and len(b) == 1:
                    continue
                if len(a) == 1:
                    f = t.faces.new([a[0], b[k], b[k2]])
                elif len(b) == 1:
                    f = t.faces.new([a[k], a[k2], b[0]])
                else:
                    f = t.faces.new([a[k], a[k2], b[k2], b[k]])
                f.material_index = i if mats else 0
        return self.merge(t, matrix, mat, mats=mats)

    def torus(self, R, r, center=(0, 0, 0), rot=(0, 0, 0), mat="M_Steel", segs=24, msegs=8,
              arc=(0.0, 360.0), matrix=None):
        """Torus in the local XY plane (axis Z). Partial arcs are left open."""
        t = bmesh.new()
        full = abs(arc[1] - arc[0]) >= 360.0 - 1e-6
        n = segs if full else segs + 1
        grid = []
        for i in range(n):
            u = math.radians(arc[0] + (arc[1] - arc[0]) * i / segs)
            grid.append([t.verts.new(((R + r * math.cos(2 * math.pi * j / msegs)) * math.cos(u),
                                      (R + r * math.cos(2 * math.pi * j / msegs)) * math.sin(u),
                                      r * math.sin(2 * math.pi * j / msegs))) for j in range(msegs)])
        for i in range(segs):
            i2 = (i + 1) % n if full else i + 1
            for j in range(msegs):
                j2 = (j + 1) % msegs
                t.faces.new([grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2]])
        return self.merge(t, (matrix or Matrix()) @ mat4(center, rot), mat, recalc=False)

    def ring(self, r_in, r_out, thick, center=(0, 0, 0), rot=(0, 0, 0), mat="M_Steel", segs=24,
             arc=(0.0, 360.0), outer_segs=None, matrix=None):
        """Flat annulus (optionally a C-shaped arc) in local XY, thickness on Z."""
        t = bmesh.new()
        full = abs(arc[1] - arc[0]) >= 360.0 - 1e-6
        n = segs if full else segs + 1
        cols = []
        for i in range(n):
            a = math.radians(arc[0] + (arc[1] - arc[0]) * i / segs)
            ro = r_out
            if outer_segs:  # polygonal outer edge, e.g. a hex nut
                step = 2 * math.pi / outer_segs
                ro = r_out * math.cos(step / 2) / math.cos((a % step) - step / 2)
            c, s = math.cos(a), math.sin(a)
            cols.append([t.verts.new((r_in * c, r_in * s, -thick / 2)), t.verts.new((ro * c, ro * s, -thick / 2)),
                         t.verts.new((ro * c, ro * s, thick / 2)), t.verts.new((r_in * c, r_in * s, thick / 2))])
        for i in range(segs):
            i2 = (i + 1) % n if full else i + 1
            for j in range(4):
                j2 = (j + 1) % 4
                t.faces.new([cols[i][j], cols[i2][j], cols[i2][j2], cols[i][j2]])
        if not full:
            t.faces.new(cols[0])
            t.faces.new(list(reversed(cols[-1])))
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        mark_sharp(t)
        return self.merge(t, (matrix or Matrix()) @ mat4(center, rot), mat)

    def prism(self, pts, depth, matrix=Matrix(), mat="M_Steel", bevel=0.0, segs=2):
        """Extrude a 2D outline (local XY) along +Z by depth."""
        t = bmesh.new()
        f = t.faces.new([t.verts.new((x, y, 0.0)) for x, y in pts])
        r = bmesh.ops.extrude_face_region(t, geom=[f])
        bmesh.ops.translate(t, vec=(0, 0, depth), verts=[g for g in r["geom"] if isinstance(g, bmesh.types.BMVert)])
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        bevel_all(t, bevel, segs)
        return self.merge(t, matrix, mat)

    def quad(self, corners, normal, mat):
        t = bmesh.new()
        vs = [t.verts.new(c) for c in corners]
        f = t.faces.new(vs)
        f.normal_update()
        if f.normal.dot(Vector(normal)) < 0:
            f.normal_flip()
        return self.merge(t, Matrix(), mat, recalc=False)

    def disc(self, r, normal_axis, center=(0, 0, 0), mat="M_Steel", segs=32, uv01=False, matrix=None):
        """Flat disc facing +Z (in local space) with optional 0..1 planar UVs."""
        t = bmesh.new()
        uvl = t.loops.layers.uv.verify()
        c = t.verts.new((0, 0, 0))
        ring = [t.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), 0))
                for k in range(segs)]
        for k in range(segs):
            f = t.faces.new([c, ring[k], ring[(k + 1) % segs]])
            for l in f.loops:
                l[uvl].uv = (0.5 + l.vert.co.x / (2 * r), 0.5 + l.vert.co.y / (2 * r))
        return self.merge(t, (matrix or Matrix()) @ look_matrix(center, normal_axis), mat,
                          keep_uv=uv01, recalc=False)

    # finish ---------------------------------------------------------------
    def build(self, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
        bmesh.ops.triangulate(self.bm, faces=[f for f in self.bm.faces if len(f.verts) > 4])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for n in self.mat_names:
            me.materials.append(MATS[n])
        return place(bpy.data.objects.new(self.name, me), parent, loc, rot)


def place(ob, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
    COLL.objects.link(ob)
    ob.parent = parent
    ob.location = loc
    ob.rotation_euler = [math.radians(a) for a in rot]
    return ob


def empty(name, parent=None, loc=(0, 0, 0), rot=(0, 0, 0), size=0.02):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "ARROWS"
    ob.empty_display_size = size
    return place(ob, parent, loc, rot)


def instance(name, src, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
    """Copy of a template mesh (FBX instancing loses per-object materials)."""
    me = src.data.copy()
    me.name = name
    return place(bpy.data.objects.new(name, me), parent, loc, rot)


# --- props ------------------------------------------------------------------
def build_room():
    root = empty("Room")
    w, f, h = ROOM_HALF_W, ROOM_FRONT, ROOM_H
    MB("Room_Floor").quad([(-w, f, 0), (w, f, 0), (w, WALL_Y, 0), (-w, WALL_Y, 0)], (0, 0, 1), "M_Floor").build(root)
    MB("Room_WallBack").quad([(-w, WALL_Y, 0), (w, WALL_Y, 0), (w, WALL_Y, h), (-w, WALL_Y, h)], (0, -1, 0), "M_Wall").build(root)
    walls = MB("Room_WallSides")
    walls.quad([(-w, f, 0), (-w, WALL_Y, 0), (-w, WALL_Y, h), (-w, f, h)], (1, 0, 0), "M_Wall")
    walls.quad([(w, f, 0), (w, WALL_Y, 0), (w, WALL_Y, h), (w, f, h)], (-1, 0, 0), "M_Wall")
    walls.build(root)
    MB("Room_Ceiling").quad([(-w, f, h), (w, f, h), (w, WALL_Y, h), (-w, WALL_Y, h)], (0, 0, -1), "M_Ceiling").build(root)
    skirt = MB("Room_Skirting")
    skirt.box((2 * w, 0.015, 0.09), (0, WALL_Y - 0.0075, 0.045), mat="M_BenchFrame", bevel=0.003)
    for s in (-1, 1):
        skirt.box((0.015, WALL_Y - f, 0.09), (s * (w - 0.0075), (WALL_Y + f) / 2, 0.045), mat="M_BenchFrame", bevel=0.003)
    skirt.build(root)
    # conduit feeding the knife switch from the ceiling
    c = MB("Room_Conduit")
    c.rod((0.47, WALL_Y - 0.012, h), (0.47, WALL_Y - 0.012, 2.09), 0.011, mat="M_Steel", segs=16)
    c.box((0.09, 0.045, 0.09), (0.47, WALL_Y - 0.0225, 2.05), mat="M_BenchFrame", bevel=0.004)
    c.cyl(0.006, 0.01, (0.47, WALL_Y - 0.03, 2.0), mat="M_Rubber")
    c.build(root)
    # fluorescent fixture
    cl = empty("CeilingLight", None, (0, 0.30, h))
    m = MB("CeilingLight_Housing")
    m.box((1.25, 0.17, 0.05), (0, 0, -0.025), mat="M_PaintCream", bevel=0.006)
    for s in (-1, 1):
        m.box((0.02, 0.15, 0.03), (s * 0.6, 0, -0.06), mat="M_PaintCream", bevel=0.003)
    m.build(cl)
    tubes = MB("CeilingLight_Tubes")
    for y in (-0.035, 0.035):
        tubes.rod((-0.59, y, -0.065), (0.59, y, -0.065), 0.013, mat="M_Fluoro", segs=12)
    tubes.build(cl)
    empty("CeilingLight_Point", cl, (0, 0, -0.08))
    empty("CeilingLight_Target", cl, (0, 0.22, -1.6))


def build_workbench():
    root = empty("Workbench")
    top = MB("Bench_Top")
    top.box((BENCH_W, BENCH_D, 0.05), (0, BENCH_D / 2, TOP_Z - 0.025), mat="M_BenchWood", bevel=0.004,
            uv_swap=True)
    top.build(root)
    fr = MB("Bench_Frame")
    lx, ly = BENCH_W / 2 - 0.06, (0.06, BENCH_D - 0.06)
    for x in (-lx, lx):
        for y in ly:
            fr.box((0.05, 0.05, TOP_Z - 0.05), (x, y, (TOP_Z - 0.05) / 2), mat="M_BenchFrame", bevel=0.003)
            fr.cyl(0.022, 0.012, (x, y, 0.006), mat="M_Rubber", segs=12)
    for y in ly:
        fr.box((2 * lx, 0.02, 0.08), (0, y, TOP_Z - 0.09), mat="M_BenchFrame", bevel=0.002)
        fr.box((2 * lx, 0.02, 0.04), (0, y, 0.15), mat="M_BenchFrame", bevel=0.002)
    for x in (-lx, lx):
        fr.box((0.02, ly[1] - ly[0], 0.08), (x, BENCH_D / 2, TOP_Z - 0.09), mat="M_BenchFrame", bevel=0.002)
        fr.box((0.02, ly[1] - ly[0], 0.04), (x, BENCH_D / 2, 0.15), mat="M_BenchFrame", bevel=0.002)
    fr.build(root)
    sh = MB("Bench_Shelf")
    sh.box((2 * lx + 0.02, ly[1] - ly[0] + 0.04, 0.02), (0, BENCH_D / 2, 0.18), mat="M_BenchWood", bevel=0.002)
    sh.build(root)


def pegboard_mesh():
    """Hardboard with real through-holes: one tile per hole, shared edges welded."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    cache = {}

    def v(x, y, z):
        k = (round(x, 6), round(y, 6), round(z, 6))
        if k not in cache:
            cache[k] = bm.verts.new(k)
        return cache[k]

    h, r, t, n = PB_PITCH / 2, 0.0045, 0.005, 8
    sq = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)]
    faces_front, faces_core = [], []
    for i in range(PB_COLS):
        for j in range(PB_ROWS):
            cx, cz = hole(i, j)
            P = [v(cx + a * h, 0, cz + b * h) for a, b in sq]
            C = [bm.verts.new((cx + r * math.cos(k * math.pi / 4), 0, cz + r * math.sin(k * math.pi / 4))) for k in range(n)]
            B = [bm.verts.new((cx + r * math.cos(k * math.pi / 4), t, cz + r * math.sin(k * math.pi / 4))) for k in range(n)]
            for k in range(n):
                k2 = (k + 1) % n
                faces_front.append(bm.faces.new([P[k], P[k2], C[k2], C[k]]))
                faces_core.append(bm.faces.new([C[k], C[k2], B[k2], B[k]]))
    for f in faces_front:
        f.material_index = 0
        f.smooth = True
        for l in f.loops:
            l[uv].uv = (l.vert.co.x, l.vert.co.z)
    for f in faces_core:
        f.material_index = 1
        f.smooth = True
        for l in f.loops:
            l[uv].uv = (l.vert.co.x * 4, l.vert.co.y * 4)
    bm.normal_update()
    mark_sharp(bm)
    me = bpy.data.meshes.new("Pegboard_Board")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MATS["M_Pegboard"])
    me.materials.append(MATS["M_PegboardCore"])
    return me


def build_pegboard():
    root = empty("Pegboard", None, (0, PB_Y, 0))
    place(bpy.data.objects.new("Pegboard_Board", pegboard_mesh()), root)
    x1, z0, z1 = -PB_X0, PB_Z0, PB_Z0 + PB_ROWS * PB_PITCH
    MB("Pegboard_Backing").quad([(-x1, 0.018, z0), (x1, 0.018, z0), (x1, 0.018, z1), (-x1, 0.018, z1)],
                                (0, -1, 0), "M_Shadow").build(root)
    fr = MB("Pegboard_Frame")
    w, inner = 0.035, 0.008
    fy = (0.02 - 0.012) / 2
    fd = 0.032
    fr.box((2 * (x1 + w - inner), fd, w), (0, fy, z1 + w / 2 - inner), mat="M_BenchFrame", bevel=0.004)
    fr.box((2 * (x1 + w - inner), fd, w), (0, fy, z0 - w / 2 + inner), mat="M_BenchFrame", bevel=0.004)
    for s in (-1, 1):
        fr.box((w, fd, z1 - z0 + 2 * (w - inner) - 2 * w), (s * (x1 + w / 2 - inner), fy, (z0 + z1) / 2),
               mat="M_BenchFrame", bevel=0.004)
    fr.build(root)
    return root


HOOK_MESH = None


def hook(name, parent, col, row, length=0.06):
    """J-hook in a pegboard hole. Returns pegboard-local hook top point at 70% reach."""
    global HOOK_MESH
    if HOOK_MESH is None:
        m = MB("Hook")
        m.rod((0, 0.012, 0), (0, -length, 0), WIRE_R, mat="M_Chrome", segs=8)
        m.sphere(WIRE_R, (0, -length, 0), mat="M_Chrome", segs=8, rings=4)
        m.rod((0, -length, 0), (0, -length, 0.014), WIRE_R, mat="M_Chrome", segs=8)
        m.rod((0, 0.012, 0), (0, 0.012, -0.012), WIRE_R, mat="M_Chrome", segs=8)
        HOOK_MESH = m.build()
        HOOK_MESH.parent = None
        COLL.objects.unlink(HOOK_MESH)
    x, z = hole(col, row)
    instance(name, HOOK_MESH, parent, (x, 0, z))
    return Vector((x, -length * 0.65, z + WIRE_R))


def wrench_mesh(name, s):
    m = MB(name)
    r_in, r_out = 0.0105 * s, 0.019 * s
    m.ring(r_in, r_out, 0.007 * s, (0, 0, -r_in), rot=(90, 0, 0), mat="M_Chrome", segs=20)
    m.box((0.016 * s, 0.0055 * s, 0.19 * s), (0, 0, -0.115 * s), mat="M_Chrome", bevel=0.0015)
    m.ring(0.0095 * s, 0.021 * s, 0.0065 * s, (0, 0, -0.225 * s), rot=(90, 0, 0), mat="M_Chrome", segs=16,
           arc=(-55 + 15, 235 + 15))
    return m


def screwdriver_mesh(name, handle_mat, shaft_len):
    m = MB(name)
    m.lathe([(0, 0.002), (0.0085, 0.002), (0.0115, 0.008), (0.0125, 0.02), (0.0125, 0.08),
             (0.011, 0.096), (0.0065, 0.104), (0, 0.106)], segs=16, mat=handle_mat)
    m.rod((0, 0, 0.004), (0, 0, -shaft_len), 0.0035, mat="M_Chrome", segs=10)
    m.box((0.007, 0.0012, 0.012), (0, 0, -shaft_len - 0.004), mat="M_Chrome")
    return m


def build_pegboard_items(pb):
    # tools hanging on the left
    p = hook("Hook_WrenchLarge", pb, 4, 22)
    t = empty("Tool_WrenchLarge", pb, p)
    wrench_mesh("Tool_WrenchLarge_Mesh", 1.0).build(t)
    p = hook("Hook_WrenchSmall", pb, 8, 22)
    t = empty("Tool_WrenchSmall", pb, p)
    wrench_mesh("Tool_WrenchSmall_Mesh", 0.78).build(t)

    a = hook("Hook_HammerL", pb, 13, 23)
    b = hook("Hook_HammerR", pb, 17, 23)
    t = empty("Tool_Hammer", pb, (a + b) / 2)
    m = MB("Tool_Hammer_Mesh")
    m.box((0.11, 0.024, 0.024), (0.005, 0, 0.0125), mat="M_Steel", bevel=0.003)
    m.cyl(0.0145, 0.016, (-0.056, 0, 0.0125), rot=(0, 90, 0), mat="M_Steel", segs=16, bevel=0.002)
    for y in (-0.0055, 0.0055):
        m.box((0.045, 0.008, 0.012), (0.075, y, 0.004), rot=(0, -25, 0), mat="M_Steel", bevel=0.002)
    m.rod((0.003, 0, 0.0), (0.003, 0, -0.30), 0.0125, 0.011, mat="M_HandleWood", segs=14)
    m.rod((0.003, 0, -0.19), (0.003, 0, -0.302), 0.0142, 0.0138, mat="M_Rubber", segs=14)
    m.build(t)

    # screwdriver rack, low on the left
    rx, rz = hole(18, 7)
    rack = empty("Pegboard_ScrewdriverRack", pb, (rx, 0, rz))
    m = MB("ScrewdriverRack_Mesh")
    m.box((0.17, 0.042, 0.003), (0, -0.023, 0), mat="M_Steel", bevel=0.0008)
    m.box((0.17, 0.003, 0.04), (0, -0.0035, -0.018), mat="M_Steel", bevel=0.0008)
    m.build(rack)
    for i, (dx, mat, ln) in enumerate(((-0.05, "M_RedPlastic", 0.13), (0.0, "M_AmberPlastic", 0.11),
                                       (0.05, "M_PaintYellow", 0.09))):
        t = empty(f"Tool_Screwdriver_{i + 1}", rack, (dx, -0.024, 0.0015))
        screwdriver_mesh(f"Tool_Screwdriver_{i + 1}_Mesh", mat, ln).build(t)

    # wire coil on the right
    p = hook("Hook_WireCoil", pb, 47, 21)
    t = empty("Tool_WireCoil", pb, p)
    m = MB("Tool_WireCoil_Mesh")
    for k, (dy, rr) in enumerate(((-0.006, 0), (0.0, 7), (0.006, -6))):
        m.torus(0.052, 0.0055, (0, dy, -0.047), rot=(90, rr, 0), mat="M_CableRed", segs=28, msegs=8)
    m.build(t)

    build_knife_switch(pb)
    build_gauge(pb)


def build_knife_switch(pb):
    root = empty("KnifeSwitch", pb, (0.47, 0, 1.36))
    base = MB("KnifeSwitch_Base")
    base.box((0.15, 0.018, 0.26), (0, -0.009, 0), mat="M_Slate", bevel=0.003)
    for x in (-0.06, 0.06):
        for z in (-0.115, 0.115):
            base.sphere(0.0045, (x, -0.018, z), scale=(1, 0.6, 1), mat="M_Brass", segs=10, rings=6)
    for x in (-0.035, 0.035):
        for z, plate_h in ((0.075, 0.035), (-0.075, 0.03)):
            base.box((0.016, 0.006, 0.02), (x, -0.021, z), mat="M_Copper", bevel=0.001)
            for dx in (-0.0035, 0.0035):
                base.box((0.003, 0.03, plate_h), (x + dx, -0.035, z), mat="M_Copper", bevel=0.0008)
        for z in (0.112, -0.112):
            base.cyl(0.0055, 0.012, (x, -0.024, z), rot=(90, 0, 0), mat="M_Brass", segs=6)
            base.cyl(0.003, 0.02, (x, -0.03, z), rot=(90, 0, 0), mat="M_Brass", segs=10)
    base.rod((-0.047, -0.04, -0.075), (0.047, -0.04, -0.075), 0.003, mat="M_Brass", segs=10)
    base.build(root)
    pivot = empty("KnifeSwitch_Pivot", root, (0, -0.04, -0.075))
    blades = MB("KnifeSwitch_Blades")
    for x in (-0.035, 0.035):
        blades.box((0.003, 0.012, 0.165), (x, 0, 0.0825), mat="M_Copper", bevel=0.0008)
    blades.build(pivot)
    h = MB("KnifeSwitch_Handle")
    h.box((0.1, 0.014, 0.016), (0, 0, 0.135), mat="M_Bakelite", bevel=0.003)
    h.rod((0, -0.006, 0.137), (0, -0.05, 0.15), 0.0065, mat="M_Bakelite", segs=12)
    h.sphere(0.0125, (0, -0.056, 0.152), scale=(1, 1.15, 1), mat="M_Bakelite", segs=16, rings=10)
    h.build(pivot)


def build_gauge(pb):
    root = empty("Gauge", pb, (0.06, 0, 1.52))
    body = MB("Gauge_Body")
    body.cyl(0.07, 0.03, (0, -0.015, 0), rot=(90, 0, 0), mat="M_PaintBlack", segs=40, bevel=0.003)
    body.torus(0.066, 0.0055, (0, -0.031, 0), rot=(90, 0, 0), mat="M_Brass", segs=40, msegs=10)
    body.ring(0.06, 0.068, 0.004, (0, -0.029, 0), rot=(90, 0, 0), mat="M_Brass", segs=40)
    body.cyl(0.008, 0.02, (0, -0.012, -0.075), mat="M_Rubber", segs=12)
    body.build(root)
    MB("Gauge_Dial").disc(0.0605, (0, -1, 0), (0, -0.0305, 0), mat="M_GaugeDial", segs=40, uv01=True).build(root)
    pivot = empty("Gauge_NeedlePivot", root, (0, -0.033, 0))
    nd = MB("Gauge_Needle")
    pts = [(-0.0024, -0.013), (0.0024, -0.013), (0.0013, 0.0), (0.0005, 0.052), (-0.0005, 0.052), (-0.0013, 0.0)]
    nd.prism(pts, 0.0012, matrix=Matrix(((1, 0, 0, 0), (0, 0, -1, 0.0006), (0, 1, 0, 0), (0, 0, 0, 1))),
             mat="M_NeedleRed")
    nd.cyl(0.0055, 0.004, (0, -0.002, 0), rot=(90, 0, 0), mat="M_PaintBlack", segs=16, bevel=0.0008)
    nd.build(pivot)


def sloped_housing(name, w, d, h_front, h_back, mat, bevel):
    """Console box: footprint w x d, sloped top from front to back height.
    Returns (MB, slope_center_z, slope_angle_deg)."""
    m = MB(name)
    pts = [(-d / 2, 0), (d / 2, 0), (d / 2, h_back), (-d / 2, h_front)]
    to_world = Matrix(((0, 0, 1, -w / 2), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    m.prism(pts, w, matrix=to_world, mat=mat, bevel=bevel, segs=2)
    angle = math.degrees(math.atan2(h_back - h_front, d))
    return m, (h_front + h_back) / 2, angle


def build_control_box():
    root = empty("ControlBox", None, (-0.36, 0.30, TOP_Z), (0, 0, 12))
    m, zc, ang = sloped_housing("ControlBox_Housing", 0.38, 0.18, 0.055, 0.13, "M_PaintOrange", 0.006)
    m.cyl(0.0065, 0.014, (0, 0.094, 0.03), rot=(90, 0, 0), mat="M_Rubber", segs=12)
    m.build(root)
    panel = empty("ControlBox_Panel", root, (0, 0, zc + 0.002), (ang, 0, 0))
    pl = MB("ControlBox_Faceplate")
    pl.box((0.34, 0.165, 0.003), (0, 0, -0.0005), mat="M_PaintCream", bevel=0.001)
    for x in (-0.16, 0.16):
        for y in (-0.072, 0.072):
            pl.sphere(0.003, (x, y, 0.001), scale=(1, 1, 0.5), mat="M_Chrome", segs=8, rings=4, hemi=True)
    for x in (-0.1, 0.0, 0.1):
        pl.box((0.05, 0.014, 0.0012), (x, -0.062, 0.0012), mat="M_Bakelite", bevel=0.0004)
        for k in range(int(round(x / 0.1)) + 2):  # 1, 2, 3 pips: identifiers without words
            pl.sphere(0.0018, (x - 0.012 * (int(round(x / 0.1)) + 1) / 2 + 0.012 * k, -0.062, 0.0018),
                      scale=(1, 1, 0.4), mat="M_PaintCream", segs=8, rings=4, hemi=True)
    pl.build(panel)

    base = MB("Toggle_Base")
    base.cyl(0.0125, 0.005, (0, 0, 0.0035), mat="M_Chrome", segs=6)
    base.cyl(0.0068, 0.011, (0, 0, 0.0115), mat="M_Chrome", segs=16)
    base = base.build()
    lever = MB("Toggle_Lever")
    lever.rod((0, 0, -0.002), (0, 0, 0.042), 0.0044, 0.003, mat="M_Chrome", segs=12)
    lever.sphere(0.0054, (0, 0, 0.043), mat="M_Chrome", segs=12, rings=8)
    lever = lever.build()
    bezel = MB("Lamp_Bezel")
    bezel.cyl(0.0138, 0.005, (0, 0, 0.0025), mat="M_Chrome", segs=16, bevel=0.0012)
    bezel = bezel.build()
    for ob in (base, lever, bezel):
        COLL.objects.unlink(ob)
    lens_mats = ["M_LampAmber", "M_LampGreen", "M_LampRed"]
    for i, x in enumerate((-0.1, 0.0, 0.1)):
        n = i + 1
        tg = empty(f"Toggle_{n}", panel, (x, -0.022, 0.001))
        instance(f"Toggle_{n}_Base", base, tg)
        pv = empty(f"Toggle_{n}_Pivot", tg, (0, 0, 0.014))
        instance(f"Toggle_{n}_Lever", lever, pv)
        lp = empty(f"Lamp_{n}", panel, (x, 0.047, 0.001))
        instance(f"Lamp_{n}_Bezel", bezel, lp)
        MB(f"Lamp_{n}_Lens").sphere(0.0105, (0, 0, 0.005), scale=(1, 1, 0.9), mat=lens_mats[i], segs=6, rings=4,
                                     hemi=True).build(lp)


def build_big_button():
    root = empty("BigButton", None, (0.03, 0.17, TOP_Z))
    m = MB("BigButton_Housing")
    m.box((0.1, 0.1, 0.05), (0, 0, 0.025), mat="M_PaintYellow", bevel=0.006)
    m.cyl(0.042, 0.006, (0, 0, 0.053), mat="M_Bakelite", segs=32, bevel=0.0015)
    m.cyl(0.007, 0.014, (0, 0.052, 0.02), rot=(90, 0, 0), mat="M_Rubber", segs=12)
    m.build(root)
    plunger = empty("BigButton_Plunger", root, (0, 0, 0.056))
    cap = MB("BigButton_Cap")
    cap.lathe([(0, -0.004), (0.024, -0.004), (0.024, 0.008), (0.036, 0.009), (0.038, 0.015), (0.036, 0.022),
               (0.028, 0.029), (0.015, 0.033), (0, 0.034)], segs=32, mat="M_RedPlastic")
    cap.build(plunger)


def build_dial_unit():
    root = empty("DialUnit", None, (0.34, 0.30, TOP_Z), (0, 0, -12))
    m, zc, ang = sloped_housing("DialUnit_Housing", 0.14, 0.12, 0.04, 0.075, "M_PaintOlive", 0.005)
    m.cyl(0.006, 0.012, (0, 0.064, 0.025), rot=(90, 0, 0), mat="M_Rubber", segs=12)
    m.build(root)
    panel = empty("DialUnit_Panel", root, (0, 0, zc + 0.002), (ang, 0, 0))
    plate = MB("DialUnit_Plate")
    t = bmesh.new()
    bmesh.ops.create_cube(t, size=1.0)
    bmesh.ops.scale(t, vec=(0.12, 0.12, 0.003), verts=t.verts)
    uvl = t.loops.layers.uv.verify()
    t.normal_update()
    for f in t.faces:
        f.material_index = 0 if f.normal.z > 0.9 else 1
        for l in f.loops:
            l[uvl].uv = (l.vert.co.x / 0.12 + 0.5, l.vert.co.y / 0.12 + 0.5)
    plate.merge(t, mat4((0, 0, -0.0005)), mats=["M_DialScale", "M_PaintCream"], keep_uv=True)
    plate.build(panel)
    pivot = empty("DialUnit_KnobPivot", panel, (0, 0, 0.001))
    k = MB("DialUnit_Knob")
    k.cyl(0.021, 0.005, (0, 0, 0.0025), mat="M_Bakelite", segs=24, bevel=0.001)
    # chicken-head outline, counter-clockwise: rounded back, then the pointer tip
    pts = [(0.0145 * math.cos(math.radians(a)), 0.0145 * math.sin(math.radians(a)))
           for a in range(160, 381, 20)]
    pts += [(0.004 * math.cos(math.radians(a)), 0.037 + 0.004 * math.sin(math.radians(a)))
            for a in (-10, 40, 90, 140, 190)]
    k.prism(pts, 0.019, matrix=mat4((0, 0, 0.005)), mat="M_Bakelite", bevel=0.0022)
    k.box((0.0028, 0.026, 0.0006), (0, 0.021, 0.0243), mat="M_PaintCream")
    k.build(pivot)


def build_desk_lamp():
    world = Vector((0.72, 0.50, TOP_Z))
    root = empty("DeskLamp", None, world)
    b = MB("DeskLamp_Base")
    b.lathe([(0, 0), (0.075, 0), (0.075, 0.012), (0.07, 0.02), (0.04, 0.026), (0.03, 0.032), (0, 0.032)],
            segs=32, mat="M_PaintTeal")
    S = Vector((0, 0.01, 0.06))
    b.rod((0, 0.01, 0.03), S, 0.01, mat="M_PaintTeal", segs=16)
    b.cyl(0.013, 0.034, S, rot=(0, 90, 0), mat="M_PaintTeal", segs=16, bevel=0.002)
    head_world = Vector((0.46, 0.34, 1.15))
    H = head_world - world
    E = Vector((0.0, 0.08, 0.37))
    for s in (-1, 1):
        b.rod(S + Vector((s * 0.012, 0, 0)), E + Vector((s * 0.012, 0, 0)), 0.0045, mat="M_Chrome", segs=10)
    b.cyl(0.011, 0.034, E, rot=(0, 90, 0), mat="M_PaintTeal", segs=16, bevel=0.002)
    d = (H - E)
    perp = d.cross(Vector((0, 0, 1))).normalized()
    for s in (-1, 1):
        b.rod(E + perp * s * 0.011, H + perp * s * 0.011, 0.0042, mat="M_Chrome", segs=10)
    b.build(root)
    sw = MB("DeskLamp_Switch")
    sw.cyl(0.0075, 0.007, (0, 0, 0.0035), mat="M_Chrome", segs=16, bevel=0.0012)
    sw.build(root, (-0.038, -0.036, 0.022))

    target = Vector((0.10, 0.26, TOP_Z))
    aim = (target - head_world).normalized()
    q = Vector((0, 0, -1)).rotation_difference(aim)
    head = empty("DeskLamp_Head", root, H)
    head.rotation_mode = "QUATERNION"
    head.rotation_quaternion = q
    sh = MB("DeskLamp_Shade")
    shade = [(0, 0.042), (0.016, 0.04), (0.024, 0.032), (0.032, 0.012), (0.05, -0.04), (0.068, -0.08),
             (0.078, -0.098), (0.0755, -0.1), (0.066, -0.082), (0.048, -0.043), (0.03, 0.006), (0, 0.012)]
    sh.lathe([(r * 0.8, z * 0.8) for r, z in shade], segs=32, mats=["M_PaintTeal"] * 7 + ["M_LampInner"] * 4)
    sh.cyl(0.011, 0.028, (0, 0, 0.026), rot=(90, 0, 0), mat="M_PaintTeal", segs=16, bevel=0.002)
    sh.build(head)
    MB("DeskLamp_Bulb").sphere(0.021, (0, 0, -0.024), scale=(1, 1, 1.15), mat="M_Bulb", segs=20, rings=12).build(head)
    empty("DeskLamp_Point", head, (0, 0, -0.036))
    empty("DeskLamp_Target", head, (0, 0, -1.0))


def build_lock_box():
    root = empty("LockBox", None, (-0.72, 0.42, TOP_Z), (0, 0, 20))
    m = MB("LockBox_Body")
    m.box((0.22, 0.15, 0.085), (0, 0, 0.0425), mat="M_PaintGreenDark", bevel=0.004)
    m.box((0.226, 0.156, 0.03), (0, 0, 0.101), mat="M_PaintGreenDark", bevel=0.005)
    for x in (-0.04, 0.04):
        m.rod((x, 0, 0.114), (x, 0, 0.127), 0.003, mat="M_Chrome", segs=10)
    m.rod((-0.043, 0, 0.127), (0.043, 0, 0.127), 0.0032, mat="M_Chrome", segs=10)
    for x in (-0.07, 0.07):
        m.cyl(0.006, 0.04, (x, 0.079, 0.086), rot=(0, 90, 0), mat="M_Chrome", segs=12)
    m.box((0.026, 0.003, 0.045), (0, -0.0795, 0.093), mat="M_Chrome", bevel=0.001)
    m.torus(0.008, 0.0019, (0, -0.085, 0.079), rot=(0, 90, 0), mat="M_Chrome", segs=16, msegs=6)
    m.build(root)
    pivot = empty("Padlock_Pivot", root, (0, -0.087, 0.0735))
    p = MB("Padlock_Mesh")
    R = 0.0105
    p.torus(R, 0.0022, (0, 0, -R), rot=(90, 0, 0), mat="M_Chrome", segs=12, msegs=8, arc=(0, 180))
    for x in (-R, R):
        p.rod((x, 0, -R), (x, 0, -0.026), 0.0022, mat="M_Chrome", segs=8)
    p.box((0.042, 0.018, 0.036), (0, 0, -0.043), mat="M_Brass", bevel=0.006, segs=3)
    p.cyl(0.0028, 0.001, (0, -0.0091, -0.046), rot=(90, 0, 0), mat="M_Shadow", segs=12)
    p.box((0.0016, 0.001, 0.007), (0, -0.0091, -0.0505), mat="M_Shadow")
    p.build(pivot)


def build_bench_clutter():
    mug = empty("Mug", None, (0.56, 0.10, TOP_Z), (0, 0, 215))
    m = MB("Mug_Mesh")
    m.lathe([(0, 0), (0.039, 0), (0.042, 0.004), (0.042, 0.095), (0.0395, 0.097), (0.037, 0.095),
             (0.037, 0.008), (0, 0.008)], segs=28, mat="M_Ceramic")
    m.torus(0.026, 0.0065, (0.046, 0, 0.05), rot=(90, 0, 0), mat="M_Ceramic", segs=14, msegs=8, arc=(-80, 80))
    m.disc(0.0372, (0, 0, 1), (0, 0, 0.074), mat="M_Coffee", segs=28)
    m.build(mug)

    sd = empty("Loose_Screwdriver", None, (-0.13, 0.50, TOP_Z + 0.0125), (93.5, 0, 62))
    screwdriver_mesh("Loose_Screwdriver_Mesh", "M_RedPlastic", 0.15).build(sd)

    nut = MB("Nut")
    nut.ring(0.0035, 0.0068, 0.0055, (0, 0, 0.00275), mat="M_Steel", segs=12, outer_segs=6)
    nut = nut.build()
    COLL.objects.unlink(nut)
    for i, (x, y, r) in enumerate(((-0.11, 0.29, 10), (-0.085, 0.355, 41), (-0.15, 0.40, 77), (-0.06, 0.25, 5))):
        instance(f"Loose_Nut_{i + 1}", nut, None, (x, y, TOP_Z), (0, 0, r))


def cable(name, pts, radius, mat, parent):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 2
    cu.resolution_u = 10
    cu.use_fill_caps = True
    sp = cu.splines.new("BEZIER")
    sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    tmp = bpy.data.objects.new(name + "_curve", cu)
    COLL.objects.link(tmp)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(SHARP_DEG))
    me.materials.append(MATS[mat])
    return place(bpy.data.objects.new(name, me), parent)


def build_cables():
    root = empty("Cables")
    gy = WALL_Y - 0.043  # just in front of the pegboard face
    ks = Vector((0.47, PB_Y, 1.36))
    tl, tr = ks + Vector((-0.035, -0.03, 0.112)), ks + Vector((0.035, -0.03, 0.112))
    bl, br = ks + Vector((-0.035, -0.03, -0.112)), ks + Vector((0.035, -0.03, -0.112))
    cable("Cable_PowerL", [(0.465, WALL_Y - 0.03, 1.99), (0.45, gy, 1.9), (0.43, gy, 1.62), tl + Vector((0, 0, 0.012))],
          0.0042, "M_CableRed", root)
    cable("Cable_PowerR", [(0.475, WALL_Y - 0.03, 1.99), (0.49, gy, 1.9), (0.51, gy, 1.62), tr + Vector((0, 0, 0.012))],
          0.0042, "M_Rubber", root)
    cable("Cable_Gauge", [bl - Vector((0, 0, 0.012)), (0.42, gy, 1.2), (0.25, gy, 1.17), (0.08, gy, 1.3),
                          (0.06, PB_Y - 0.012, 1.425)], 0.004, "M_Rubber", root)
    cable("Cable_Bench", [br - Vector((0, 0, 0.012)), (0.52, gy, 1.12), (0.5, gy, 0.99), (0.45, WALL_Y - 0.03, 0.88),
                          (0.36, 0.695, TOP_Z + 0.006), (0.05, 0.63, TOP_Z + 0.005), (-0.28, 0.55, TOP_Z + 0.005),
                          (-0.37, 0.45, TOP_Z + 0.006), (-0.38, 0.40, TOP_Z + 0.025)], 0.0045, "M_Rubber", root)
    cable("Cable_Dial", [(0.354, 0.37, TOP_Z + 0.025), (0.36, 0.45, TOP_Z + 0.005), (0.40, 0.63, TOP_Z + 0.005),
                         (0.415, 0.70, TOP_Z + 0.002), (0.42, 0.74, TOP_Z - 0.06)], 0.004, "M_Rubber", root)
    cable("Cable_Button", [(0.03, 0.235, TOP_Z + 0.02), (0.03, 0.31, TOP_Z + 0.005), (0.07, 0.52, TOP_Z + 0.005),
                           (0.11, 0.70, TOP_Z + 0.002), (0.11, 0.74, TOP_Z - 0.06)], 0.004, "M_Rubber", root)
    cable("Cable_LampCord", [(0.72, 0.57, TOP_Z + 0.008), (0.72, 0.66, TOP_Z + 0.005), (0.735, 0.71, TOP_Z),
                             (0.74, 0.745, TOP_Z - 0.07)], 0.0035, "M_Rubber", root)


def build_markers():
    # Player head position and resting look target (Unity places the camera here)
    empty("Camera_Point", None, (0, -0.70, 1.45))
    empty("Camera_Target", None, (0, 0.50, 1.15))


def export():
    BLEND_OUT.parent.mkdir(parents=True, exist_ok=True)
    FBX_OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT), compress=True)
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_OUT), use_selection=False, object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        path_mode="STRIP", embed_textures=False)


def main():
    global COLL, MATS
    reset()
    COLL = bpy.context.scene.collection
    MATS = make_materials()
    build_room()
    build_workbench()
    pb = build_pegboard()
    build_pegboard_items(pb)
    build_control_box()
    build_big_button()
    build_dial_unit()
    build_desk_lamp()
    build_lock_box()
    build_bench_clutter()
    build_cables()
    build_markers()
    export()
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:
        o.data.calc_loop_triangles()
    tris = sum(len(o.data.loop_triangles) for o in meshes)
    print(f"BUILD OK: {len(bpy.data.objects)} objects, {len(meshes)} meshes, {tris} triangles")
    print(f"  {BLEND_OUT}\n  {FBX_OUT}")


main()
